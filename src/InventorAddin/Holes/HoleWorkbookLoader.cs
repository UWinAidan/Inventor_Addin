using System;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Settings;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Reads the hole workbook Settings points at, or the starter beside the add-in (spec 09, "The workbook"):
    /// resolved with <see cref="LibraryPaths.HoleWorkbook"/> and read with <see cref="HoleWorkbookReader.Read(LibraryPath)"/>,
    /// so each problem says which workbook it was. Problems are logged; nothing is thrown for a bad file.
    /// </summary>
    public static class HoleWorkbookLoader
    {
        private const string LogPrefix = "AWB Hole: ";

        public static HoleWorkbookReadResult Load(AddinSettings settings, ILog log)
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
                if (string.IsNullOrWhiteSpace(settings.HoleWorkbookPath))
                {
                    log.Error(LogPrefix + "the add-in's folder could not be found.", ex);
                    return new HoleWorkbookReadResult(
                        HoleWorkbook.Empty,
                        new[] { new HoleWorkbookProblem(null, null, HoleWorkbookMessages.AddinFolderUnknown(ex.Message)) });
                }

                addinFolder = string.Empty;
            }

            LibraryPath path = LibraryPaths.HoleWorkbook(settings, addinFolder);
            HoleWorkbookReadResult result = HoleWorkbookReader.Read(path);
            foreach (HoleWorkbookProblem problem in result.Problems)
                log.Warn(LogPrefix + $"hole workbook {path.Path}: {problem}");
            return result;
        }
    }
}
