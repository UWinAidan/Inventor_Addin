using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Ribbon;
using InventorAddin.PrintTemplates;
using Forms = System.Windows.Forms;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Developer tool (task 187, spec 05 question 22): draws the AWB metric and imperial print templates from <see cref="PrintTemplateLayout"/>
    /// into a folder the developer chooses (normally <c>libraries\Templates</c> in the repository), then commits them. The drawing is done by
    /// <see cref="PrintTemplateBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Like Create Print it makes and saves new files and never changes an open document, so its command type is
    /// <c>kFileOperationsCmdType</c>. It is on the Developer panel only.
    /// </remarks>
    public sealed class BuildPrintTemplateCommand : AddinCommand
    {
        public override string InternalName => CommandNames.BuildPrintTemplate;
        public override string DisplayName => "Build Print Template";
        public override string Description =>
            "Draw the AWB metric and imperial print templates from the layout and save them in a folder (developer tool).";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            var folderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = $"Choose the folder for {PrintTemplateBuilder.FileNameFor(PrintUnits.Metric)} and {PrintTemplateBuilder.FileNameFor(PrintUnits.Imperial)} (libraries\\Templates)",
                Multiselect = false,
            };
            if (folderDialog.ShowDialog() != true)
                return;

            string folder = folderDialog.FolderName;
            string? logo = FindLogo(folder);
            if (logo == null)
            {
                Show("The logo file " + Branding.PrintLogoFileName + " was not found, so no template was made.", Forms.MessageBoxIcon.Warning);
                return;
            }

            PrintUnits[] units = { PrintUnits.Metric, PrintUnits.Imperial };
            string[] existing = units
                .Select(u => IOPath.Combine(folder, PrintTemplateBuilder.FileNameFor(u)))
                .Where(IOFile.Exists)
                .ToArray();
            if (existing.Length > 0)
            {
                string question = "These files exist and will be replaced:\n\n" + string.Join("\n", existing) + "\n\nReplace them?";
                if (Forms.MessageBox.Show(question, DisplayName, Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) != Forms.DialogResult.Yes)
                    return;
            }

            // A template cannot be rebuilt while Inventor has it open.
            foreach (string path in existing)
            {
                bool open = InventorHost.App.Documents.Cast<Document>()
                    .Any(d => string.Equals(ComSafe.Get(() => d.FullFileName), path, StringComparison.OrdinalIgnoreCase));
                if (open)
                {
                    Show($"'{path}' is open in Inventor. Close it and run the command again.", Forms.MessageBoxIcon.Information);
                    return;
                }
            }

            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                Run(folder, logo, units);
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }
        }

        private void Run(string folder, string logo, PrintUnits[] units)
        {
            var report = new List<string>();
            bool failed = false;
            foreach (PrintUnits unit in units)
            {
                string path = IOPath.Combine(folder, PrintTemplateBuilder.FileNameFor(unit));
                var builder = new PrintTemplateBuilder(InventorHost.App, logo, AddinServices.Log);
                try
                {
                    builder.Build(unit, path);
                    report.Add($"Made {path}" + (builder.Warnings.Count > 0 ? $" ({builder.Warnings.Count} warning(s), see the log)" : "."));
                }
                catch (Exception ex)
                {
                    failed = true;
                    AddinServices.Log.Error($"Build Print Template ({unit}) failed while {builder.Step}.", ex);
                    report.Add($"The {unit.ToString().ToLowerInvariant()} template failed while {builder.Step}: {ex.Message}");
                    // Nothing is left half made: the builder saves under a temporary name and removes it on failure.
                    break;
                }
            }

            if (!failed)
            {
                // Create Print reads the starter copy beside the add-in, so put the new templates there too (task 239).
                string? target = PrintTemplatePublish.CopyTarget(folder, AddinServices.AddinFolder);
                var copyFailures = new List<string>();
                if (target != null)
                {
                    foreach (PrintUnits unit in units)
                    {
                        string name = PrintTemplateBuilder.FileNameFor(unit);
                        try
                        {
                            IODirectory.CreateDirectory(target);
                            IOFile.Copy(IOPath.Combine(folder, name), IOPath.Combine(target, name), overwrite: true);
                        }
                        catch (Exception ex)
                        {
                            AddinServices.Log.Warn($"Build Print Template could not copy {name} to {target}: {ex.Message}");
                            copyFailures.Add($"{name}: {ex.Message}");
                        }
                    }
                }

                IReadOnlyList<string> lines = PrintTemplatePublish.Report(folder, target, copyFailures, AddinServices.Settings);
                AddinServices.Log.Info("Build Print Template: " + string.Join(" ", lines));
                report.AddRange(lines);
                report.Add("Open each one in Inventor and check it against the checklist before committing.");
            }
            else if (AddinServices.LogFilePath != null)
                report.Add("The log has the details: " + AddinServices.LogFilePath);

            Show(string.Join("\n\n", report), failed ? Forms.MessageBoxIcon.Error : Forms.MessageBoxIcon.Information);
        }

        /// <summary>
        /// The logo in <c>libraries\Branding</c>: found beside the chosen folder (<c>libraries\Templates</c>), then above the add-in's own
        /// folder, then asked for.
        /// </summary>
        private static string? FindLogo(string chosenFolder)
        {
            string relative = IOPath.Combine("libraries", "Branding", Branding.PrintLogoFileName);
            var candidates = new List<string> { IOPath.GetFullPath(IOPath.Combine(chosenFolder, "..", "Branding", Branding.PrintLogoFileName)) };

            ComSafe.Run(() =>
            {
                for (string? dir = AddinServices.AddinFolder; !string.IsNullOrEmpty(dir); dir = IOPath.GetDirectoryName(dir))
                    candidates.Add(IOPath.Combine(dir, relative));
            });

            string? found = candidates.FirstOrDefault(IOFile.Exists);
            if (found != null)
                return found;

            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose " + Branding.PrintLogoFileName,
                Filter = "Logo (" + Branding.PrintLogoFileName + ")|" + Branding.PrintLogoFileName + "|PNG files (*.png)|*.png",
                CheckFileExists = true,
            };
            return picker.ShowDialog() == true ? picker.FileName : null;
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
