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
using WpfWindow = System.Windows.Window;

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
    /// <para>
    /// Opening the files and showing the window is <see cref="MaterialFinishWindowLauncher"/>, which Part Properties'
    /// Change button uses too (task 076).
    /// </para>
    /// </remarks>
    public sealed class MaterialFinishCommand : DocumentEditCommand
    {
        public override string InternalName => CommandNames.MaterialFinish;
        public override string DisplayName => MaterialFinishWindowLauncher.Title;
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

        protected override void Run(Document doc) =>
            MaterialFinishWindowLauncher.Show(TargetDocument(doc), edit => RunInTransaction(doc, edit));

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
    }

    /// <summary>
    /// Opens the material library and the finish list and shows the Select Material and Finish window for a document's
    /// parts. Used by <see cref="MaterialFinishCommand"/> and by Part Properties' Change button (task 076), which also
    /// reads the library when it opens (<see cref="OpenLibrary"/>) for its not-from-the-library warning.
    /// </summary>
    internal static class MaterialFinishWindowLauncher
    {
        /// <summary>The command's name: the message box title and the window's command name.</summary>
        public const string Title = "Material and Finish";

        /// <summary>
        /// Opens the material library set in Settings, or the starter library (<see cref="LibraryPaths.MaterialLibrary"/>).
        /// Never throws and shows nothing: <paramref name="error"/> is the message to show when the library could not be
        /// read, and the result is null when the add-in's folder is unknown. Every failure is logged once, here or by
        /// <see cref="MaterialLibraryReader"/>. Opening a library on the NAS takes about a second (spike 067).
        /// </summary>
        public static MaterialLibrary? OpenLibrary(out string? error)
        {
            ILog log = AddinServices.Log;
            if (!TryAddinFolder(log, out string addinFolder, out error))
                return null;

            var warnings = new List<string>();
            MaterialLibrary library = MaterialLibraryReader.Open(
                InventorHost.App, LibraryPaths.MaterialLibrary(AddinServices.Settings, addinFolder), warnings);
            LogWarnings(log, warnings);
            error = library.Error;

            // The reader logs a file it could not open; a library that opened but holds no material is logged here.
            if (error != null && library.Library != null)
                log.Warn(MaterialFinishTargeting.LogPrefix + error);
            return library;
        }

        /// <summary>
        /// Finds the parts in <paramref name="target"/> (<see cref="MaterialFinishTargets.Find"/>), opens the library and
        /// the finish list, and shows the window until it closes. Each Apply runs through
        /// <paramref name="runInTransaction"/>. Returns false, after telling the user why in a message box, when the window
        /// could not be shown: no part to change, or a file that could not be read.
        /// </summary>
        /// <param name="target">The part, or the assembly whose selected parts are changed.</param>
        /// <param name="runInTransaction">Runs one Apply inside one transaction on the document open in the window.</param>
        /// <param name="openedLibrary">
        /// The library already opened by <see cref="OpenLibrary"/> without an error, to save opening it again; null opens it.
        /// </param>
        /// <param name="owner">The add-in window to open over; null opens over Inventor.</param>
        public static bool Show(
            Document target, Action<Action> runInTransaction, MaterialLibrary? openedLibrary = null, WpfWindow? owner = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (runInTransaction == null)
                throw new ArgumentNullException(nameof(runInTransaction));

            ILog log = AddinServices.Log;

            MaterialFinishTargetPlan<Document> plan = MaterialFinishTargets.Find(target);
            if (plan.StopMessage != null)
            {
                ShowMessage(plan.StopMessage, Forms.MessageBoxIcon.Information);
                return false;
            }

            if (!TryAddinFolder(log, out string addinFolder, out string? folderError))
            {
                ShowMessage(folderError!, Forms.MessageBoxIcon.Error);
                return false;
            }

            AddinSettings settings = AddinServices.Settings;
            LibraryPath finishListPath = LibraryPaths.FinishList(settings, addinFolder);

            var warnings = new List<string>();
            MaterialLibrary library;
            AppearanceFinder appearances;
            FinishListReadResult finishList;

            Forms.Cursor? previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                // The library first: its Error stops the command, and the appearance finder needs it opened so the
                // library gets the last rank (spec 03, "Finding an appearance by name").
                library = openedLibrary != null && openedLibrary.Error == null
                    ? openedLibrary
                    : MaterialLibraryReader.Open(InventorHost.App, LibraryPaths.MaterialLibrary(settings, addinFolder), warnings);
                if (library.Error != null)
                {
                    LogWarnings(log, warnings);
                    Forms.Cursor.Current = previousCursor;
                    ShowMessage(library.Error, Forms.MessageBoxIcon.Warning);
                    return false;
                }

                string? finishListError = ReadFinishList(finishListPath, log, out finishList);
                if (finishListError != null)
                {
                    LogWarnings(log, warnings);
                    Forms.Cursor.Current = previousCursor;
                    ShowMessage(finishListError, Forms.MessageBoxIcon.Warning);
                    return false;
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
            var targets = new MaterialFinishTargets(plan, library, appearances, runInTransaction, log);

            var viewModel = new MaterialFinishViewModel(
                current,
                library.Materials,
                finishList.Finishes,
                finishList.Problems,
                settings.PreferredMaterials,
                TogglePreferred,
                targets,
                log);

            WindowHost.ShowDialog(new MaterialFinishWindow(viewModel), IconNames.MaterialFinish, owner);
            return true;
        }

        private static bool TryAddinFolder(ILog log, out string addinFolder, out string? error)
        {
            try
            {
                addinFolder = AddinServices.AddinFolder;
                error = null;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                log.Error(MaterialFinishTargeting.LogPrefix + "the add-in's folder could not be found.", ex);
                addinFolder = string.Empty;
                error = MaterialLibraryMessages.AddinFolderUnknown(ex.Message);
                return false;
            }
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

        private static void ShowMessage(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, Title, Forms.MessageBoxButtons.OK, icon);
    }
}
