using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Holes
{
    /// <summary>
    /// The dowel and precision parts of an AWB hole (spec 09, "Dowel and precision holes"; spike 081 sections 4 and 5
    /// and round 5): the tolerance on the hole diameter, the fit colour on every face, and the stored data, applied
    /// together on OK or removed together when a dowel becomes another kind. Which holes have their faces put back to
    /// the part's appearance is <see cref="HoleSessionRules.ResetsFaceAppearances"/>'s decision. Nothing here opens a
    /// transaction; a failure throws, so the caller can abort.
    /// </summary>
    public static class HoleFitWriter
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        /// <summary>
        /// Applies what the add-in leaves on a hole when it is kept (task 089, OK): for a precision hole (a dowel, or a
        /// toleranced Clearance or Simple hole, tasks 113 and 115) the tolerance on the through diameter and the fit
        /// colour on every face, for any other hole no tolerance; then the stored data for
        /// <paramref name="build"/>'s resolved spec and the state applied. A seated hole's seat keeps the default
        /// tolerance (spike 108 question 5: ISO gives a stacked fit, Custom a deviation, on the hole diameter only).
        /// </summary>
        /// <param name="build">A successful <see cref="HoleRules.Build"/> result for the hole.</param>
        /// <param name="resetFaces">The hole was a dowel or precision hole and is kept as a hole that is not one, for
        /// example with Toleranced unticked (<see cref="HoleSessionRules.ResetsFaceAppearances"/>): every face goes back
        /// to the part's appearance (task 096). Ignored when the hole is kept as a precision hole.</param>
        public static void Finish(PartDocument doc, HoleFeature hole, HoleBuildResult build, bool resetFaces, ILog log)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (build?.Geometry is not { } geometry)
                throw new ArgumentException("The hole build did not succeed.", nameof(build));
            log ??= NullLog.Instance;

            if (geometry.FitColour is { } colour)
            {
                ApplyTolerance(hole, geometry.Tolerance, log);
                ApplyColour(doc, hole, build.Resolved.PrecisionFit, colour);
            }
            else
            {
                RemoveDowel(hole, resetFaces, log);
            }

            HoleStoredDataStore.Write(hole, new HoleStoredData(build.Resolved, HoleAppliedState.From(geometry)));
        }

        /// <summary>
        /// The tolerance on the hole diameter: an ISO fit Inventor works out itself, or upper and lower deviations in
        /// centimetres; none puts the default back. The model stays at the nominal size (spike 081 section 4). A Custom
        /// code (task 150) is applied as an ISO row's is: a hole class (H8) as the fit's hole tolerance, a shaft class
        /// (k6) as its shaft tolerance. Should Inventor refuse a shaft class on a hole diameter, its limits go on as
        /// deviations instead, with a warning in the log, so the hole keeps the tolerance the window showed.
        /// </summary>
        public static void ApplyTolerance(HoleFeature hole, HoleTolerance? tolerance, ILog? log = null)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            Parameter diameter = hole.HoleDiameter;
            if (tolerance == null)
            {
                if (ComSafe.Get(() => (ToleranceTypeEnum?)diameter.Tolerance.ToleranceType) != ToleranceTypeEnum.kDefaultTolerance)
                    diameter.Tolerance.SetToDefault();
                return;
            }

            if (tolerance.IsoCode is { } code && tolerance.IsShaftCode)
            {
                try
                {
                    diameter.Tolerance.SetToFits(ToleranceTypeEnum.kLimitsFitsStackedTolerance, "", code);
                }
                catch (Exception ex)
                {
                    (log ?? NullLog.Instance).Warn(LogPrefix + $"Inventor refused the shaft fit {code} on {ComSafe.Get(() => hole.Name)} "
                        + $"({ex.Message}); its limits are applied as deviations instead.");
                    diameter.Tolerance.SetToDeviation(tolerance.UpperCm, tolerance.LowerCm);
                }
            }
            else if (tolerance.IsoCode is { } holeCode)
            {
                diameter.Tolerance.SetToFits(ToleranceTypeEnum.kLimitsFitsStackedTolerance, holeCode, "");
            }
            else
            {
                diameter.Tolerance.SetToDeviation(tolerance.UpperCm, tolerance.LowerCm);
            }

            diameter.ModelValueType = ModelValueTypeEnum.kNominalValue;
        }

        /// <summary>Colours every face of the hole, the bottom included (spike 081 round 2), with the fit's appearance.</summary>
        public static void ApplyColour(PartDocument doc, HoleFeature hole, PrecisionFit fit, HoleColour colour)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            Asset appearance = FitAppearance(doc, fit, colour);
            foreach (Face face in hole.Faces)
                face.Appearance = appearance;
        }

        /// <summary>
        /// Puts every face of the hole, the bottom included, back to the part's appearance, whatever appearance it had
        /// (task 096; spec 09 question 8, Aidan): the fit colour and any colour a user set on a face.
        /// </summary>
        public static void RemoveColour(HoleFeature hole)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            foreach (Face face in hole.Faces)
                face.AppearanceSourceType = AppearanceSourceTypeEnum.kPartAppearance;
        }

        /// <summary>
        /// Removes everything dowel from a hole (spec 09; spike 081 round 5): the tolerance and the <c>AWB_Hole</c>
        /// attribute set, and, when <paramref name="resetFaces"/>, the colour on every face (<see cref="RemoveColour"/>).
        /// A hole that never was a dowel is passed false, so the colours a user set on its faces are kept.
        /// </summary>
        public static void RemoveDowel(HoleFeature hole, bool resetFaces, ILog log)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            log ??= NullLog.Instance;

            // A tapped hole's diameter may have no tolerance to reset.
            if (!ComSafe.Get(() => hole.Tapped))
                ApplyTolerance(hole, null);
            if (resetFaces)
                RemoveColour(hole);
            HoleStoredDataStore.Delete(hole);
            if (resetFaces)
                log.Info(LogPrefix + $"dowel tolerance and data removed from {ComSafe.Get(() => hole.Name)}; every face is back to the part's appearance.");
        }

        // ---- hand colours (task 122) ----

        /// <summary>
        /// The colours a user put on a hole by hand, read before the hole is deleted and made again (task 122): the
        /// appearance on each face that carries an override, and the feature's own appearance when one was set on it.
        /// AWB fit appearances and the preview appearance are not hand colours and are left out.
        /// </summary>
        public sealed class HandColours
        {
            internal HandColours(
                IReadOnlyList<HoleFaceKey?> keys, IReadOnlyList<string?> names, Dictionary<string, Asset> assets,
                Asset? featureAppearance)
            {
                Keys = keys;
                Names = names;
                Assets = assets;
                FeatureAppearance = featureAppearance;
            }

            internal IReadOnlyList<HoleFaceKey?> Keys { get; }

            internal IReadOnlyList<string?> Names { get; }

            internal Dictionary<string, Asset> Assets { get; }

            internal Asset? FeatureAppearance { get; }

            /// <summary>True when the hole had no hand colour at all.</summary>
            public bool IsEmpty => FeatureAppearance == null && !HoleFaceColours.AnyColour(Names);
        }

        /// <summary>The hole centres in model space (<c>SketchPoint.Geometry3d</c>), for <see cref="ReadHandColours"/>.</summary>
        public static IReadOnlyList<HoleVector> CentrePoints(ObjectCollection centres)
        {
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));

            var points = new List<HoleVector>();
            foreach (object centre in centres)
            {
                if (centre is SketchPoint sketchPoint && ComSafe.Get(() => sketchPoint.Geometry3d) is Point point)
                    points.Add(new HoleVector(point.X, point.Y, point.Z));
            }

            return points;
        }

        /// <summary>
        /// Reads the hand colours on <paramref name="hole"/> and the role of each of its faces at
        /// <paramref name="centres"/> (<see cref="HoleFaceColours.Classify"/>). A face whose shape or appearance cannot
        /// be read counts as having no hand colour. The feature's own colour is kept when its source counts as set by hand
        /// (<see cref="HoleFaceColours.IsFeatureHandSource"/>) and at least one face takes its colour from somewhere other
        /// than its own override (<see cref="HoleFaceColours.KeepFeatureColour"/>, task 140); the decision is logged at Info.
        /// </summary>
        public static HandColours ReadHandColours(HoleFeature hole, IReadOnlyList<HoleVector> centres, ILog? log = null)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));
            log ??= NullLog.Instance;

            var shapes = new List<HoleFaceShape?>();
            var names = new List<string?>();
            var faceSources = new List<string?>();
            var assets = new Dictionary<string, Asset>(StringComparer.Ordinal);
            foreach (Face face in FacesOf(hole))
            {
                shapes.Add(ReadShape(face));

                AppearanceSourceTypeEnum? faceSource = ComSafe.Get(() => (AppearanceSourceTypeEnum?)face.AppearanceSourceType);
                faceSources.Add(faceSource?.ToString());

                string? name = null;
                if (faceSource == AppearanceSourceTypeEnum.kOverrideAppearance
                    && ComSafe.Get(() => face.Appearance) is { } asset
                    && IsHandColour(asset))
                {
                    name = AssetKey(asset);
                    if (name != null)
                        assets[name] = asset;
                }

                names.Add(name);
            }

            // The feature's own colour (task 140): kept when its source is one set on the feature and at least one
            // face takes its colour from the feature, so a face override that moves the feature's source cannot
            // spread one face's colour over the whole new hole.
            Asset? featureAppearance = null;
            AppearanceSourceTypeEnum? source = ComSafe.Get(() => (AppearanceSourceTypeEnum?)hole.AppearanceSourceType);
            Asset? ownAsset = ComSafe.Get(() => hole.Appearance);
            string? ownName = ownAsset == null ? null : AssetKey(ownAsset);
            bool handSource = HoleFaceColours.IsFeatureHandSource(source?.ToString());
            bool keep = HoleFaceColours.KeepFeatureColour(source?.ToString(), faceSources);
            if (keep && ownAsset != null && IsHandColour(ownAsset))
                featureAppearance = ownAsset;
            int overridden = faceSources.Count(f => f == nameof(AppearanceSourceTypeEnum.kOverrideAppearance));
            string decision = featureAppearance != null
                ? "kept as the hole's own colour."
                : handSource && !keep
                    ? "not kept: no face takes its colour from the hole, so it is not a colour set on the hole."
                    : "not a colour set on the hole.";
            log.Info(LogPrefix + $"{ComSafe.Get(() => hole.Name)} reports appearance source {source?.ToString() ?? "(unread)"}, "
                + $"appearance {ownName ?? "(unread)"}, {overridden} of {faceSources.Count} face(s) overridden; " + decision);

            return new HandColours(HoleFaceColours.Classify(shapes, centres, SeatOf(hole)), names, assets, featureAppearance);
        }

        /// <summary>
        /// Puts <paramref name="colours"/>, read from the hole this one replaces, back on <paramref name="hole"/>: the
        /// feature's appearance, then each face's colour on the face of the same role and centre
        /// (<see cref="HoleFaceColours.Carry"/>). A colour Inventor refuses is logged and left off; the hole is kept.
        /// </summary>
        /// <param name="seat">The new hole's seat, as its geometry has it.</param>
        public static void ApplyHandColours(
            HoleFeature hole, HandColours colours, HoleSeat seat, IReadOnlyList<HoleVector> centres, ILog log)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (colours == null)
                throw new ArgumentNullException(nameof(colours));
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));
            log ??= NullLog.Instance;
            if (colours.IsEmpty)
                return;

            string? name = ComSafe.Get(() => hole.Name);
            if (colours.FeatureAppearance is { } featureAppearance)
            {
                try
                {
                    hole.Appearance = featureAppearance;
                    log.Info(LogPrefix + $"the hole's own colour {AssetKey(featureAppearance)} put back on {name}; it now reports "
                        + $"appearance source {ComSafe.Get(() => (AppearanceSourceTypeEnum?)hole.AppearanceSourceType)?.ToString() ?? "(unread)"}.");
                }
                catch (Exception ex)
                {
                    log.Warn(LogPrefix + $"the hole's own colour could not be put back on {name}: " + ex.Message);
                }
            }

            if (!HoleFaceColours.AnyColour(colours.Names))
                return;

            (int kept, int total) = ColourFaces(hole, colours, seat, centres, HoleFaceColours.Carry, name, log);
            log.Info(LogPrefix + $"hand colours put back on {kept} of {total} face(s) of {name}.");
        }

        /// <summary>
        /// After <paramref name="hole"/> was changed in place (task 152), gives the faces the change added, those whose
        /// role and centre the hole did not have before, the colour <see cref="HoleFaceColours.CarryToNewFaces"/> picks
        /// from <paramref name="colours"/> (read before the change): the bore's hand colour. Faces that existed keep
        /// what Inventor kept on them, and the feature's own colour is not written again (Inventor keeps it in place).
        /// A colour Inventor refuses is logged and left off.
        /// </summary>
        /// <param name="seat">The hole's seat after the change, as its geometry has it.</param>
        public static void ApplyHandColoursToNewFaces(
            HoleFeature hole, HandColours colours, HoleSeat seat, IReadOnlyList<HoleVector> centres, ILog log)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (colours == null)
                throw new ArgumentNullException(nameof(colours));
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));
            log ??= NullLog.Instance;
            if (!HoleFaceColours.AnyColour(colours.Names))
                return;

            string? name = ComSafe.Get(() => hole.Name);
            (int coloured, int total) = ColourFaces(hole, colours, seat, centres, HoleFaceColours.CarryToNewFaces, name, log);
            log.Info(LogPrefix + $"{coloured} new face(s) of {total} on {name} took the bore's hand colour after the change in place.");
        }

        /// <summary>
        /// Classifies the hole's faces now and puts on each the colour <paramref name="carry"/> picks for it; returns
        /// how many faces took a colour and how many the hole has.
        /// </summary>
        private static (int Coloured, int Total) ColourFaces(
            HoleFeature hole, HandColours colours, HoleSeat seat, IReadOnlyList<HoleVector> centres,
            Func<IReadOnlyList<HoleFaceKey?>, IReadOnlyList<string?>, IReadOnlyList<HoleFaceKey?>, IReadOnlyList<string?>> carry,
            string? name, ILog log)
        {
            var faces = new List<Face>();
            var shapes = new List<HoleFaceShape?>();
            foreach (Face face in FacesOf(hole))
            {
                faces.Add(face);
                shapes.Add(ReadShape(face));
            }

            IReadOnlyList<HoleFaceKey?> keys = HoleFaceColours.Classify(shapes, centres, seat);
            IReadOnlyList<string?> carried = carry(colours.Keys, colours.Names, keys);
            int coloured = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                if (carried[i] is not { } key || !colours.Assets.TryGetValue(key, out Asset? asset))
                    continue;
                try
                {
                    faces[i].Appearance = asset;
                    coloured++;
                }
                catch (Exception ex)
                {
                    log.Warn(LogPrefix + $"a hand colour could not be put on a face of {name}: " + ex.Message);
                }
            }

            return (coloured, faces.Count);
        }

        /// <summary>A hand colour is any appearance that is not an AWB fit appearance or the preview appearance.</summary>
        private static bool IsHandColour(Asset asset)
        {
            string? displayName = ComSafe.Get(() => asset.DisplayName);
            return !HoleFitAppearances.IsFitAppearance(displayName) && !HolePreviewAppearance.Is(displayName);
        }

        /// <summary>The appearance's name in the part, else its display name; null when neither can be read.</summary>
        private static string? AssetKey(Asset asset)
        {
            string? name = ComSafe.Get(() => asset.Name);
            return string.IsNullOrEmpty(name) ? ComSafe.Get(() => asset.DisplayName) : name;
        }

        private static List<Face> FacesOf(HoleFeature hole)
        {
            var list = new List<Face>();
            Faces? faces = ComSafe.Get(() => hole.Faces);
            if (faces != null)
            {
                foreach (Face face in faces)
                    list.Add(face);
            }

            return list;
        }

        /// <summary>
        /// The hole's seat as Inventor reports it; <see cref="HoleFaceColours.Classify"/> gives a spotface the walls and
        /// floor of a counterbore (task 122).
        /// </summary>
        private static HoleSeat SeatOf(HoleFeature hole) =>
            ComSafe.Get(() => (HoleTypeEnum?)hole.HoleType) switch
            {
                HoleTypeEnum.kCounterBoreHole => HoleSeat.Counterbore,
                HoleTypeEnum.kSpotFaceHole => HoleSeat.Spotface,
                HoleTypeEnum.kCounterSinkHole => HoleSeat.Countersink,
                _ => HoleSeat.None,
            };

        /// <summary>The face's surface, axis and middle, in centimetres; null when Inventor cannot report them.</summary>
        private static HoleFaceShape? ReadShape(Face face) =>
            ComSafe.Get(() =>
            {
                Box box = face.Evaluator.RangeBox;
                Point min = box.MinPoint;
                Point max = box.MaxPoint;
                var mid = new HoleVector((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);

                switch (face.SurfaceType)
                {
                    case SurfaceTypeEnum.kCylinderSurface:
                        var cylinder = (Cylinder)face.Geometry;
                        return new HoleFaceShape(
                            HoleFaceSurface.Cylinder, Vector(cylinder.BasePoint), Vector(cylinder.AxisVector), mid, cylinder.Radius);
                    case SurfaceTypeEnum.kConeSurface:
                        var cone = (Cone)face.Geometry;
                        return new HoleFaceShape(HoleFaceSurface.Cone, Vector(cone.BasePoint), Vector(cone.AxisVector), mid, 0);
                    case SurfaceTypeEnum.kPlaneSurface:
                        var plane = (Plane)face.Geometry;
                        return new HoleFaceShape(HoleFaceSurface.Plane, mid, Vector(plane.Normal), mid, 0);
                    default:
                        // No axis: the face belongs to no centre, so it keeps no colour of its own.
                        return new HoleFaceShape(HoleFaceSurface.Other, mid, new HoleVector(0, 0, 0), mid, 0);
                }
            });

        private static HoleVector Vector(Point point) => new HoleVector(point.X, point.Y, point.Z);

        private static HoleVector Vector(UnitVector vector) => new HoleVector(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// The fit's appearance in the part, made the first time (a Generic appearance) and found by its display
        /// name after that. Its colour is set from the workbook each time, so a colour changed in the workbook
        /// reaches the part.
        /// </summary>
        public static Asset FitAppearance(PartDocument doc, PrecisionFit fit, HoleColour colour)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (colour == null)
                throw new ArgumentNullException(nameof(colour));

            string displayName = HoleFitAppearances.DisplayName(fit);
            Asset? asset = null;
            foreach (Asset candidate in doc.Assets)
            {
                if (ComSafe.Get(() => (AssetTypeEnum?)candidate.AssetType) == AssetTypeEnum.kAssetTypeAppearance
                    && HoleFitAppearances.FitFor(ComSafe.Get(() => candidate.DisplayName)) == fit)
                {
                    asset = candidate;
                    break;
                }
            }

            asset ??= doc.Assets.Add(AssetTypeEnum.kAssetTypeAppearance, "Generic", HoleFitAppearances.Name(fit), displayName);

            var diffuse = (ColorAssetValue)asset["generic_diffuse"];
            Color? current = ComSafe.Get(() => diffuse.Value);
            bool same = current != null
                && ComSafe.Get(() => current.Red == colour.R && current.Green == colour.G && current.Blue == colour.B);
            if (!same)
                diffuse.Value = InventorHost.App.TransientObjects.CreateColor(colour.R, colour.G, colour.B);

            return asset;
        }
    }
}
