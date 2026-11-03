using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Draws the AWB Hole preview with client graphics (task 110; spec 09, "Live 3D preview"; spike 107): a faint,
    /// see-through red copy of the hole volume drawn over the model, end caps included. No face is painted. Core's
    /// <see cref="HolePreviewSolids"/> works out a solid of revolution for each cylindrical and conical face of the
    /// hole; they are unioned into one body, drawn as one <c>SurfaceGraphics</c> on the part's
    /// <c>ClientGraphicsCollection</c>, made with <c>AddNonTransacting</c> so nothing reaches the Undo list. The
    /// graphics are drawn on the part only, with no occurrence transform: they land on the part when it is edited in
    /// place too (spike 103 question 8). Nothing here opens a transaction.
    /// </summary>
    public static class HolePreviewWriter
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        /// <summary>
        /// Draws the preview of <paramref name="hole"/> on <paramref name="doc"/>, replacing any drawn before. Returns
        /// how many solids were drawn. A solid Inventor refuses is logged and left out; a failure after the graphics
        /// were made removes them and throws.
        /// </summary>
        public static int Draw(Application app, PartDocument doc, HoleFeature hole, ILog log)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            log ??= NullLog.Instance;

            Clear(doc);
            IReadOnlyList<HolePreviewSolid> solids = HolePreviewSolids.For(HolePreviewFaceReader.Read(hole, log));
            List<SurfaceBody> bodies = Bodies(app, solids, log);
            if (bodies.Count == 0)
                return 0;

            ClientGraphics graphics = doc.ComponentDefinition.ClientGraphicsCollection.AddNonTransacting(HolePreviewGraphics.ClientId);
            try
            {
                GraphicsNode node = graphics.AddNode(1);
                SurfaceBody body = bodies[0];
                for (int i = 1; i < bodies.Count; i++)
                {
                    try
                    {
                        app.TransientBRep.DoBoolean(body, bodies[i], BooleanTypeEnum.kBooleanTypeUnion);
                    }
                    catch (Exception ex)
                    {
                        // Drawn on its own: overlapping see-through solids are darker where they meet, but still drawn.
                        log.Warn(LogPrefix + $"a preview solid could not be joined to the others ({ex.Message}); it is drawn on its own.");
                        AddSurface(app, node, bodies[i]);
                    }
                }

                AddSurface(app, node, body);
                node.OverrideOpacity = HolePreviewGraphics.Opacity;
            }
            catch
            {
                ComSafe.Run(() => graphics.Delete());
                throw;
            }

            ComSafe.Run(() => app.ActiveView.Update());
            return bodies.Count;
        }

        /// <summary>Removes the preview graphics from the part, if any. Never throws.</summary>
        public static void Clear(PartDocument doc)
        {
            if (doc == null)
                return;
            ClientGraphicsCollection? collection = ComSafe.Get(() => doc.ComponentDefinition.ClientGraphicsCollection);
            if (collection == null)
                return;
            ClientGraphics? graphics = ComSafe.Get(() => collection[HolePreviewGraphics.ClientId]);
            if (graphics != null)
                ComSafe.Run(() => graphics.Delete());
        }

        /// <summary>A transient solid for each of Core's solids; one Inventor refuses is logged and left out.</summary>
        private static List<SurfaceBody> Bodies(Application app, IReadOnlyList<HolePreviewSolid> solids, ILog log)
        {
            var bodies = new List<SurfaceBody>();
            TransientGeometry geometry = app.TransientGeometry;
            foreach (HolePreviewSolid solid in solids)
            {
                try
                {
                    Point bottom = geometry.CreatePoint(solid.Bottom.X, solid.Bottom.Y, solid.Bottom.Z);
                    Point top = geometry.CreatePoint(solid.Top.X, solid.Top.Y, solid.Top.Z);
                    bodies.Add(app.TransientBRep.CreateSolidCylinderCone(
                        bottom, top, solid.BottomRadiusCm, solid.BottomRadiusCm, solid.TopRadiusCm, Type.Missing));
                }
                catch (Exception ex)
                {
                    log.Warn(LogPrefix + $"a preview solid could not be made ({ex.Message}); it is not drawn.");
                }
            }

            return bodies;
        }

        private static void AddSurface(Application app, GraphicsNode node, SurfaceBody body)
        {
            SurfaceGraphics surface = node.AddSurfaceGraphics(body);
            HoleColour colour = HolePreviewGraphics.Colour;
            surface.Color = app.TransientObjects.CreateColor(colour.R, colour.G, colour.B, HolePreviewGraphics.Opacity);
            surface.BurnThrough = HolePreviewGraphics.BurnThrough;
            surface.DepthPriority = HolePreviewGraphics.DepthPriority;
        }
    }
}
