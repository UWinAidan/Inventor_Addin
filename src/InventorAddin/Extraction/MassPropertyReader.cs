using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads a part's or assembly's mass and asks Core (<see cref="MassText"/>) for the write that brings the custom "Mass"
    /// property up to date (task 222). It only reads; the caller writes the result.
    /// </summary>
    internal static class MassPropertyReader
    {
        /// <summary>
        /// The Mass write for a part or assembly: null when <paramref name="doc"/> is another kind of document, the mass cannot be
        /// read, or the stored text is already right. Never throws. Inventor's mass is in kilograms; the text is in lb for an
        /// imperial document (<see cref="MaterialFinishTargets.UnitsOf"/>).
        /// </summary>
        public static PropertyWrite? WriteFor(Document doc)
        {
            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));
            double? massKg = kind.IsPart()
                ? ComSafe.Get(() => (double?)((PartDocument)doc).ComponentDefinition.MassProperties.Mass)
                : kind.IsAssembly()
                    ? ComSafe.Get(() => (double?)((AssemblyDocument)doc).ComponentDefinition.MassProperties.Mass)
                    : null;
            if (massKg == null)
                return null;

            string? stored = PropertyReader.GetString(doc, PartPropertyStorage.Mass.SetName, PartPropertyStorage.Mass.PropertyName);
            return MassText.WriteFor(massKg, MaterialFinishTargets.UnitsOf(doc), stored);
        }
    }
}
