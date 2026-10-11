using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class RenameViewModelTests
{
    private const string OldPath = @"C:\Cad\mounting_bracket,AWB-10001.ipt";

    private readonly List<RenameRequest> _requests = [];
    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);

    private RenameViewModel Create(
        string path = OldPath,
        Func<RenameRequest, Action<string>, RenameResult?>? run = null,
        ILog? log = null,
        PartNumber? registeredNumber = null,
        DocumentKind kind = DocumentKind.Part) =>
        new(path, _existing.Contains, run ?? Succeed, log, registeredNumber, kind);

    // The assembly prefix

    [Theory]
    [InlineData(DocumentKind.Assembly)]
    [InlineData(DocumentKind.WeldmentAssembly)]
    public void Assembly_NewName_GetsThePrefix_InThePreviewAndTheRequest(DocumentKind kind)
    {
        const string path = @"C:\Cad\ASSY_GEARBOX,AWB-10006.iam";
        var vm = Create(path: path, kind: kind);

        vm.NewName = "NEW GEARBOX";

        Assert.Equal("ASSY_NEW_GEARBOX,AWB-10006.iam", vm.NewFileNamePreview);
        vm.RenameCommand.Execute(null);
        Assert.Equal(
            new RenameRequest(path, @"C:\Cad\ASSY_NEW_GEARBOX,AWB-10006.iam", "ASSY NEW GEARBOX"),
            Assert.Single(_requests));
    }

    [Fact]
    public void Assembly_NameAlreadyPrefixed_IsNotPrefixedTwice()
    {
        var vm = Create(path: @"C:\Cad\ASSY_GEARBOX,AWB-10006.iam", kind: DocumentKind.Assembly);

        vm.NewName = "assy_new gearbox";

        Assert.Equal("ASSY_NEW_GEARBOX,AWB-10006.iam", vm.NewFileNamePreview);
    }

    [Fact]
    public void Assembly_OnlyThePrefix_IsRefused()
    {
        var vm = Create(path: @"C:\Cad\ASSY_GEARBOX,AWB-10006.iam", kind: DocumentKind.Assembly);

        vm.NewName = "ASSY";

        Assert.Equal(PartFileName.AssemblyNameMissingMessage, vm.ValidationMessage);
        Assert.False(vm.CanRename);
    }

    [Fact]
    public void Part_NewName_IsNotPrefixed()
    {
        var vm = Create();

        vm.NewName = "gearbox";

        Assert.Equal("GEARBOX,AWB-10001.ipt", vm.NewFileNamePreview);
    }

    private RenameResult? Succeed(RenameRequest request, Action<string> progress)
    {
        _requests.Add(request);
        progress("Checking 1 of 1: asm.iam");
        return new RenameResult(
            request, Renamed: true, OldFileDeleted: true, DescriptionWritten: true,
            [new RenameFileResult(@"C:\Cad\asm.iam")], []);
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentException>(() => new RenameViewModel(" ", _ => false, Succeed));
        Assert.Throws<ArgumentNullException>(() => new RenameViewModel(OldPath, null!, Succeed));
        Assert.Throws<ArgumentNullException>(() => new RenameViewModel(OldPath, _ => false, null!));
    }

    [Fact]
    public void OpensWithTheCurrentName_WhichCannotBeUsed()
    {
        var vm = Create();

        Assert.Equal("Rename", vm.HeaderTitle);
        Assert.Equal("mounting_bracket,AWB-10001.ipt", vm.HeaderSubline);
        Assert.Equal("mounting bracket", vm.CurrentName);
        Assert.Equal("mounting bracket", vm.NewName);
        Assert.Equal(new PartNumber(10001), vm.NumberKept);
        Assert.Equal(RenamePlan.SameFileMessage, vm.ValidationMessage);
        Assert.False(vm.CanRename);
        Assert.False(vm.RenameCommand.CanExecute(null));
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void TypingANewName_PreviewsTheFileName_AndEnablesRename()
    {
        var vm = Create();

        vm.NewName = "  base   plate ";

        Assert.Equal("BASE_PLATE,AWB-10001.ipt", vm.NewFileNamePreview);
        Assert.Equal(@"C:\Cad\BASE_PLATE,AWB-10001.ipt", vm.NewFullFileName);
        Assert.Null(vm.ValidationMessage);
        Assert.Equal(string.Empty, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.True(vm.CanRename);
        Assert.True(vm.RenameCommand.CanExecute(null));
    }

    [Fact]
    public void AnInvalidName_ShowsTheProblemAndNoPreview()
    {
        var vm = Create();

        vm.NewName = "a?b";

        Assert.Equal(PartFileName.ValidateName("a?b"), vm.ValidationMessage);
        Assert.Equal(vm.ValidationMessage, vm.StatusText);
        Assert.True(vm.HasError);
        Assert.Equal(string.Empty, vm.NewFileNamePreview);
        Assert.False(vm.CanRename);
    }

    [Fact]
    public void ACommaInAFileWithoutANumber_IsAccepted()
    {
        var vm = Create(@"C:\Old\bracket.ipt");

        vm.NewName = "bracket, left";

        Assert.Null(vm.NumberKept);
        Assert.Null(vm.ValidationMessage);
        Assert.Equal("BRACKET,_LEFT.ipt", vm.NewFileNamePreview);
        Assert.True(vm.CanRename);
    }

    [Fact]
    public void ANameEndingInANumber_InAFileWithoutANumber_IsRefused()
    {
        var vm = Create(@"C:\Old\bracket.ipt");

        vm.NewName = "bracket,AWB-10009";

        Assert.Null(vm.NumberKept);
        Assert.Equal(RenamePlan.NameEndsWithNumberMessage, vm.ValidationMessage);
        Assert.False(vm.CanRename);
    }

    [Fact]
    public void AFileNumberedByGenerate_GetsItsRegisteredNumberInTheNewName_AndMayUseAComma()
    {
        var vm = Create(@"C:\Old\bracket.ipt", registeredNumber: new PartNumber(10007));

        Assert.Equal(new PartNumber(10007), vm.NumberKept);
        // Keeping the name still renames the file: it gains the number.
        Assert.Null(vm.ValidationMessage);
        Assert.Equal("BRACKET,AWB-10007.ipt", vm.NewFileNamePreview);

        vm.NewName = "bracket, left";
        vm.RenameCommand.Execute(null);

        Assert.Equal(@"C:\Old\BRACKET,_LEFT,AWB-10007.ipt", Assert.Single(_requests).NewFullFileName);
    }

    [Fact]
    public void ANameCarryingANumber_WithNoRegisteredPartNumber_KeepsTheNumberInTheName()
    {
        var vm = Create(registeredNumber: null);

        vm.NewName = "base plate";

        Assert.Equal(new PartNumber(10001), vm.NumberKept);
        Assert.Equal("BASE_PLATE,AWB-10001.ipt", vm.NewFileNamePreview);
    }

    [Fact]
    public void ATargetThatExists_IsRefusedBeforeAnythingChanges()
    {
        _existing.Add(@"C:\Cad\BASE_PLATE,AWB-10001.ipt");
        var vm = Create();

        vm.NewName = "base plate";
        vm.RenameCommand.Execute(null);

        Assert.Equal(RenamePlan.TargetExistsMessage("BASE_PLATE,AWB-10001.ipt"), vm.ValidationMessage);
        Assert.Equal("BASE_PLATE,AWB-10001.ipt", vm.NewFileNamePreview);
        Assert.False(vm.CanRename);
        Assert.Empty(_requests);
    }

    [Fact]
    public void Rename_HandsOverTheRequest_ThenShowsTheResult()
    {
        var vm = Create();
        var statuses = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.StatusText))
                statuses.Add(vm.StatusText);
        };
        vm.NewName = "base_plate";

        vm.RenameCommand.Execute(null);

        var request = Assert.Single(_requests);
        Assert.Equal(new RenameRequest(OldPath, @"C:\Cad\BASE_PLATE,AWB-10001.ipt", "BASE PLATE"), request);
        Assert.Contains(RenameViewModel.StartingText, statuses);
        Assert.Contains("Checking 1 of 1: asm.iam", statuses);
        Assert.True(vm.IsDone);
        Assert.False(vm.IsEditing);
        Assert.False(vm.IsRunning);
        Assert.False(vm.CanRename);
        Assert.False(vm.HasError);
        Assert.Equal("Renamed to BASE_PLATE,AWB-10001.ipt. 1 file updated.", vm.StatusText);
        Assert.Equal(RenameViewModel.UpdatedPrefix + @"C:\Cad\asm.iam", vm.ResultText);
        Assert.Equal(new PartRenameOutcome(@"C:\Cad\BASE_PLATE,AWB-10001.ipt", "BASE PLATE", true), vm.Outcome);
    }

    [Fact]
    public void Rename_NotConfirmed_GoesBackToEditing()
    {
        var vm = Create(run: (r, _) =>
        {
            _requests.Add(r);
            return null;
        });
        vm.NewName = "base plate";

        vm.RenameCommand.Execute(null);

        Assert.Single(_requests);
        Assert.True(vm.IsEditing);
        Assert.False(vm.IsDone);
        Assert.True(vm.CanRename);
        Assert.Null(vm.Outcome);
        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public void Rename_RunnerThrows_ShowsTheReason_AndLogs()
    {
        var log = new RecordingLog();
        var vm = Create(run: (_, _) => throw new InvalidOperationException("Inventor is busy"), log: log);
        vm.NewName = "base plate";

        vm.RenameCommand.Execute(null);

        Assert.True(vm.IsDone);
        Assert.True(vm.HasError);
        Assert.Equal(RenameViewModel.RunErrorPrefix + "Inventor is busy", vm.StatusText);
        Assert.Null(vm.Outcome);
        Assert.Single(log.Errors);
    }

    [Fact]
    public void Rename_WithProblems_IsShownAsAnError_AndListsEachFile()
    {
        var vm = Create(run: (r, _) => new RenameResult(
            r, true, OldFileDeleted: false, DescriptionWritten: true,
            [new RenameFileResult(@"C:\Cad\a.iam"), new RenameFileResult(@"C:\Cad\b.idw", "it is read-only")],
            [new RenameNote("The old file was kept.", true)]));
        vm.NewName = "base plate";

        vm.RenameCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Equal("Renamed to BASE_PLATE,AWB-10001.ipt. 1 file updated, 1 not updated. The old file was kept.", vm.StatusText);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                RenameViewModel.UpdatedPrefix + @"C:\Cad\a.iam",
                RenameViewModel.NotUpdatedPrefix + @"C:\Cad\b.idw: it is read-only",
                "The old file was kept."),
            vm.ResultText);
        Assert.NotNull(vm.Outcome);
    }

    [Fact]
    public void Rename_NotRenamed_HasNoOutcome()
    {
        var vm = Create(run: (r, _) => new RenameResult(r, false, false, false, [], [new RenameNote("could not save", true)]));
        vm.NewName = "base plate";

        vm.RenameCommand.Execute(null);

        Assert.True(vm.IsDone);
        Assert.True(vm.HasError);
        Assert.Null(vm.Outcome);
        Assert.Equal("Not renamed. Nothing was changed.", vm.StatusText);
        Assert.Equal("could not save", vm.ResultText);
    }

    [Fact]
    public void NewName_CannotChangeOnceDone()
    {
        var vm = Create();
        vm.NewName = "base plate";
        vm.RenameCommand.Execute(null);

        vm.NewName = "other";

        Assert.Equal("base plate", vm.NewName);
    }

    [Fact]
    public void ResultLines_SaysWhenNothingElseUsedIt()
    {
        var result = new RenameResult(new RenameRequest(OldPath, @"C:\Cad\x.ipt", "x"), true, true, true, [], []);

        Assert.Equal(RenameViewModel.NothingElseText, RenameViewModel.ResultLines(result));
        Assert.Equal("Renamed to x.ipt. 0 files updated.", RenameViewModel.Summary(result));
    }

    [Fact]
    public void CloseButton_IsCancelBefore_CloseAfter_AndDisabledWhileRunning()
    {
        bool? canCloseWhileRunning = null;
        RenameViewModel? vm = null;
        vm = Create(run: (r, p) =>
        {
            canCloseWhileRunning = vm!.CanClose;
            return Succeed(r, p);
        });

        Assert.Equal(RenameViewModel.CancelButtonText, vm.CloseText);
        Assert.True(vm.CanClose);

        vm.NewName = "base plate";
        vm.RenameCommand.Execute(null);

        Assert.False(canCloseWhileRunning);
        Assert.True(vm.CanClose);
        Assert.Equal(RenameViewModel.CloseButtonText, vm.CloseText);
    }

    [Fact]
    public void Close_RaisesCloseRequested()
    {
        var vm = Create();
        var raised = 0;
        vm.CloseRequested += (_, _) => raised++;

        vm.Close();

        Assert.Equal(1, raised);
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = [];

        public void Info(string message) { }

        public void Warn(string message) { }

        public void Error(string message, Exception? ex = null) => Errors.Add(message);
    }
}
