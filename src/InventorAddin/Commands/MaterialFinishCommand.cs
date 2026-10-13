using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;
using Forms = System.Windows.Forms;
using IOException = System.IO.IOException;
using SysFile = System.IO.File;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Material and Finish (spec 03; task 075): opens the material library and the finish list set in Settings (or the
    /// starter files), finds the parts to change (<see cref="MaterialFinishTargets"/>), and shows the Select Material and
    /// Finish window. Each Apply (or OK) changes every target in one transaction on the document open in the window, so
    /// one Undo reverts the material, the appearance and the four finish iProperties (spike 067). The window's star saves
    /// the preferred materials to Settings straight away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target: a part document is changed itself; in an assembly, the selected parts are. While a part is edited in
    /// place inside an assembly, that part is the target, as Part Properties does (Default, task 075;
    /// <see cref="MaterialFinishTargeting.TargetsEditedPart"/>). The transaction is always started on the document open
    /// in the window (spike 067 section 4).
    /// </para>
    /// <para>
    /// Material, appearance and properties are not geometry, so the command type is <c>kNonShapeEditCmdType</c>.
    /// </para>
    /// </remarks>
    public sealed class MaterialFinishCommand : DocumentEditCommand
    {
        public override string InternalName => CommandNames.MaterialFinish;
        public override string DisplayName => "Material and Finish";
        public override string Description =>
            "Pick a material and a finish from the library and apply them to this part or the selected parts.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        // The document open in the window: it holds the assembly's selection also while something is edited in place
        // (spike 052, task 060), and the transaction is started on it.
        protected override Document? DocumentToEdit => InventorHost.ActiveDocument;

        protected override bool CanRunOn(DocumentKind kind) => kind.IsPart() || kind.IsAssembly();

        protected override string WrongDocumentMessage(DocumentKind kind) => MaterialFinishTargeting.WrongDocumentMessage;

        // Run is overridden: the window stays outside any transaction, and each Apply runs in one of its own.
        protected override void Execute(Document doc) =>
            throw new NotSupportedException("Material and Finish runs through Run.");

        protected override void Run(Document doc)
        {
            ILog log = AddinServices.Log;
            Document target = TargetDocument(doc);

            MaterialFinishTargetPlan<Document> plan = MaterialFinishTargets.Find(target);
            if (plan.StopMessage != null)
            {
                Show(plan.StopMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            string addinFolder;
            try
            {
                addinFolder = AddinServices.AddinFolder;
            }
            catch (InvalidOperationException ex)
            {
                log.Error(MaterialFinishTargeting.LogPrefix + "the add-in's folder could not be found.", ex);
                Show(MaterialLibraryMessages.AddinFolderUnknown(ex.Message), Forms.MessageBoxIcon.Error);
                return;
            }

            AddinSettings settings = AddinServices.Settings;
            LibraryPath libraryPath = LibraryPaths.MaterialLibrary(settings, addinFolder);
            LibraryPath finishListPath = LibraryPaths.FinishList(settings, addinFolder);

            var warnings = new List<string>();
            MaterialLibrary library;
            AppearanceFinder appearances;
            FinishListReadResult finishList;

            // Opening a library on the NAS takes about a second (spike 067).
            Forms.Cursor? previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                // The library first: its Error stops the command, and the appearance finder needs it opened so the
                // library gets the last rank (spec 03, "Finding an appearance by name").
                library = MaterialLibraryReader.Open(InventorHost.App, libraryPath, warnings);
                if (library.Error != null)
                {
                    LogWarnings(log, warnings);
                    Forms.Cursor.Current = previousCursor;
                    Show(library.Error, Forms.MessageBoxIcon.Warning);
                    return;
                }

                string? finishListError = ReadFinishList(finishListPath, log, out finishList);
                if (finishListError != null)
                {
                    LogWarnings(log, warnings);
                    Forms.Cursor.Current = previousCursor;
                    Show(finishListError, Forms.MessageBoxIcon.Warning);
                    return;
                }

                appearances = AppearanceFinder.Read(InventorHost.App, library.Library, warnings);
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }

            LogWarnings(log, warnings);

            // Each problem once, here: the view-model only counts them in the status line.
            foreach (FinishListProblem problem in finishList.Problems)
                log.Warn(MaterialLibraryMessages.FinishListProblemLogLine(finishListPath, problem));

            MaterialFinishCurrent current = MaterialFinishTargets.ReadCurrent(target, plan);
            var targets = new MaterialFinishTargets(plan, library, appearances, edit => RunInTransaction(doc, edit), log);

            var viewModel = new MaterialFinishViewModel(
                current,
                library.Materials,
                finishList.Finishes,
                finishList.Problems,
                settings.PreferredMaterials,
                TogglePreferred,
                targets,
                log);

            WindowHost.ShowDialog(new MaterialFinishWindow(viewModel), IconNames.MaterialFinish);
        }

        /// <summary>
        /// The document whose parts are changed: the part being edited in place inside an assembly, otherwise the document
        /// open in the window (Core decides, <see cref="MaterialFinishTargeting.TargetsEditedPart"/>).
        /// </summary>
        private static Document TargetDocument(Document open)
        {
            Document? edited = ComSafe.Get(() => InventorHost.ActiveEditDocument);
            if (edited == null)
                return open;

            DocumentKind openKind = DocumentKinds.Of(open);
            DocumentKind editedKind = ComSafe.Get(() => DocumentKinds.Of(edited));
            return MaterialFinishTargeting.TargetsEditedPart(openKind, editedKind) ? edited : open;
        }

        /// <summary>
        /// Reads and parses the finish list. Returns the message to show when the file is missing or cannot be read
        /// (naming it), or null; rows with problems are left out by the parser and do not stop the command.
        /// </summary>
        private static string? ReadFinishList(LibraryPath path, ILog log, out FinishListReadResult result)
        {
            result = new FinishListReadResult(Array.Empty<Finish>(), Array.Empty<FinishListProblem>());

            string text;
            try
            {
                text = SysFile.ReadAllText(path.Path);
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException)
            {
                string message = MaterialLibraryMessages.FinishListMissing(path);
                log.Warn(MaterialFinishTargeting.LogPrefix + message);
                return message;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
                                           or System.Security.SecurityException)
            {
                string message = MaterialLibraryMessages.FinishListUnreadable(path, ex.Message);
                log.Error(MaterialFinishTargeting.LogPrefix + message, ex);
                return message;
            }

            result = FinishListCsv.Parse(text);
            return null;
        }

        /// <summary>
        /// The star in the window: adds the material to the preferred materials or removes it, saves Settings and makes
        /// the new settings the session's (spec 03, question 11). Throws when it cannot save; the view-model then leaves the
        /// star as it was and shows why.
        /// </summary>
        private static void TogglePreferred(string materialName)
        {
            string folder = AddinServices.DataFolder
                ?? throw new InvalidOperationException("The add-in could not set up its data folder when Inventor started.");

            AddinSettings updated = LibraryPaths.TogglePreferred(AddinServices.Settings, materialName);
            new SettingsStore(folder).Save(updated);
            AddinServices.UpdateSettings(updated);
        }

        private static void LogWarnings(ILog log, List<string> warnings)
        {
            foreach (string warning in warnings)
                log.Warn(MaterialFinishTargeting.LogPrefix + warning);
            warnings.Clear();
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
