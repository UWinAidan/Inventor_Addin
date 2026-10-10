using System;
using Inventor;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;
using SysEnvironment = System.Environment;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Mass Update (spec 02, "Commands: Mass Update"; task 048): scans a folder of old files, shows each file's number
    /// and names, and on the files ticked registers existing numbers, assigns new ones and replaces the names that mean
    /// the user, saving each file. The scan and the apply steps, their order and their messages are in
    /// <see cref="MassUpdateScanner"/> and <see cref="MassUpdateRunner"/> (Core); this class shows the window, supplies
    /// Inventor through <see cref="FileScanner"/>, and keeps the window responsive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a <see cref="DocumentEditCommand"/>: it works on files in a folder, not on the active document, and it is on
    /// the ZeroDoc ribbon. It opens, changes and saves files, so its command type is <c>kFileOperationsCmdType</c>, as
    /// for New Part. Saved files cannot be put back with Undo; the window asks first.
    /// </para>
    /// <para>
    /// Every COM call runs on the UI thread. Between files the scan and the apply let WPF process its queue
    /// (<see cref="PumpUi"/>), so the progress line is drawn and Stop can be clicked; the view-model disables everything
    /// else while they run.
    /// </para>
    /// </remarks>
    public sealed class MassUpdateCommand : AddinCommand
    {
        public override string InternalName => CommandNames.MassUpdate;
        public override string DisplayName => "Mass Update";
        public override string Description =>
            "Register numbers, assign new ones and replace your name in Designer and Detailer across a folder of files.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            AddinSettings settings = AddinServices.Settings;
            var viewModel = new MassUpdateViewModel(
                settings.WorkFolder,
                PersonNames.NamesThatMeanMe(SysEnvironment.UserName, settings.OtherNamesForMe),
                settings.DefaultDesigner);

            // The documents open now are the ones the user has open: the window is modal, so none opens or closes
            // while it is shown (see FileScanner).
            var files = new FileScanner(InventorHost.App, DisplayName);
            var window = new MassUpdateWindow(viewModel);

            viewModel.ScanRequested += (_, e) => Scan(viewModel, window, files, e.Folder);
            viewModel.ApplyRequested += (_, e) => Apply(viewModel, window, files, e.Plan);

            WindowHost.ShowDialog(window, IconNames.MassUpdate);
        }

        private void Scan(MassUpdateViewModel viewModel, MassUpdateWindow window, FileScanner files, string folder)
        {
            if (viewModel.IsBusy)
                return;

            viewModel.BeginWork(cancellable: true);
            try
            {
                var scanner = new MassUpdateScanner(AddinServices.RegisterStore, files, AddinServices.Log);
                MassUpdateScanResult result = scanner.Scan(
                    folder,
                    (current, total) =>
                    {
                        viewModel.ShowStatus(total == 0
                            ? MassUpdateScanner.ListingProgressText
                            : MassUpdateViewModel.ScanProgressText(current, total));
                        PumpUi();
                    },
                    () => viewModel.IsCancelRequested);

                viewModel.ShowScan(result.Files, result.Register);
                viewModel.ShowStatus(MassUpdateViewModel.ScanDoneText(result.Files.Count, result.Total, result.Cancelled));

                if (result.Notice != null)
                    ShowMessage(window, result.Notice, WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            }
            catch (MassUpdateException ex)
            {
                AddinServices.Log.Warn($"{DisplayName}: scan of '{folder}' stopped. {ex.Message}");
                viewModel.ShowStatus(ex.Message, isError: true);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"{DisplayName}: scan of '{folder}' failed.", ex);
                viewModel.ShowStatus(InventorAddin.Core.Logging.ErrorMessages.CommandFailed(DisplayName, ex, AddinServices.LogFilePath), isError: true);
            }
            finally
            {
                viewModel.EndWork();
            }
        }

        private void Apply(MassUpdateViewModel viewModel, MassUpdateWindow window, FileScanner files, MassUpdatePlan plan)
        {
            if (viewModel.IsBusy || plan.Updates.Count == 0)
                return;

            if (ShowMessage(window, viewModel.ConfirmText, WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Question)
                != WpfMessageBoxResult.Yes)
            {
                return;
            }

            viewModel.BeginWork(cancellable: false);
            try
            {
                var runner = new MassUpdateRunner(AddinServices.RegisterStore, files, AddinServices.Log);
                MassUpdateApplyResult result = runner.Apply(
                    plan,
                    (current, total) =>
                    {
                        viewModel.ShowStatus(MassUpdateViewModel.ApplyProgressText(current, total));
                        PumpUi();
                    });

                if (result.Results.Count > 0)
                {
                    viewModel.ReportResults(result.Results);
                    viewModel.ShowStatus(
                        MassUpdateRunner.SummaryText(result) + " " + MassUpdateViewModel.ScanAgainText,
                        isError: result.FailedCount > 0);
                }

                if (result.StopMessage != null)
                {
                    // Before any file changed, the status line says so too; Apply stays enabled to try again.
                    if (result.Results.Count == 0)
                        viewModel.ShowStatus(result.StopMessage, isError: true);
                    ShowMessage(window, result.StopMessage, WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                }

                if (result.Notice != null)
                    ShowMessage(window, result.Notice, WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"{DisplayName}: apply failed.", ex);
                viewModel.ShowStatus(InventorAddin.Core.Logging.ErrorMessages.CommandFailed(DisplayName, ex, AddinServices.LogFilePath), isError: true);
            }
            finally
            {
                viewModel.EndWork();
            }
        }

        private WpfMessageBoxResult ShowMessage(
            MassUpdateWindow window, string text, WpfMessageBoxButton buttons, WpfMessageBoxImage image) =>
            window.IsVisible
                ? WpfMessageBox.Show(window, text, DisplayName, buttons, image)
                : WpfMessageBox.Show(text, DisplayName, buttons, image);

        /// <summary>
        /// Lets WPF draw the progress line and handle Stop between files, as Rename does (the COM calls stay on the UI
        /// thread). Input is processed too; the view-model disables everything but Stop while busy, and the window
        /// refuses to close.
        /// </summary>
        private static void PumpUi() =>
            WpfDispatcher.CurrentDispatcher.Invoke(WpfDispatcherPriority.Background, new Action(() => { }));
    }
}
