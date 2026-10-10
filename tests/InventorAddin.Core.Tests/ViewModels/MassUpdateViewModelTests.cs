using System.ComponentModel;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class MassUpdateViewModelTests
{
    private const string User = "testlogin";
    private const string Designer = "Test Designer";
    private const string Folder = @"C:\Work\Project";

    private static readonly PartRegister Register = new(
    [
        new RegisterEntry(
            new PartNumber(10001), "test plate", "M", DocumentKind.Part, @"C:\Work\Project\AWB-10001,test_plate.ipt",
            new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero), "someone", "TESTPC"),
    ]);

    private static ScannedFile File(
        string name,
        string partNumber = "",
        string designer = "Alex Example",
        string detailer = "Alex Example",
        bool open = false,
        string? error = null) =>
        new($@"{Folder}\{name}", partNumber, designer, detailer, "M", DocumentKind.Part, open, error);

    private static MassUpdateViewModel Create(string defaultDesigner = Designer) => new(Folder, [User], defaultDesigner);

    private static MassUpdateViewModel Scanned(string defaultDesigner, params ScannedFile[] files)
    {
        var vm = Create(defaultDesigner);
        vm.ShowScan(files, Register);
        return vm;
    }

    // Folder and scan

    [Fact]
    public void FolderStartsAtTheDefaultGiven()
    {
        Assert.Equal(Folder, Create().Folder);
    }

    [Fact]
    public void ScanCommandRaisesScanRequestedWithTheTrimmedFolder()
    {
        var vm = Create();
        vm.Folder = @"  D:\Other  ";
        string? requested = null;
        vm.ScanRequested += (_, e) => requested = e.Folder;

        vm.ScanCommand.Execute(null);

        Assert.Equal(@"D:\Other", requested);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ScanIsDisabledWithABlankFolder(string folder)
    {
        var vm = Create();
        var raised = false;
        vm.ScanCommand.CanExecuteChanged += (_, _) => raised = true;

        vm.Folder = folder;

        Assert.False(vm.ScanCommand.CanExecute(null));
        Assert.True(raised);
    }

    [Fact]
    public void ShowScanMakesOneRowPerFileInOrder()
    {
        var vm = Scanned(Designer, File("b.ipt"), File("a.ipt"));

        Assert.Equal(["b.ipt", "a.ipt"], vm.Rows.Select(r => r.FileName));
        Assert.All(vm.Rows, r => Assert.Equal(MassUpdateActions.None, r.Ticked));
    }

    [Fact]
    public void ShowScanRaisesRowsAndCounts()
    {
        var vm = Create();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowScan([File("a.ipt")], Register);

        Assert.Contains(nameof(MassUpdateViewModel.Rows), changed);
        Assert.Contains(nameof(MassUpdateViewModel.CountsText), changed);
    }

    // Row state text

    [Theory]
    [InlineData("AWB-10001", "Has a number")]
    [InlineData("AWB-10002", "Not in the register")]
    [InlineData("", "No number")]
    [InlineData("a", "No number")]
    public void RowShowsTheNumberState(string partNumber, string expected)
    {
        var row = Scanned(Designer, File("a.ipt", partNumber)).Rows[0];

        Assert.Equal(expected, row.StateText);
        Assert.Equal(partNumber, row.PartNumberText);
    }

    [Theory]
    [InlineData(User, "Alex Example", "Has a number; your name as Designer")]
    [InlineData("Alex Example", User, "Has a number; your name as Detailer")]
    [InlineData(User, User, "Has a number; your name as Designer and Detailer")]
    public void RowCallsANameThatMeansMeYourName(string designer, string detailer, string expected)
    {
        var row = Scanned(Designer, File("a.ipt", "AWB-10001", designer, detailer)).Rows[0];

        Assert.Equal(expected, row.StateText);
    }

    [Fact]
    public void RowOpenInInventorShowsWhyAndOffersNothing()
    {
        var row = Scanned(Designer, File("a.ipt", "", User, User, open: true)).Rows[0];

        Assert.Equal("Open in Inventor; close it first.", row.StateText);
        Assert.False(row.CanRegister);
        Assert.False(row.CanAssign);
        Assert.False(row.CanReplaceNames);
    }

    [Fact]
    public void RowWithAReadErrorShowsTheError()
    {
        var row = Scanned(Designer, File("a.ipt", error: "The file is damaged.")).Rows[0];

        Assert.Equal("The file is damaged.", row.StateText);
        Assert.False(row.CanAssign);
    }

    // Tick boxes

    [Fact]
    public void TicksThatDoNotApplyStayOff()
    {
        var row = Scanned(Designer, File("a.ipt", "AWB-10001")).Rows[0];

        row.Register = true;
        row.Assign = true;
        row.ReplaceNames = true;

        Assert.Equal(MassUpdateActions.None, row.Ticked);
    }

    [Fact]
    public void TicksThatApplyAreKept()
    {
        var row = Scanned(Designer, File("a.ipt", "", User)).Rows[0];

        row.Assign = true;
        row.ReplaceNames = true;

        Assert.Equal(MassUpdateActions.Assign | MassUpdateActions.ReplaceNames, row.Ticked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ReplaceNamesIsNotOfferedWithABlankDefaultDesigner(string defaultDesigner)
    {
        var vm = Scanned(defaultDesigner, File("a.ipt", "AWB-10001", User));
        var row = vm.Rows[0];

        row.ReplaceNames = true;

        Assert.False(row.CanReplaceNames);
        Assert.False(row.ReplaceNames);
        Assert.Equal("Set Default designer in Settings first.", row.ReplaceNamesNote);
        Assert.Equal("Set Default designer in Settings first.", vm.ReplaceNamesDisabledReason);
    }

    [Fact]
    public void ReplaceNamesHasNoNoteWhenOffered()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10001", User), File("b.ipt", "AWB-10001"));

        Assert.True(vm.Rows[0].CanReplaceNames);
        Assert.Null(vm.Rows[0].ReplaceNamesNote);
        Assert.Null(vm.Rows[1].ReplaceNamesNote);
        Assert.Null(vm.ReplaceNamesDisabledReason);
    }

    [Fact]
    public void TickAllTicksOnlyTheRowsTheActionAppliesTo()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10002"), File("b.ipt", ""), File("c.ipt", "AWB-10003"));

        vm.TickAllRegister = true;

        Assert.Equal([true, false, true], vm.Rows.Select(r => r.Register));
        Assert.True(vm.TickAllRegister);
        Assert.False(vm.TickAllAssign);
    }

    [Fact]
    public void TickAllFalseUnticksEveryRow()
    {
        var vm = Scanned(Designer, File("a.ipt", "", User), File("b.ipt", "", User));
        vm.TickAllAssign = true;
        vm.TickAllReplaceNames = true;

        vm.TickAllAssign = false;

        Assert.All(vm.Rows, r => Assert.False(r.Assign));
        Assert.All(vm.Rows, r => Assert.True(r.ReplaceNames));
    }

    [Fact]
    public void TickAllIsFalseWhenOneRowIsUnticked()
    {
        var vm = Scanned(Designer, File("a.ipt"), File("b.ipt"));
        vm.TickAllAssign = true;

        vm.Rows[1].Assign = false;

        Assert.False(vm.TickAllAssign);
    }

    [Fact]
    public void TickAllIsFalseWhenNoRowOffersTheAction()
    {
        var vm = Scanned(Designer, File("a.ipt"));

        vm.TickAllRegister = true;

        Assert.False(vm.TickAllRegister);
    }

    [Fact]
    public void TickingARowRaisesTheDependentProperties()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var canExecuteRaised = false;
        vm.ApplyCommand.CanExecuteChanged += (_, _) => canExecuteRaised = true;

        vm.Rows[0].Assign = true;

        Assert.Contains(nameof(MassUpdateViewModel.ConfirmText), changed);
        Assert.Contains(nameof(MassUpdateViewModel.UpdateCount), changed);
        Assert.Contains(nameof(MassUpdateViewModel.TickAllAssign), changed);
        Assert.True(canExecuteRaised);
    }

    // Counts

    [Fact]
    public void CountsAreEmptyBeforeAScan()
    {
        Assert.Equal("", Create().CountsText);
    }

    [Fact]
    public void CountsGiveFilesAndWhatEachActionAppliesTo()
    {
        var vm = Scanned(
            Designer,
            File("a.ipt", "AWB-10001", User),
            File("b.ipt", "AWB-10002"),
            File("c.ipt", "AWB-10003", detailer: User),
            File("d.ipt", ""),
            File("e.ipt", "", User, User),
            File("f.ipt", "", User, open: true));

        Assert.Equal("6 files, 2 to register, 2 to number, 3 with your name", vm.CountsText);
    }

    [Fact]
    public void OtherNamesThatMeanMeAreOfferedLikeTheWindowsUser()
    {
        var vm = new MassUpdateViewModel(Folder, PersonNames.NamesThatMeanMe(User, "T.Login, tl"), Designer);
        vm.ShowScan([File("a.ipt", "AWB-10001", "t.login", "TL"), File("b.ipt", "AWB-10001", "T.Login2")], Register);

        var row = vm.Rows[0];
        Assert.True(row.CanReplaceNames);
        Assert.Equal("Has a number; your name as Designer and Detailer", row.StateText);
        Assert.False(vm.Rows[1].CanReplaceNames);
        Assert.Equal("2 files, 0 to register, 0 to number, 1 with your name", vm.CountsText);

        row.ReplaceNames = true;
        var update = Assert.Single(vm.BuildPlan().Updates);
        Assert.All(update.Writes, w => Assert.Equal(Designer, w.TextValue));
        Assert.Equal(2, update.Writes.Count);
    }

    [Fact]
    public void ConstructorRejectsNullNames()
    {
        Assert.Throws<ArgumentNullException>(() => new MassUpdateViewModel(Folder, null!, Designer));
    }

    [Fact]
    public void CountsSayOneFile()
    {
        Assert.Equal(
            "1 file, 0 to register, 1 to number, 0 with your name",
            Scanned(Designer, File("a.ipt")).CountsText);
    }

    [Fact]
    public void CountsIncludeYourNameWhenDefaultDesignerIsBlank()
    {
        Assert.Equal(
            "1 file, 0 to register, 0 to number, 1 with your name",
            Scanned("", File("a.ipt", "AWB-10001", User)).CountsText);
    }

    // Confirmation and Apply

    [Fact]
    public void ConfirmTextCountsTheFilesThatWillChange()
    {
        var vm = Scanned(Designer, File("a.ipt"), File("b.ipt", "AWB-10002"), File("c.ipt", "AWB-10001", User), File("d.ipt"));
        vm.Rows[0].Assign = true;
        vm.Rows[1].Register = true;
        vm.Rows[2].ReplaceNames = true;

        Assert.Equal(3, vm.UpdateCount);
        Assert.Equal("Change 3 files? This saves them and cannot be undone with Undo.", vm.ConfirmText);
    }

    [Fact]
    public void ConfirmTextForOneFile()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        vm.Rows[0].Assign = true;

        Assert.Equal("Change 1 file? This saves it and cannot be undone with Undo.", vm.ConfirmText);
    }

    [Fact]
    public void ApplyIsDisabledWithNothingTicked()
    {
        var vm = Scanned(Designer, File("a.ipt"));

        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Equal(0, vm.UpdateCount);
    }

    [Fact]
    public void ApplyRaisesApplyRequestedWithThePlan()
    {
        var vm = Scanned(Designer, File("a.ipt", "", User, User), File("b.ipt", "AWB-10002"));
        vm.TickAllAssign = true;
        vm.TickAllReplaceNames = true;
        vm.TickAllRegister = true;
        MassUpdatePlan? plan = null;
        vm.ApplyRequested += (_, e) => plan = e.Plan;

        vm.ApplyCommand.Execute(null);

        Assert.NotNull(plan);
        Assert.Same(plan, vm.LastPlan);
        Assert.Equal(2, plan!.Updates.Count);
        Assert.Same(AssignNew.Instance, plan.Updates[0].Register);
        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Designer, Designer),
                PropertyWrite.SetText(PartPropertyStorage.Detailer, Designer),
            ],
            plan.Updates[0].Writes);
        Assert.Equal(new RegisterExisting(new PartNumber(10002)), plan.Updates[1].Register);
    }

    // Results

    [Fact]
    public void ReportedResultsShowDoneOrTheError()
    {
        var vm = Scanned(Designer, File("a.ipt"), File("b.ipt"), File("c.ipt"));
        vm.TickAllAssign = true;
        vm.ApplyCommand.Execute(null);

        vm.ReportResults(
        [
            new FileUpdateResult($@"{Folder}\a.ipt"),
            new FileUpdateResult($@"{Folder}\B.IPT", "The file could not be saved."),
        ]);

        Assert.Equal("Done", vm.Rows[0].ResultText);
        Assert.True(vm.Rows[0].Succeeded);
        Assert.Equal("The file could not be saved.", vm.Rows[1].ResultText);
        Assert.False(vm.Rows[1].Succeeded);
        Assert.Null(vm.Rows[2].ResultText);
        Assert.Null(vm.Rows[2].Succeeded);
    }

    // Duplicates at scan time (task 048)

    [Fact]
    public void ALaterFileCarryingTheSameUnregisteredNumberSaysSoAtScanAndOffersNoRegister()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10005"), File("b.ipt", "awb-10005"));

        Assert.True(vm.Rows[0].CanRegister);
        Assert.Equal(MassUpdateRow.NotInRegisterText, vm.Rows[0].StateText);
        Assert.Null(vm.Rows[0].ScanDuplicate);

        var row = vm.Rows[1];
        Assert.False(row.CanRegister);
        Assert.Equal("Not registered: AWB-10005 is also on a.ipt", row.StateText);
        Assert.Equal(new MassUpdateDuplicate(new PartNumber(10005), $@"{Folder}\a.ipt", $@"{Folder}\b.ipt"), row.ScanDuplicate);
        row.Register = true;
        Assert.False(row.Register);
        Assert.Equal("2 files, 1 to register, 0 to number, 0 with your name", vm.CountsText);
    }

    [Fact]
    public void TickAllRegisterSkipsTheDuplicate_AndThePlanRegistersTheFirstOnly()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10005"), File("b.ipt", "AWB-10005", User));

        vm.TickAllRegister = true;
        vm.TickAllReplaceNames = true;

        Assert.True(vm.TickAllRegister);
        var plan = vm.BuildPlan();
        Assert.Empty(plan.Duplicates);
        Assert.Equal(2, plan.Updates.Count);
        Assert.Equal(new RegisterExisting(new PartNumber(10005)), plan.Updates[0].Register);
        Assert.Null(plan.Updates[1].Register);
        Assert.Single(plan.Updates[1].Writes);
    }

    [Fact]
    public void DuplicateRowWithYourNameShowsBoth_AndStillOffersReplaceNames()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10005"), File("b.ipt", "AWB-10005", User));

        Assert.Equal("Not registered: AWB-10005 is also on a.ipt; your name as Designer", vm.Rows[1].StateText);
        Assert.True(vm.Rows[1].CanReplaceNames);
    }

    [Fact]
    public void AFileOpenInInventorStillKeepsItsNumber_SoALaterCarrierIsTheDuplicate()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10005", open: true), File("b.ipt", "AWB-10005"));

        Assert.Equal(ScannedFileState.OpenInInventorText, vm.Rows[0].StateText);
        Assert.False(vm.Rows[1].CanRegister);
        Assert.Equal("Not registered: AWB-10005 is also on a.ipt", vm.Rows[1].StateText);
    }

    [Fact]
    public void ARegisteredNumberOnTwoFilesIsNotADuplicate()
    {
        var vm = Scanned(Designer, File("a.ipt", "AWB-10001"), File("b.ipt", "AWB-10001"));

        Assert.All(vm.Rows, r => Assert.Null(r.ScanDuplicate));
        Assert.All(vm.Rows, r => Assert.Equal(MassUpdateRow.HasNumberText, r.StateText));
    }

    [Fact]
    public void DuplicateTextNamesTheFirstFileOnly()
    {
        var duplicate = new MassUpdateDuplicate(new PartNumber(10001), @"C:\A\AWB-10001,plate.ipt", @"C:\B\copy.ipt");

        Assert.Equal("Not registered: AWB-10001 is also on AWB-10001,plate.ipt", MassUpdateViewModel.DuplicateText(duplicate));
    }

    [Fact]
    public void ApplyIsDisabledAfterResultsUntilTheNextScan()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        vm.Rows[0].Assign = true;
        Assert.True(vm.ApplyCommand.CanExecute(null));

        vm.ApplyCommand.Execute(null);
        vm.ReportResults([new FileUpdateResult($@"{Folder}\a.ipt")]);

        Assert.False(vm.ApplyCommand.CanExecute(null));
        vm.ShowScan([File("a.ipt")], Register);
        vm.Rows[0].Assign = true;
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyStaysEnabledWhenNoResultsWereReported()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        vm.Rows[0].Assign = true;

        vm.ApplyCommand.Execute(null);

        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void ResultFlagsFollowTheResult()
    {
        var vm = Scanned(Designer, File("a.ipt"), File("b.ipt"));
        Assert.False(vm.Rows[0].ResultIsDone);
        Assert.False(vm.Rows[0].ResultIsError);

        vm.ReportResults([new FileUpdateResult($@"{Folder}\a.ipt"), new FileUpdateResult($@"{Folder}\b.ipt", "Failed.")]);

        Assert.True(vm.Rows[0].ResultIsDone);
        Assert.False(vm.Rows[0].ResultIsError);
        Assert.False(vm.Rows[1].ResultIsDone);
        Assert.True(vm.Rows[1].ResultIsError);
    }

    // Busy, progress and cancel (task 048)

    [Fact]
    public void WhileBusyScanApplyAndCloseAreDisabled_AndCancelOnlyForAScan()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        vm.Rows[0].Assign = true;

        vm.BeginWork(cancellable: true);

        Assert.True(vm.IsBusy);
        Assert.False(vm.IsIdle);
        Assert.False(vm.ScanCommand.CanExecute(null));
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.True(vm.CanCancel);

        vm.EndWork();

        Assert.False(vm.IsBusy);
        Assert.True(vm.ScanCommand.CanExecute(null));
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.CanCancel);

        vm.BeginWork(cancellable: false);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.CanCancel);
    }

    [Fact]
    public void CancelAsksTheScanToStop_AndSaysSo()
    {
        var vm = Create();
        vm.BeginWork(cancellable: true);

        vm.CancelCommand.Execute(null);

        Assert.True(vm.IsCancelRequested);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.Equal(MassUpdateViewModel.CancellingText, vm.StatusText);

        vm.EndWork();
        vm.BeginWork(cancellable: true);
        Assert.False(vm.IsCancelRequested);
        Assert.Null(vm.StatusText);
    }

    [Fact]
    public void StatusLine()
    {
        var vm = Create();

        vm.ShowStatus("Something went wrong.", isError: true);
        Assert.Equal("Something went wrong.", vm.StatusText);
        Assert.True(vm.StatusIsError);

        vm.ShowStatus(" ", isError: true);
        Assert.Null(vm.StatusText);
        Assert.False(vm.StatusIsError);

        vm.ShowStatus(MassUpdateViewModel.ScanProgressText(3, 12));
        Assert.Equal("Reading 3 of 12", vm.StatusText);
        Assert.False(vm.StatusIsError);
        Assert.Equal("Updating 1 of 7", MassUpdateViewModel.ApplyProgressText(1, 7));
    }

    [Theory]
    [InlineData(4, 12, true, "Scan stopped after 4 of 12 files.")]
    [InlineData(0, 0, false, MassUpdateViewModel.NoFilesText)]
    [InlineData(12, 12, false, null)]
    public void ScanDoneText(int read, int total, bool cancelled, string? expected)
    {
        Assert.Equal(expected, MassUpdateViewModel.ScanDoneText(read, total, cancelled));
    }

    [Fact]
    public void HasActionsIsFalseOnlyForARowWithNothingToOffer()
    {
        var vm = Scanned(
            Designer,
            File("a.ipt", "AWB-10001"),
            File("b.ipt", ""),
            File("c.ipt", "AWB-10001", User),
            File("d.ipt", "", open: true),
            File("e.ipt", "AWB-10005"),
            File("f.ipt", "AWB-10005"));

        Assert.Equal([false, true, true, false, true, false], vm.Rows.Select(r => r.HasActions));
        Assert.True(Scanned("", File("a.ipt", "AWB-10001", User)).Rows[0].HasActions);
    }

    [Fact]
    public void HasReplaceNamesDisabledReasonFollowsTheDefaultDesigner()
    {
        Assert.False(Create().HasReplaceNamesDisabledReason);
        Assert.True(Create("  ").HasReplaceNamesDisabledReason);
    }

    [Fact]
    public void HeaderAndHasRows()
    {
        var vm = Create();
        Assert.Equal("Mass Update", vm.HeaderTitle);
        Assert.False(string.IsNullOrWhiteSpace(vm.HeaderSubline));
        Assert.False(vm.HasRows);

        vm.ShowScan([File("a.ipt")], Register);

        Assert.True(vm.HasRows);
    }

    [Fact]
    public void ResultChangesAreRaised()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        var changed = new List<string?>();
        vm.Rows[0].PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ReportResults([new FileUpdateResult($@"{Folder}\a.ipt")]);

        Assert.Contains(nameof(MassUpdateRow.ResultText), changed);
        Assert.Contains(nameof(MassUpdateRow.Succeeded), changed);
    }

    [Fact]
    public void ANewScanClearsTheLastPlanAndStopsListeningToOldRows()
    {
        var vm = Scanned(Designer, File("a.ipt"));
        var oldRow = vm.Rows[0];
        oldRow.Assign = true;
        vm.ApplyCommand.Execute(null);

        vm.ShowScan([File("b.ipt")], Register);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        oldRow.Assign = false;

        Assert.Null(vm.LastPlan);
        Assert.Empty(changed);
    }
}
