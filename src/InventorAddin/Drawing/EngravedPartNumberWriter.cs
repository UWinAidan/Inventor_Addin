using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Adds the engraved part number to a part (spec 05, "Added for M5", item 2; task 200). The text, its size, the depth and the feature
    /// name are Core's (<see cref="PartMarking"/>); this class reads the part and applies the result. Calls are those of spike 184,
    /// question 3 (round 2): the largest planar face by <c>Face.SurfaceType</c> and <c>Evaluator.Area</c>, <c>Sketches.Add(face, false)</c>,
    /// <c>ModelToSketchSpace(face.PointOnFace)</c>, <c>TextBoxes.AddFitted</c> with a Part Number property field,
    /// <c>Profiles.AddForSolid</c>, <c>EmbossFeatures.AddEngraveFromFace</c>.
    /// </summary>
    /// <remarks>Run it inside a transaction on the part, so one Undo in the part removes the sketch and the feature.</remarks>
    internal static class EngravedPartNumberWriter
    {
        /// <summary>
        /// Engraves the part number on the part's largest planar face. A negative direction is tried first (spike 184); when the volume does
        /// not drop, the feature is deleted (keeping its sketch) and the positive direction is tried. Throws, with the reasons, so the caller's transaction aborts and the part is left as it was,
        /// when no engraving was made.
        /// </summary>
        public static void Add(PartDocument part, PartMarkingOptions options, List<string> warnings)
        {
            PartComponentDefinition definition = part.ComponentDefinition;
            SurfaceBody? body = ComSafe.Get(() => definition.SurfaceBodies[1], warnings, "engrave: first body");
            if (body == null)
                throw new InvalidOperationException("The part has no solid body.");

            Face face = LargestPlanarFace(body, warnings)
                ?? throw new InvalidOperationException("The part has no planar face.");

            double depthCm = PartMarking.EngraveDepthCm(options.Units);
            double volumeBefore = definition.MassProperties.Volume;

            PlanarSketch sketch = definition.Sketches.Add(face, false);
            Point2d centre = sketch.ModelToSketchSpace(face.PointOnFace);
            Inventor.TextBox text = sketch.TextBoxes.AddFitted(centre, PartMarking.EngraveFormattedText(options.Units), Type.Missing);

            var reasons = new List<string>();
            foreach (PartFeatureExtentDirectionEnum direction in new[]
            {
                PartFeatureExtentDirectionEnum.kNegativeExtentDirection,
                PartFeatureExtentDirectionEnum.kPositiveExtentDirection,
            })
            {
                EmbossFeature feature;
                try
                {
                    // A fresh profile for each attempt: the first feature consumed the earlier one.
                    ObjectCollection items = InventorHost.App.TransientObjects.CreateObjectCollection();
                    items.Add(text);
                    Profile profile = sketch.Profiles.AddForSolid(false, items, Type.Missing);
                    feature = definition.Features.EmbossFeatures.AddEngraveFromFace(profile, depthCm, direction, Type.Missing, Type.Missing);
                }
                catch (Exception ex)
                {
                    reasons.Add($"{direction}: {ex.Message}");
                    continue;
                }

                if (PartMarking.EngraveRemovedMaterial(volumeBefore, definition.MassProperties.Volume))
                {
                    ComSafe.Run(() => feature.Name = PartMarking.EngraveFeatureName, warnings, "engrave feature name");
                    return;
                }

                reasons.Add($"{direction}: no material removed");
                // Keep the sketch (RetainConsumedSketches true) so the other direction can use it.
                feature.Delete(true, false, false);
            }

            throw new InvalidOperationException("The engraving could not be added (" + string.Join("; ", reasons) + ").");
        }

        private static Face? LargestPlanarFace(SurfaceBody body, List<string> warnings)
        {
            var faces = new List<Face>();
            var areas = new List<double>();
            foreach (Face face in body.Faces)
            {
                SurfaceTypeEnum? type = ComSafe.Get(() => (SurfaceTypeEnum?)face.SurfaceType, warnings, "face type");
                if (type != SurfaceTypeEnum.kPlaneSurface)
                    continue;
                faces.Add(face);
                areas.Add(ComSafe.Get(() => face.Evaluator.Area, warnings, "face area"));
            }

            int index = PartMarking.LargestAreaIndex(areas);
            return index < 0 ? null : faces[index];
        }
    }
}
