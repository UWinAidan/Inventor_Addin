using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>
    /// The parts the Material and Finish window changes (spec 03; task 074), and how a choice is applied to them. In a
    /// part document, the part itself. In an assembly, the selected components that are parts, once per file; the rest
    /// are skipped and named in the notes (<see cref="MaterialFinishTargeting"/>, Core).
    /// </summary>
    /// <remarks>
    /// Each <see cref="Apply"/> runs in one transaction through the delegate the command passes, which starts it on the
    /// active document (the assembly, in an assembly; spike 067 section 4) and names it after the command; a failure
    /// aborts it, so nothing is half applied. Nothing is saved.
    /// </remarks>
    public sealed class MaterialFinishTargets : IMaterialFinishTarget
    {
        private readonly MaterialFinishTargetPlan<Document> _plan;
        private readonly MaterialLibrary _library;
        private readonly AppearanceFinder _appearances;
        private readonly Action<Action> _runInTransaction;
        private readonly ILog _log;

        /// <param name="plan">The targets, from <see cref="Find"/>; must hold at least one part.</param>
        /// <param name="library">The opened material library the choice's material is taken from.</param>
        /// <param name="appearances">The appearances, read once for the command.</param>
        /// <param name="runInTransaction">
        /// Runs its argument in one transaction on the active document and aborts it when the argument throws, for
        /// example <c>edit =&gt; RunInTransaction(doc, edit)</c> from a <see cref="DocumentEditCommand"/>.
        /// </param>
        public MaterialFinishTargets(
            MaterialFinishTargetPlan<Document> plan,
            MaterialLibrary library,
            AppearanceFinder appearances,
            Action<Action> runInTransaction,
            ILog? log = null)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _appearances = appearances ?? throw new ArgumentNullException(nameof(appearances));
            _runInTransaction = runInTransaction ?? throw new ArgumentNullException(nameof(runInTransaction));
            _log = log ?? AddinServices.Log;

            if (plan.Parts.Count == 0)
                throw new ArgumentException("The plan holds no part.", nameof(plan));
        }

        /// <summary>The parts a choice is applied to.</summary>
        public IReadOnlyList<MaterialFinishCandidate<Document>> Parts => _plan.Parts;

        /// <summary>
        /// The targets in <paramref name="active"/> (Aidan, 2026-10-12). A part document gives the part, or a stop message
        /// when it cannot be modified. An assembly gives the selected components that are parts (<c>SelectSet</c> is
        /// 1-based; an occurrence inside a sub-assembly is a <c>ComponentOccurrenceProxy</c>, which is also a
        /// <c>ComponentOccurrence</c>, spike 032), each occurrence's <c>Definition.Document</c>, once per file. Anything
        /// else stops with <see cref="MaterialFinishTargeting.WrongDocumentMessage"/>.
        /// </summary>
        /// <param name="active">
        /// The document open in the window, which holds the selection also while a part or sub-assembly is edited in
        /// place (spike 052, task 060).
        /// </param>
        public static MaterialFinishTargetPlan<Document> Find(Document active)
        {
            if (active == null)
                throw new ArgumentNullException(nameof(active));

            DocumentKind kind = DocumentKinds.Of(active);
            if (kind.IsPart())
                return MaterialFinishTargeting.ForPart(PartCandidate(active, FileName(active)));

            if (!kind.IsAssembly())
            {
                return new MaterialFinishTargetPlan<Document>(
                    Array.Empty<MaterialFinishCandidate<Document>>(), Array.Empty<string>(), MaterialFinishTargeting.WrongDocumentMessage);
            }

            var selected = new List<MaterialFinishCandidate<Document>>();
            int other = 0;

            SelectSet? selectSet = ComSafe.Get(() => active.SelectSet);
            int count = (selectSet == null ? null : ComSafe.Get(() => (int?)selectSet.Count)) ?? 0;
            for (int i = 1; selectSet != null && i <= count; i++)
            {
                int index = i;
                if (ComSafe.Get(() => selectSet[index]) is ComponentOccurrence occurrence)
                    selected.Add(OccurrenceCandidate(occurrence));
                else
                    other++;
            }

            MaterialFinishTargetPlan<Document> plan = MaterialFinishTargeting.ForAssembly(selected, other);
            if (plan.StopMessage != null)
                AddinServices.Log.Info($"{MaterialFinishTargeting.LogPrefix}no part to change among {count} selected items.");
            return plan;
        }

        /// <summary>
        /// The window's header for <paramref name="active"/> and the parts in <paramref name="plan"/>: each part's material
        /// display name (<c>ActiveMaterial.DisplayName</c>) and its <c>Finish</c> and <c>Finish Code</c> iProperties.
        /// Core decides the text (<see cref="MaterialFinishCurrent"/>: the file name for a part document, "N parts
        /// selected" and "Varies" in an assembly).
        /// </summary>
        public static MaterialFinishCurrent ReadCurrent(Document active, MaterialFinishTargetPlan<Document> plan)
        {
            if (active == null)
                throw new ArgumentNullException(nameof(active));
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            List<(string? Material, string? FinishName, string? FinishCode)> values =
                plan.Parts.Select(p => ReadValues(p.Item)).ToList();

            if (DocumentKinds.Of(active).IsPart() && values.Count == 1)
                return MaterialFinishCurrent.ForPart(FileName(active), values[0].Material, values[0].FinishName, values[0].FinishCode);

            return MaterialFinishCurrent.ForParts(values);
        }

        /// <summary>
        /// Applies <paramref name="choice"/> to every part in one transaction: <c>ActiveMaterial</c> set to the library
        /// asset (never <c>CopyTo</c>, spike 067), then <c>ActiveAppearance</c> when the finish names an appearance that is
        /// found (setting the material has already reset the appearance to the material's, spike 067), then the four
        /// finish iProperties. A missing or ambiguous appearance gives a note and the part keeps its material's
        /// appearance (Default, spec 03 questions 9 and 10). Any failure aborts the transaction and the result names the
        /// part that failed.
        /// </summary>
        public MaterialFinishResult Apply(MaterialFinishChoice choice)
        {
            if (choice == null)
                throw new ArgumentNullException(nameof(choice));

            var notes = new List<string>(_plan.Notes);

            Asset? material = _library.FindMaterial(choice.MaterialName);
            if (material == null)
            {
                string message = MaterialFinishTargeting.MaterialNotInLibraryMessage(choice.MaterialName);
                _log.Warn(MaterialFinishTargeting.LogPrefix + message);
                return MaterialFinishResult.Failure(message, notes);
            }

            Asset? appearance = null;
            string? appearanceName = MaterialFinishTargeting.AppearanceToFind(choice.Finish);
            if (appearanceName != null)
            {
                AppearanceLookupResult found = _appearances.Find(appearanceName, out appearance);
                if (found.Status == AppearanceLookupStatus.Found && appearance == null)
                    found = AppearanceLookupResult.Missing;

                string? note = MaterialFinishTargeting.AppearanceNote(appearanceName, found, _plan.Parts.Count);
                if (note != null)
                    notes.Add(note);
            }

            _log.Info(MaterialFinishTargeting.ApplyLogLine(_plan.Parts.Select(p => p.Name), choice.MaterialName, choice.Finish));

            string? current = null;
            try
            {
                _runInTransaction(() =>
                {
                    foreach (MaterialFinishCandidate<Document> part in _plan.Parts)
                    {
                        current = part.Name;
                        var partDocument = (PartDocument)part.Item;
                        partDocument.ActiveMaterial = material;
                        if (appearance != null)
                            partDocument.ActiveAppearance = appearance;
                        PropertyWriter.Apply(part.Item, choice.PropertyWrites);
                    }
                });
            }
            catch (Exception ex)
            {
                string message = MaterialFinishTargeting.FailedMessage(current, ex.Message);
                _log.Error(MaterialFinishTargeting.LogPrefix + message, ex);
                LogNotes(notes);
                return MaterialFinishResult.Failure(message, notes);
            }

            LogNotes(notes);
            return MaterialFinishResult.Success(MaterialFinishTargeting.AppliedMessage(_plan.Parts.Count), notes);
        }

        private void LogNotes(IEnumerable<string> notes)
        {
            foreach (string note in notes)
                _log.Info(MaterialFinishTargeting.NoteLogLine(note));
        }

        private static (string? Material, string? FinishName, string? FinishCode) ReadValues(Document doc) =>
        (
            ComSafe.Get(() => ((PartDocument)doc).ActiveMaterial.DisplayName),
            PropertyReader.GetString(doc, PartPropertyStorage.Finish.SetName, PartPropertyStorage.Finish.PropertyName),
            PropertyReader.GetString(doc, PartPropertyStorage.FinishCode.SetName, PartPropertyStorage.FinishCode.PropertyName)
        );

        /// <summary>A part document, kept unless it cannot be modified.</summary>
        private static MaterialFinishCandidate<Document> PartCandidate(Document doc, string name)
        {
            string path = ComSafe.Get(() => doc.FullFileName) ?? string.Empty;
            EditBlock block = path.Length == 0 ? EditBlock.None : PartPropertiesReader.ReadEditBlock(doc, path);
            MaterialFinishSkip skip = block switch
            {
                EditBlock.ReadOnlyFile => MaterialFinishSkip.ReadOnlyFile,
                EditBlock.NotModifiable => MaterialFinishSkip.NotModifiable,
                _ => MaterialFinishSkip.None,
            };
            return new MaterialFinishCandidate<Document>(doc, name, path, skip);
        }

        /// <summary>
        /// A selected occurrence: its document through <c>Definition.Document</c>, as <see cref="AssemblyExtractor"/>
        /// reads it, when it is a part that can be modified; otherwise skipped, named after the occurrence.
        /// </summary>
        private static MaterialFinishCandidate<Document> OccurrenceCandidate(ComponentOccurrence occurrence)
        {
            string occurrenceName = ComSafe.Get(() => occurrence.Name) ?? string.Empty;
            MaterialFinishCandidate<Document> Skipped(MaterialFinishSkip skip) =>
                new MaterialFinishCandidate<Document>(null!, occurrenceName, string.Empty, skip);

            if (ComSafe.Get(() => occurrence.Suppressed))
                return Skipped(MaterialFinishSkip.Suppressed);
            if (ComSafe.Get(() => occurrence.Definition is VirtualComponentDefinition))
                return Skipped(MaterialFinishSkip.Virtual);

            Document? doc = ComSafe.Get(() => (Document)occurrence.Definition.Document);
            if (doc == null)
                return Skipped(MaterialFinishSkip.NotAPart);

            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));
            if (kind.IsAssembly())
                return Skipped(MaterialFinishSkip.SubAssembly);
            if (!kind.IsPart())
                return Skipped(MaterialFinishSkip.NotAPart);

            MaterialFinishCandidate<Document> part = PartCandidate(doc, FileName(doc));
            return part.Skip == MaterialFinishSkip.None ? part : part with { Name = occurrenceName };
        }

        /// <summary>The document's file name, or its display name when it has never been saved.</summary>
        private static string FileName(Document doc)
        {
            string path = ComSafe.Get(() => doc.FullFileName) ?? string.Empty;
            string name = path.Length == 0 ? string.Empty : IOPath.GetFileName(path);
            return name.Length > 0 ? name : ComSafe.Get(() => doc.DisplayName) ?? string.Empty;
        }
    }
}
