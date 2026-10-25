using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Save As and Replace (spec 02, "Commands: Save As and Replace"): with one component selected in the active
    /// assembly, copies that component's file under the next part number and a new name, as Save As Copy does, and
    /// replaces only the selected occurrence with the copy (Default, spec 02 question 16). The steps, their order and the
    /// messages are in <see cref="SaveAsReplacer"/> (Core); this class reads the selection, shows the Save As Copy dialog
    /// (<see cref="SaveAsCopyViewModel.ForReplace"/>) and supplies Inventor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A top-level occurrence is replaced in one transaction on the assembly, so one Undo puts the original back; the
    /// copy's file and its register row stay. An occurrence inside a sub-assembly (a <c>ComponentOccurrenceProxy</c>,
    /// spike 032) is replaced in the sub-assembly, which is then saved (Aidan, spec 02 question 25); the dialog warns
    /// that this cannot be undone.
    /// </para>
    /// <para>
    /// The copy is opened invisibly to write its properties and closed again before the replace; it is not opened in
    /// its own window. The command edits the assembly's structure, so its command type is <c>kShapeEditCmdType</c>.
    /// </para>
    /// </remarks>
    public sealed class SaveAsReplaceCommand : DocumentEditCommand
    {
        public override string InternalName => CommandNames.SaveAsReplace;
        public override string DisplayName => "Save As and Replace";
        public override string Description =>
            "Copy the selected component under the next part number and a new name, and replace it with the copy.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kShapeEditCmdType;

        // The document is the top-level assembly open in the window, by this command's row in CommandDocuments (TopLevel,
        // task 109), also while a sub-assembly is edited in place: the selection is held by the top-level document then,
        // and the sub-assembly's own SelectSet is empty (spike 052, task 060). The kind check, the selection, the
        // transaction and the replace all use it (DocumentEditCommand.DocumentToEdit).

        protected override bool CanRunOn(DocumentKind kind) => kind.IsAssembly();

        protected override string WrongDocumentMessage(DocumentKind kind) => SaveAsReplacer.WrongDocumentMessage;

        // Run is overridden: the dialog and the copy stay outside any transaction, and only the replace runs in one.
        protected override void Execute(Document doc) =>
            throw new NotSupportedException("Save As and Replace runs through Run.");

        protected override void Run(Document doc)
        {
            ComponentOccurrence? occurrence = ReadSelectedOccurrence(doc);
            if (occurrence == null)
            {
                Show(SaveAsReplacer.SelectOneMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            SelectedComponent? selected = ReadSelection(doc, occurrence);
            if (selected == null)
            {
                Show(SaveAsReplacer.SubAssemblyNotReadMessage, Forms.MessageBoxIcon.Warning);
                return;
            }

            ReplaceSelection selection = selected.Selection;
            string? problem = SaveAsReplacer.Check(selection);
            if (problem != null)
            {
                AddinServices.Log.Info($"Save As and Replace stopped before the dialog: {problem}");
                Show(problem, Forms.MessageBoxIcon.Information);
                return;
            }

            string? partType = PropertyReader.GetString(
                selected.Component!, PartPropertyStorage.PartType.SetName, PartPropertyStorage.PartType.PropertyName);

            AddinSettings settings = AddinServices.Settings;
            SaveAsCopyViewModel viewModel = SaveAsCopyViewModel.ForReplace(
                selection.ComponentFullFileName,
                selection.ComponentKind,
                partType,
                settings.DefaultDesigner,
                SaveAsReplacer.SubAssemblyWarning(selection));
            WindowHost.ShowDialog(new SaveAsCopyWindow(viewModel), IconNames.SaveAsReplace);

            SaveAsCopyRequest? request = viewModel.Request;
            if (request == null)
                return;

            var target = new InventorReplaceTarget(this, doc, occurrence, selected.Component!, selected.SubAssembly);
            var replacer = new SaveAsReplacer(
                AddinServices.RegisterStore, target, selection, viewModel.Designer, AddinServices.Log);

            NewPartResult result;

            // Saving the copy, the replace and saving a sub-assembly can take a while, and the register may wait on the NAS.
            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                result = replacer.Replace(request);
            }
            catch (NewPartException ex)
            {
                // A failure after the number was assigned is already logged with its exception in Core.
                if (ex.AssignedNumber == null)
                    AddinServices.Log.Warn($"Save As and Replace stopped: {ex.Message}");

                Forms.Cursor.Current = previousCursor;
                Show(ex.Message, ex.AssignedNumber == null ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }

            string? notice = NewPartCreator.Notice(result);
            if (notice != null)
                Show(notice, result.DuplicateMessages.Count > 0 ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        }

        /// <summary>
        /// The one selected component occurrence (spike 032: <c>SelectSet</c> is 1-based; a
        /// <c>ComponentOccurrenceProxy</c> inside a sub-assembly is also a <c>ComponentOccurrence</c>), or null when
        /// nothing, more than one thing, or something other than an occurrence is selected. A null is logged with the
        /// count and each item's type, so a report can be diagnosed from the log (task 060).
        /// </summary>
        private static ComponentOccurrence? ReadSelectedOccurrence(Document doc)
        {
            SelectSet? selectSet = ComSafe.Get(() => doc.SelectSet);
            int count = (selectSet == null ? null : ComSafe.Get(() => (int?)selectSet.Count)) ?? -1;

            if (selectSet != null && count == 1)
            {
                if (ComSafe.Get(() => selectSet[1]) is ComponentOccurrence occurrence)
                    return occurrence;
            }

            var types = new List<string?>();
            for (int i = 1; selectSet != null && i <= count; i++)
            {
                int index = i;
                object? item = ComSafe.Get(() => selectSet[index]);

                // The COM type name (for example ComponentOccurrenceProxy), read without dynamic.
                types.Add(item == null ? null : Microsoft.VisualBasic.Information.TypeName(item));
            }

            AddinServices.Log.Info(SaveAsReplacer.SelectionNotOneComponentLog(count, types));
            return null;
        }

        /// <summary>
        /// Reads what <see cref="SaveAsReplacer.Check"/> needs. Null only when the occurrence is inside a sub-assembly
        /// and that sub-assembly's document could not be reached.
        /// </summary>
        private static SelectedComponent? ReadSelection(Document assembly, ComponentOccurrence occurrence)
        {
            // Inside a sub-assembly the occurrence the replace edits lives in the sub-assembly (spike 032). Its native
            // occurrence's parent definition is the sub-assembly's; that document is the one saved afterwards.
            ComponentOccurrenceProxy? proxy = occurrence as ComponentOccurrenceProxy;
            ComponentOccurrence native = occurrence;
            Document? subAssembly = null;
            string? subAssemblyPath = null;
            string? subAssemblyOccurrence = null;
            EditBlock changedBlock;

            if (proxy != null)
            {
                ComponentOccurrence? nativeOccurrence = ComSafe.Get(() => (ComponentOccurrence)proxy.NativeObject);
                subAssembly = nativeOccurrence == null
                    ? null
                    : ComSafe.Get(() => (Document)((AssemblyComponentDefinition)nativeOccurrence.Parent).Document);
                if (nativeOccurrence == null || subAssembly == null)
                {
                    AddinServices.Log.Warn("Save As and Replace: the sub-assembly holding the selected component could not be read.");
                    return null;
                }

                native = nativeOccurrence;
                subAssemblyPath = ComSafe.Get(() => subAssembly.FullFileName) ?? string.Empty;
                subAssemblyOccurrence = ComSafe.Get(() => proxy.ParentOccurrence.Name);
                changedBlock = PartPropertiesReader.ReadEditBlock(subAssembly, subAssemblyPath);
            }
            else
            {
                string assemblyPath = ComSafe.Get(() => assembly.FullFileName) ?? string.Empty;
                changedBlock = PartPropertiesReader.ReadEditBlock(assembly, assemblyPath);
            }

            bool suppressed = ComSafe.Get(() => native.Suppressed);
            bool isVirtual = !suppressed && ComSafe.Get(() => native.Definition is VirtualComponentDefinition);

            // The same read as AssemblyExtractor: the component's document through its definition.
            Document? component = suppressed || isVirtual ? null : ComSafe.Get(() => (Document)native.Definition.Document);
            string componentPath = component == null ? string.Empty : ComSafe.Get(() => component.FullFileName) ?? string.Empty;
            DocumentKind componentKind = component == null ? DocumentKind.Unknown : ComSafe.Get(() => DocumentKinds.Of(component));
            EditBlock componentBlock = component == null || componentPath.Length == 0
                ? EditBlock.None
                : PartPropertiesReader.ReadEditBlock(component, componentPath);

            var selection = new ReplaceSelection(
                componentPath,
                componentKind,
                componentBlock,
                changedBlock,
                IsVirtual: isVirtual,
                SubAssemblyFullFileName: subAssemblyPath,
                SubAssemblyOccurrenceName: subAssemblyOccurrence,
                IsSuppressed: suppressed);

            return new SelectedComponent(selection, component, subAssembly);
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);

        /// <summary>What was read from the selection, with the documents the target needs.</summary>
        private sealed class SelectedComponent
        {
            public SelectedComponent(ReplaceSelection selection, Document? component, Document? subAssembly)
            {
                Selection = selection;
                Component = component;
                SubAssembly = subAssembly;
            }

            public ReplaceSelection Selection { get; }

            /// <summary>The component's document; set whenever <see cref="SaveAsReplacer.Check"/> passes.</summary>
            public Document? Component { get; }

            /// <summary>The sub-assembly that holds the occurrence; null for a top-level occurrence.</summary>
            public Document? SubAssembly { get; }
        }

        /// <summary>
        /// The selected occurrence and its component. The copy is made as Save As Copy makes it
        /// (<c>SaveAs(path, true)</c>, task 045), but opened invisibly and closed before the replace. The replace is
        /// <c>Replace(path, false)</c> on the selected occurrence only (spike 032: constraints are kept), in one
        /// transaction on the active assembly.
        /// </summary>
        private sealed class InventorReplaceTarget : ISaveAsReplaceTarget
        {
            private readonly SaveAsReplaceCommand _command;
            private readonly Document _assembly;
            private readonly ComponentOccurrence _occurrence;
            private readonly Document _component;
            private readonly Document? _subAssembly;
            private Document? _copy;

            public InventorReplaceTarget(
                SaveAsReplaceCommand command,
                Document assembly,
                ComponentOccurrence occurrence,
                Document component,
                Document? subAssembly)
            {
                _command = command;
                _assembly = assembly;
                _occurrence = occurrence;
                _component = component;
                _subAssembly = subAssembly;
            }

            // SaveCopyAs = true: writes the file, with the component's unsaved edits; the component keeps its name.
            public void SaveCopyAs(string fullPath) => _component.SaveAs(fullPath, true);

            public ICopiedDocument OpenCopy(string fullPath)
            {
                // Invisible (spike 032, question 5): the copy does not open in its own window.
                _copy = InventorHost.App.Documents.Open(fullPath, false);
                return new InventorNewDocument(_copy, _command.DisplayName);
            }

            public void CloseCopy()
            {
                // Close(true) skips saving (spike 032); the copy has just been saved.
                _copy?.Close(true);
                _copy = null;
            }

            // A proxy's Replace edits the sub-assembly (spike 032). The transaction is on the top-level assembly
            // (ActiveDocument) either way, also while a sub-assembly is edited in place (task 060); a failure aborts it.
            public void ReplaceOccurrence(string copyFullPath) =>
                _command.RunInTransaction(_assembly, () => _occurrence.Replace(copyFullPath, false));

            public void SaveSubAssembly()
            {
                if (_subAssembly == null)
                    throw new InvalidOperationException("No sub-assembly to save.");
                _subAssembly.Save();
            }
        }
    }
}
