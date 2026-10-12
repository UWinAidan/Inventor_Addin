using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Mass Update's files in Inventor (task 048), by the calls spike 032 confirmed: <c>Documents.Open(path, false)</c>
    /// with <c>SilentOperation</c> on around the open only, the properties read with <see cref="PropertyReader"/>, and
    /// <c>Close(true)</c>, which does not save.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The documents open in Inventor are listed once, when this is created, with a loop over <c>Documents</c> comparing
    /// <c>FullFileName</c> ignoring case (spike 032: <c>get_ItemByName</c> throws for a file that is not open, and the
    /// loop also finds invisible documents). Mass Update's window is modal, so nothing else opens or closes a document
    /// while it is shown. Listing them once means a file Mass Update itself loaded (for example a part loaded by an
    /// assembly it opened, which may stay loaded after the assembly is closed) is not mistaken for one the user has open.
    /// </para>
    /// <para>Every call runs on the UI thread.</para>
    /// </remarks>
    internal sealed class FileScanner : IMassUpdateFiles
    {
        public const string ReadOnlyText = "The file is read-only.";

        private readonly Application _app;
        private readonly string _commandName;
        private readonly Dictionary<string, Document> _openAtStart = new(StringComparer.OrdinalIgnoreCase);

        /// <param name="app">The running Inventor.</param>
        /// <param name="commandName">The command's display name: the write transaction's name, and the log prefix.</param>
        public FileScanner(Application app, string commandName)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _commandName = commandName;

            foreach (Document d in _app.Documents)
            {
                string path = ComSafe.Get(() => d.FullFileName) ?? string.Empty;
                if (path.Length > 0 && !_openAtStart.ContainsKey(path))
                    _openAtStart.Add(path, d);
            }
        }

        public ScannedFile Read(string fullPath)
        {
            if (_openAtStart.TryGetValue(fullPath, out Document? open))
                return ReadFrom(open, fullPath, openInInventor: true);

            Document doc;
            try
            {
                doc = OpenSilently(fullPath);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn($"{_commandName}: '{fullPath}' could not be opened to read it. {ex.Message}");
                return new ScannedFile(fullPath, "", "", "", "", DocumentKind.Unknown, ReadError: CouldNotOpenText(ex));
            }

            try
            {
                return ReadFrom(doc, fullPath, openInInventor: false);
            }
            finally
            {
                // Close(true) closes without saving (spike 032).
                ComSafe.Run(() => doc.Close(true));
            }
        }

        public IMassUpdateDocument Open(string fullPath)
        {
            if (_openAtStart.ContainsKey(fullPath))
                throw new InvalidOperationException(ScannedFileState.OpenInInventorText);

            if (ModelExtractor.IsFileReadOnly(fullPath))
                throw new InvalidOperationException(ReadOnlyText);

            Document doc;
            try
            {
                doc = OpenSilently(fullPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(CouldNotOpenText(ex), ex);
            }

            // Writes go through PropertyWriter inside one transaction, as for New Part and Save As Copy.
            return new InventorNewDocument(doc, _commandName);
        }

        private static string CouldNotOpenText(Exception ex) => $"The file could not be opened: {ex.Message}";

        /// <summary>Opens invisibly with SilentOperation on around the open only, restored in a finally (spike 032).</summary>
        private Document OpenSilently(string fullPath)
        {
            bool previousSilent = _app.SilentOperation;
            _app.SilentOperation = true;
            try
            {
                return _app.Documents.Open(fullPath, false);
            }
            finally
            {
                _app.SilentOperation = previousSilent;
            }
        }

        /// <summary>The properties Mass Update needs. Never throws: a property that cannot be read reads as blank.</summary>
        private static ScannedFile ReadFrom(Document doc, string fullPath, bool openInInventor) =>
            new(
                fullPath,
                ReadText(doc, PartPropertyStorage.PartNumber),
                ReadText(doc, PartPropertyStorage.Designer),
                ReadText(doc, PartPropertyStorage.Detailer),
                ReadText(doc, PartPropertyStorage.PartType),
                ComSafe.Get(() => DocumentKinds.Of(doc)),
                openInInventor,
                Author: ReadText(doc, PartPropertyStorage.Author));

        private static string ReadText(Document doc, PropertyLocation location) =>
            PropertyReader.GetString(doc, location.SetName, location.PropertyName) ?? string.Empty;
    }
}
