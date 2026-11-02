using System;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Paints the preview hole in translucent red while the AWB Hole window is open (task 101; spec 09, "Live 3D
    /// preview"). The session calls it inside its preview transaction and aborts that transaction before every other
    /// change and on every way out, so the abort puts every face back exactly as it was (a user's colour on an edited
    /// hole included) and removes the preview appearance from the part (task 101's spike). Nothing here opens a
    /// transaction; a failure throws, so the caller can abort.
    /// </summary>
    public static class HolePreviewWriter
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        /// <summary>Gives every face of the hole, the bottom included, the preview appearance.</summary>
        public static void Paint(PartDocument doc, HoleFeature hole, ILog log)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            log ??= NullLog.Instance;

            Asset appearance = PreviewAppearance(doc, log);
            foreach (Face face in hole.Faces)
                face.Appearance = appearance;
        }

        /// <summary>
        /// The preview appearance in the part, made the first time in the preview transaction (a Generic appearance)
        /// and found by its display name otherwise, with the preview colour and transparency from Core. A
        /// transparency Inventor refuses is logged and the preview stays plain red.
        /// </summary>
        private static Asset PreviewAppearance(PartDocument doc, ILog log)
        {
            Asset? asset = null;
            foreach (Asset candidate in doc.Assets)
            {
                if (ComSafe.Get(() => (AssetTypeEnum?)candidate.AssetType) == AssetTypeEnum.kAssetTypeAppearance
                    && HolePreviewAppearance.Is(ComSafe.Get(() => candidate.DisplayName)))
                {
                    asset = candidate;
                    break;
                }
            }

            asset ??= doc.Assets.Add(
                AssetTypeEnum.kAssetTypeAppearance, "Generic", HolePreviewAppearance.Name, HolePreviewAppearance.DisplayName);

            HoleColour colour = HolePreviewAppearance.Colour;
            var diffuse = (ColorAssetValue)asset[HolePreviewAppearance.ColourValueName];
            diffuse.Value = InventorHost.App.TransientObjects.CreateColor(colour.R, colour.G, colour.B);

            Asset found = asset;
            try
            {
                var transparency = (FloatAssetValue)found[HolePreviewAppearance.TransparencyValueName];
                transparency.Value = HolePreviewAppearance.Transparency(HolePreviewAppearance.Opacity);
            }
            catch (Exception ex)
            {
                log.Warn(LogPrefix + $"the preview colour's transparency could not be set ({ex.Message}); it is drawn opaque.");
            }

            return asset;
        }
    }
}
