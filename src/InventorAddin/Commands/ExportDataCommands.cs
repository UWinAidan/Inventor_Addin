using System;
using System.Diagnostics;
using Inventor;
using InventorAddin.Core.Serialization;
using InventorAddin.Extraction;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>Developer tool: dumps everything extracted from the active document to JSON and opens it.</summary>
    public sealed class ExportModelDataCommand : AddinCommand
    {
        public override string InternalName => "WorkflowTools_ExportModelData";
        public override string DisplayName => "Export Model Data";
        public override string Description => "Extract all data from the active document to a JSON file (developer tool).";

        protected override void Execute()
        {
            Document doc = InventorHost.ActiveEditDocument
                ?? throw new InvalidOperationException("No document is open.");

            var data = ModelExtractor.Extract(doc);
            string name = IOPath.GetFileNameWithoutExtension(doc.FullFileName);
            ExportHelper.WriteAndOpen(string.IsNullOrEmpty(name) ? "Unsaved" : name, ModelJson.Serialize(data));
        }
    }

    /// <summary>Developer tool: dumps all loaded material/appearance libraries to JSON.</summary>
    public sealed class ExportLibrariesCommand : AddinCommand
    {
        public override string InternalName => "WorkflowTools_ExportLibraries";
        public override string DisplayName => "Export Libraries";
        public override string Description => "List all loaded material and appearance libraries to a JSON file (developer tool).";

        protected override void Execute()
        {
            var data = LibraryExtractor.Extract(InventorHost.App);
            ExportHelper.WriteAndOpen("Libraries", ModelJson.Serialize(data));
        }
    }

    internal static class ExportHelper
    {
        public static void WriteAndOpen(string baseName, string json)
        {
            string dir = IOPath.Combine(IOPath.GetTempPath(), "InventorWorkflowTools");
            System.IO.Directory.CreateDirectory(dir);
            string path = IOPath.Combine(dir, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            IOFile.WriteAllText(path, json);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
