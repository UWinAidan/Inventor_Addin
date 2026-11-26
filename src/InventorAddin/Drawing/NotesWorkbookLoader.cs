using System;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Settings;

namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Reads the notes workbook Settings points at, or the starter beside the add-in (spec 05, "Added for M5"), as
    /// <c>HoleWorkbookLoader</c> reads the hole workbook: resolved with <see cref="LibraryPaths.NotesWorkbook"/> and read with
    /// <see cref="NotesWorkbookReader.Read(LibraryPath)"/>, so each problem says which workbook it was. Problems are logged; nothing is
    /// thrown for a bad file.
    /// </summary>
    internal static class NotesWorkbookLoader
    {
        private const string LogPrefix = "Update Notes: ";

        public static NotesWorkbookReadResult Load(AddinSettings settings, ILog log)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            log ??= NullLog.Instance;

            string addinFolder;
            try
            {
                addinFolder = AddinServices.AddinFolder;
            }
            catch (InvalidOperationException ex)
            {
                // A workbook set in Settings does not need the add-in's folder.
                if (string.IsNullOrWhiteSpace(settings.NotesWorkbookPath))
                {
                    log.Error(LogPrefix + "the add-in's folder could not be found.", ex);
                    return new NotesWorkbookReadResult(
                        NotesWorkbook.Empty,
                        new[] { new NotesWorkbookProblem(null, null, NotesWorkbookMessages.AddinFolderUnknown(ex.Message)) });
                }

                addinFolder = string.Empty;
            }

            LibraryPath path = LibraryPaths.NotesWorkbook(settings, addinFolder);
            NotesWorkbookReadResult result = NotesWorkbookReader.Read(path);
            foreach (NotesWorkbookProblem problem in result.Problems)
                log.Warn(LogPrefix + $"notes workbook {path.Path}: {problem}");
            return result;
        }
    }
}
