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
    /// Reads a part's Finish, Finish Spec, Revision Number, Design Status and User Status and asks Core
    /// (<see cref="PrintPropertyBackfill"/>) for the writes an older part needs (tasks 217 and 219). It only reads; the
    /// caller writes the result inside its own transaction.
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
        /// Every missing print property of <paramref name="doc"/> as writes (Finish Spec, Revision Number, Design Status,
        /// User Status; task 219): empty when it is not a part or nothing is missing. Never throws for a property that
        /// cannot be read.
        /// </summary>
        public static IReadOnlyList<PropertyWrite> WritesFor(Document doc, IReadOnlyList<Finish> finishList)
        {
            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));
            if (!kind.IsPart())
                return Array.Empty<PropertyWrite>();

            return PrintPropertyBackfill.For(
                Read(doc, PartPropertyStorage.Finish),
                Read(doc, PartPropertyStorage.FinishThickness),
                Read(doc, PartPropertyStorage.FinishSpec),
                Read(doc, PartPropertyStorage.RevisionNumber),
                ReadDesignStatus(doc),
                Read(doc, PartPropertyStorage.UserStatus),
                finishList,
                MaterialFinishTargets.UnitsOf(doc));
        }

        /// <summary>Design Status as a number, or null when it cannot be read.</summary>
        private static int? ReadDesignStatus(Document doc)
        {
            object? value = PropertyReader.Get(
                doc, PartPropertyStorage.DesignStatus.SetName, PartPropertyStorage.DesignStatus.PropertyName);
            if (value == null)
                return null;

            try
            {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? Read(Document doc, PropertyLocation location) =>
            PropertyReader.GetString(doc, location.SetName, location.PropertyName);
    }
}
