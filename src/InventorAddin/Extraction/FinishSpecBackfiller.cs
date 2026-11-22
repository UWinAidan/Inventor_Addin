using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using SysFile = System.IO.File;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads a part's Finish, Finish Thickness and Finish Spec and asks Core (<see cref="FinishSpecBackfill"/>) for the
    /// Finish Spec write a part finished before task 211 needs (task 217). It only reads; the caller writes the result
    /// inside its own transaction.
    /// </summary>
    internal static class FinishSpecBackfiller
    {
        /// <summary>
        /// The finishes in the finish list set in Settings (or the starter list). Never throws: a list that is missing or
        /// cannot be read gives an empty list, so a part's spec is built from its stored Finish and Finish Thickness; the
        /// reason is logged.
        /// </summary>
        public static IReadOnlyList<Finish> LoadFinishList()
        {
            try
            {
                LibraryPath path = LibraryPaths.FinishList(AddinServices.Settings, AddinServices.AddinFolder);
                return FinishListCsv.Parse(SysFile.ReadAllText(path.Path)).Finishes;
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn($"Finish Spec backfill: the finish list could not be read, so stored values are used. {ex.Message}");
                return Array.Empty<Finish>();
            }
        }

        /// <summary>
        /// The Finish Spec write for <paramref name="doc"/>, or null when it is not a part, has no Finish or already has a
        /// Finish Spec. Never throws for a property that cannot be read.
        /// </summary>
        public static PropertyWrite? WriteFor(Document doc, IReadOnlyList<Finish> finishList)
        {
            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));
            if (!kind.IsPart())
                return null;

            return FinishSpecBackfill.For(
                Read(doc, PartPropertyStorage.Finish),
                Read(doc, PartPropertyStorage.FinishThickness),
                Read(doc, PartPropertyStorage.FinishSpec),
                finishList,
                MaterialFinishTargets.UnitsOf(doc));
        }

        private static string? Read(Document doc, PropertyLocation location) =>
            PropertyReader.GetString(doc, location.SetName, location.PropertyName);
    }
}
