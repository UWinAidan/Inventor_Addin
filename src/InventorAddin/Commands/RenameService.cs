using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;
using IOFile = System.IO.File;
using RegisterPartNumber = InventorAddin.Core.Numbering.PartNumber;
using WpfCursor = System.Windows.Input.Cursor;
using WpfCursors = System.Windows.Input.Cursors;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfMouse = System.Windows.Input.Mouse;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Rename for the Part Properties window (spec 02, "Commands: Rename"; task 047). Shows the Rename window and, when
    /// the user confirms, renames the open file by spike 032's method through <see cref="PartRenamer"/> (Core), which
    /// decides the steps, their order and the result list. This class supplies Inventor and the dialogs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before anything changes: the new name is checked as it is typed (a file that exists, or a document open under that
    /// name, is refused); open documents that use the file and have unsaved changes stop the rename, since it would save
    /// them (Default); the CAD folders from Settings are listed off the UI thread with a timeout
    /// (<see cref="RenamePlan.ListFilesToSearch"/>); and the user confirms, told how many files will be searched and that
    /// Undo cannot revert it.
    /// </para>
    /// <para>
    /// Rename saves files, so it is not one Undo step. The Description write uses Part Properties' write target, so it is
    /// a transaction of its own, then the document is saved.
    /// </para>
    /// </remarks>
    internal sealed class RenameService : IPartRenamer
    {
        private const string Title = "Rename";

        private readonly Document _doc;
        private readonly IPropertyWriteTarget _writeTarget;
        private RenameWindow? _window;

        // The file's Part Number when the register holds it, read when Rename opens. The new file name keeps it (spec 02),
        // and its register entry is updated.
        private RegisterPartNumber? _registeredNumber;

        /// <param name="doc">Part Properties' document: the file to rename. It has been saved.</param>
        /// <param name="writeTarget">Part Properties' write target, which writes inside a transaction.</param>
        public RenameService(Document doc, IPropertyWriteTarget writeTarget)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _writeTarget = writeTarget ?? throw new ArgumentNullException(nameof(writeTarget));
        }

        public PartRenameOutcome? Rename()
        {
            string oldPath = ComSafe.Get(() => _doc.FullFileName) ?? string.Empty;
            if (oldPath.Trim().Length == 0)
            {
                WpfMessageBox.Show(RenamePlan.NotSavedMessage, Title, WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
                return null;
            }

            // The Part Number as it is now (Generate may have written it since Part Properties opened), checked against
            // this PC's copy of the register, as Part Properties does.
            string? partNumber = PropertyReader.GetString(
                _doc, PartPropertyStorage.PartNumber.SetName, PartPropertyStorage.PartNumber.PropertyName);
            _registeredNumber =
                RegisterPartNumber.TryParse(partNumber, out RegisterPartNumber number) && AddinServices.RegisterStore.HasNumber(partNumber)
                    ? number
                    : null;

            var viewModel = new RenameViewModel(
                oldPath, TargetExists, Run, AddinServices.Log, _registeredNumber, ComSafe.Get(() => DocumentKinds.Of(_doc)));
            _window = new RenameWindow(viewModel);
            try
            {
                WindowHost.ShowDialog(_window, IconNames.PartProperties);
            }
            finally
            {
                _window = null;
            }

            return viewModel.Outcome;
        }

        /// <summary>A file on disk, or a document open in Inventor, already has this name.</summary>
        private static bool TargetExists(string fullFileName) =>
            IOFile.Exists(fullFileName) || FindOpen(fullFileName) != null;

        /// <summary>The runner the view-model calls: checks, lists, confirms, then renames. Null when not confirmed.</summary>
        private RenameResult? Run(RenameRequest request, Action<string> progress)
        {
            List<string> unsaved = OpenDocumentsWithUnsavedChangesUsing(request.OldFullFileName);
            if (unsaved.Count > 0)
            {
                AddinServices.Log.Info($"Rename of '{request.OldFullFileName}' stopped: {unsaved.Count} open files that use it have unsaved changes.");
                ShowMessage(RenamePlan.UnsavedOpenFilesMessage(unsaved), WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
                return null;
            }

            AddinSettings settings = AddinServices.Settings;
            IReadOnlyList<string> folders = RenamePlan.SearchFolders(
                settings.WorkFolder, settings.OtherCadFolders, request.OldFullFileName);

            RenameSearchList search;
            WpfCursor? previousCursor = WpfMouse.OverrideCursor;
            WpfMouse.OverrideCursor = WpfCursors.Wait;
            try
            {
                progress(RenameViewModel.StartingText);
                PumpUi();
                search = RenamePlan.ListFilesToSearch(folders);
            }
            finally
            {
                WpfMouse.OverrideCursor = previousCursor;
            }

            foreach (SkippedFolder skipped in search.Skipped)
                AddinServices.Log.Warn($"Rename: '{skipped.Folder}' not searched: {skipped.Reason}.");

            if (ShowMessage(RenamePlan.ConfirmationMessage(request, search), WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Question)
                != WpfMessageBoxResult.Yes)
            {
                return null;
            }

            // The register entry is updated only for a registered number.
            var renamer = new PartRenamer(
                new InventorRenameTarget(_doc, _writeTarget), AddinServices.RegisterStore, AddinServices.Log);

            previousCursor = WpfMouse.OverrideCursor;
            WpfMouse.OverrideCursor = WpfCursors.Wait;
            try
            {
                return renamer.Run(
                    request,
                    search.Files,
                    _registeredNumber,
                    text =>
                    {
                        progress(text);
                        PumpUi();
                    },
                    search.Skipped);
            }
            finally
            {
                WpfMouse.OverrideCursor = previousCursor;
            }
        }

        private WpfMessageBoxResult ShowMessage(string text, WpfMessageBoxButton buttons, WpfMessageBoxImage image) =>
            _window != null
                ? WpfMessageBox.Show(_window, text, Title, buttons, image)
                : WpfMessageBox.Show(text, Title, buttons, image);

        /// <summary>
        /// Lets WPF draw the progress line while the rename runs on the UI thread (the COM calls must stay there). Input
        /// is processed too, which is why the window cannot be closed while running.
        /// </summary>
        private static void PumpUi() =>
            WpfDispatcher.CurrentDispatcher.Invoke(WpfDispatcherPriority.Background, new Action(() => { }));

        /// <summary>
        /// The open documents, other than the renamed one, that reference <paramref name="fullFileName"/> and have
        /// unsaved changes. A document never saved is left out: it is repointed in memory and never saved by Rename.
        /// </summary>
        private List<string> OpenDocumentsWithUnsavedChangesUsing(string fullFileName)
        {
            var found = new List<string>();
            foreach (Document d in OpenDocuments())
            {
                string path = ComSafe.Get(() => d.FullFileName) ?? string.Empty;
                if (path.Length == 0 || SamePath(path, fullFileName))
                    continue;

                if (ComSafe.Get(() => d.Dirty) && References(d, fullFileName))
                    found.Add(path);
            }

            return found;
        }

        /// <summary>Every document Inventor holds, visible or not (spike 032, question 5).</summary>
        private static List<Document> OpenDocuments()
        {
            var documents = new List<Document>();
            foreach (Document d in InventorHost.App.Documents)
                documents.Add(d);
            return documents;
        }

        /// <summary>
        /// The document Inventor holds under <paramref name="fullFileName"/>, ignoring case, or null. A loop, because
        /// <c>Documents.get_ItemByName</c> throws for a file that is not open (spike 032).
        /// </summary>
        private static Document? FindOpen(string fullFileName)
        {
            foreach (Document d in OpenDocuments())
            {
                if (SamePath(ComSafe.Get(() => d.FullFileName), fullFileName))
                    return d;
            }

            return null;
        }

        /// <summary>
        /// True when one of <paramref name="doc"/>'s direct references is <paramref name="fullFileName"/>; false when its
        /// references cannot be read (<see cref="TryReferences"/> says which).
        /// </summary>
        private static bool References(Document doc, string fullFileName) =>
            TryReferences(doc, fullFileName, out _) == true;

        /// <summary>
        /// True when one of <paramref name="doc"/>'s direct references is <paramref name="fullFileName"/>, false when none
        /// is, and null when its references cannot be read, with the reason in <paramref name="error"/>.
        /// </summary>
        private static bool? TryReferences(Document doc, string fullFileName, out string? error)
        {
            error = null;
            try
            {
                foreach (FileDescriptor fd in doc.File.ReferencedFileDescriptors)
                {
                    if (SamePath(ComSafe.Get(() => fd.FullFileName), fullFileName))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static bool SamePath(string? a, string? b) =>
            !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when <paramref name="a"/> and <paramref name="b"/> are the same Inventor document: Inventor reports the
        /// same <c>FullFileName</c> for both, whatever spelling of the path was used to open it.
        /// </summary>
        private static bool SameDocument(Document a, Document b) =>
            SamePath(ComSafe.Get(() => a.FullFileName), ComSafe.Get(() => b.FullFileName));

        /// <summary>
        /// Spikes 032 (question 4) and 052 (question 1): <c>SaveAs(newPath, false)</c> on the open document, which
        /// repoints open documents in memory without marking them modified; then, in each file that uses it, open or
        /// opened invisibly with <c>SilentOperation</c>, <c>ReplaceReference</c> through
        /// <c>Document.File.ReferencedFileDescriptors</c> where it still names the old path, the occurrence renames, then
        /// <c>Dirty = true</c> and <c>Save</c>, because <c>Save</c> writes nothing for a document that is not dirty.
        /// </summary>
        private sealed class InventorRenameTarget : IRenameTarget
        {
            private readonly Document _doc;
            private readonly IPropertyWriteTarget _writeTarget;

            public InventorRenameTarget(Document doc, IPropertyWriteTarget writeTarget)
            {
                _doc = doc;
                _writeTarget = writeTarget;
            }

            // false: save under the new name and keep working on it (not a copy). The old file stays on disk.
            public void SaveAs(string newFullFileName) => _doc.SaveAs(newFullFileName, false);

            public RenameFileUpdate? UpdateReferences(string fullFileName, string oldFullFileName, string newFullFileName)
            {
                // ReplaceReference works on an open document too (spike 032); an open one is saved, not closed.
                Document? open = FindOpen(fullFileName);
                if (open != null)
                    return UpdateAndSave(open, oldFullFileName, newFullFileName);

                // FindOpen can miss a document that is open under another spelling of its path (a mapped drive and
                // its UNC path); Documents.Open then returns that open document. Record what is open first, so such a
                // document is updated and saved but not closed.
                List<Document> openBefore = OpenDocuments();

                // SilentOperation only around the invisible open, as spike 032 did.
                var app = InventorHost.App;
                bool previousSilent = app.SilentOperation;
                Document doc;
                app.SilentOperation = true;
                try
                {
                    doc = app.Documents.Open(fullFileName, false);
                }
                finally
                {
                    app.SilentOperation = previousSilent;
                }

                bool wasOpen = openBefore.Exists(d => ReferenceEquals(d, doc) || SameDocument(d, doc));
                if (wasOpen)
                    AddinServices.Log.Info($"Rename: '{fullFileName}' was already open; it is saved and left open.");

                try
                {
                    return UpdateAndSave(doc, oldFullFileName, newFullFileName);
                }
                finally
                {
                    // Close(true) skips saving (spike 032): a failed update is not written. A document the user
                    // already had open stays open.
                    if (!wasOpen)
                        ComSafe.Run(() => doc.Close(true));
                }
            }

            public IReadOnlyList<RenameFileResult> SaveRepointedDocuments(
                string oldFullFileName,
                string newFullFileName,
                IReadOnlyCollection<string> alreadyHandled)
            {
                var handled = new HashSet<string>(alreadyHandled, StringComparer.OrdinalIgnoreCase);
                var results = new List<RenameFileResult>();
                foreach (Document d in OpenDocuments())
                {
                    string path = ComSafe.Get(() => d.FullFileName) ?? string.Empty;

                    // A never-saved document is repointed in memory and keeps the new name when it is saved. The renamed
                    // document is skipped, as is each file the search already handled.
                    if (path.Length == 0 || SamePath(path, newFullFileName) || SamePath(path, oldFullFileName)
                        || handled.Contains(path))
                        continue;

                    // Not dirty is expected here (spike 052): UpdateAndSave marks it before saving.
                    if (!References(d, newFullFileName) && !References(d, oldFullFileName))
                        continue;

                    try
                    {
                        RenameFileUpdate? update = UpdateAndSave(d, oldFullFileName, newFullFileName);
                        if (update != null)
                            results.Add(new RenameFileResult(path, null, update.OccurrencesRenamed, update.OccurrenceProblems));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new RenameFileResult(path, ex.Message));
                    }
                }

                return results;
            }

            public IReadOnlyList<string> OpenDocumentsReferencing(string oldFullFileName)
            {
                var found = new List<string>();
                foreach (Document d in OpenDocuments())
                {
                    string path = ComSafe.Get(() => d.FullFileName) ?? string.Empty;
                    if (path.Length == 0 || SamePath(path, oldFullFileName))
                        continue;

                    // Fail safe: a document whose references cannot be read counts as still using the old file, so
                    // the old file is kept.
                    bool? uses = TryReferences(d, oldFullFileName, out string? error);
                    if (uses == null)
                        AddinServices.Log.Warn(
                            $"Rename: the references of open file '{path}' could not be read ({error}); it counts as still using '{oldFullFileName}'.");

                    if (uses != false)
                        found.Add(path);
                }

                return found;
            }

            public void WriteDescription(string partName)
            {
                _writeTarget.Apply(new[] { PropertyWrite.SetText(PartPropertyStorage.Description, partName) });
                _doc.Save();
            }

            public void DeleteFile(string fullFileName) => IOFile.Delete(fullFileName);

            /// <summary>
            /// Null when <paramref name="doc"/> has no direct reference to the old or the new path. Otherwise replaces each
            /// reference to the old path with the new one, renames the occurrences named after the old file, then sets
            /// <c>Dirty = true</c> and saves (spike 052). There is no <c>Document.ReferencedFileDescriptors</c>; it goes
            /// through <c>doc.File</c>.
            /// </summary>
            private static RenameFileUpdate? UpdateAndSave(Document doc, string oldFullFileName, string newFullFileName)
            {
                bool uses = false;
                foreach (FileDescriptor fd in doc.File.ReferencedFileDescriptors)
                {
                    string? path = ComSafe.Get(() => fd.FullFileName);
                    if (SamePath(path, oldFullFileName))
                    {
                        fd.ReplaceReference(newFullFileName);
                        uses = true;
                    }
                    else if (SamePath(path, newFullFileName))
                    {
                        uses = true;
                    }
                }

                if (!uses)
                    return null;

                var problems = new List<OccurrenceProblem>();
                int renamed = RenameOccurrences(doc, oldFullFileName, newFullFileName, problems);

                // After the occurrence renames, right before the save, so the save always writes.
                doc.Dirty = true;
                doc.Save();
                return new RenameFileUpdate(renamed, problems.Count > 0 ? problems : null);
            }

            /// <summary>
            /// In an assembly, renames each top-level occurrence of the renamed file whose name
            /// <see cref="RenamePlan.RenamedOccurrence"/> renames. The occurrence's file is matched against the new path,
            /// and the old one too, for a closed assembly whose occurrences may still read the old path after
            /// <c>ReplaceReference</c>. A failure is added to <paramref name="problems"/> and the rest go on. The count renamed.
            /// </summary>
            private static int RenameOccurrences(
                Document doc, string oldFullFileName, string newFullFileName, List<OccurrenceProblem> problems)
            {
                if (doc is not AssemblyDocument asm)
                    return 0;

                ComponentOccurrences? occurrences = ComSafe.Get(() => asm.ComponentDefinition.Occurrences);
                if (occurrences == null)
                    return 0;

                int renamed = 0;
                foreach (ComponentOccurrence occ in occurrences)
                {
                    string name = ComSafe.Get(() => occ.Name) ?? string.Empty;
                    string? newName = RenamePlan.RenamedOccurrence(name, oldFullFileName, newFullFileName);
                    if (newName == null)
                        continue;

                    string file = RenamePlan.FileOfDocumentName(
                        ComSafe.Get(() => occ.ReferencedDocumentDescriptor.FullDocumentName));
                    if (!SamePath(file, newFullFileName) && !SamePath(file, oldFullFileName))
                        continue;

                    try
                    {
                        occ.Name = newName;
                        renamed++;
                    }
                    catch (Exception ex)
                    {
                        problems.Add(new OccurrenceProblem(name, ex.Message));
                    }
                }

                return renamed;
            }
        }
    }
}
