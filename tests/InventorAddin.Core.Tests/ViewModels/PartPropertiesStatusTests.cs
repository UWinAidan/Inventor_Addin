using System.Globalization;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class PartPropertiesStatusTests
{
    private sealed class FakeTarget : IPropertyWriteTarget
    {
        public List<IReadOnlyList<PropertyWrite>> Calls { get; } = new();

        public void Apply(IReadOnlyList<PropertyWrite> writes) => Calls.Add(writes);
    }

    private sealed class NoAssigner : IPartNumberAssigner
    {
        public PartNumberAssignment Assign() => throw new InvalidOperationException();
    }

    private static PartPropertiesSnapshot Part(string? revision = "A", int? design = 1, string? user = "WORK IN PROGRESS", EditBlock block = EditBlock.None) => new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = DocumentKind.Part,
        PartNumber = "AWB-10001",
        Description = "Bracket",
        PartType = "M",
        Designer = "Alex Example",
        Author = "Alex Example",
        Detailer = "Sam Sample",
        Cost = 12.5m,
        RevisionNumber = revision,
        DesignStatus = design,
        UserStatus = user,
        EditBlock = block,
    };

    private static PartPropertiesViewModel Create(PartPropertiesSnapshot snapshot, FakeTarget? target = null) =>
        new(snapshot, target ?? new FakeTarget(), _ => true, new NoAssigner(), "Test Designer", [], CultureInfo.InvariantCulture);

    private static PropertyWrite Design(int n) => PropertyWrite.SetInteger(PartPropertyStorage.DesignStatus, n);

    private static PropertyWrite User(string t) => PropertyWrite.SetText(PartPropertyStorage.UserStatus, t);

    [Fact]
    public void Matching_NoPendingChange()
    {
        var vm = Create(Part());
        Assert.Equal(PartStatus.WorkInProgress, vm.Status);
        Assert.Equal("A", vm.RevisionDisplay);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Released_Apply_WritesBothInOneCall()
    {
        var target = new FakeTarget();
        var vm = Create(Part(), target);

        vm.Status = PartStatus.Released;
        Assert.True(vm.HasChanges);
        vm.ApplyCommand.Execute(null);

        Assert.Equal([[Design(3), User("RELEASED")]], target.Calls);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void UserStatusNotMatching_IsAPrefilledPendingChange()
    {
        var target = new FakeTarget();
        var vm = Create(Part(design: 3, user: null), target);

        Assert.True(vm.HasChanges);
        Assert.Equal(PartStatus.Released, vm.Status);
        vm.ApplyCommand.Execute(null);
        Assert.Equal([[User("RELEASED")]], target.Calls);
    }

    [Fact]
    public void WrongUserStatusText_IsRewrittenAfterDesignStatus()
    {
        var target = new FakeTarget();
        var vm = Create(Part(design: 2, user: "RELEASED"), target);

        vm.ApplyCommand.Execute(null);
        Assert.Equal([[User("PENDING")]], target.Calls);
    }

    [Fact]
    public void DesignStatusNotSet_FollowsAUserStatusWord()
    {
        var target = new FakeTarget();
        var vm = Create(Part(design: 0, user: "PENDING"), target);

        Assert.Equal(PartStatus.Pending, vm.Status);
        vm.ApplyCommand.Execute(null);
        Assert.Equal([[Design(2)]], target.Calls);
    }

    [Fact]
    public void DesignStatusNotSet_OtherTextGivesWorkInProgress()
    {
        var target = new FakeTarget();
        var vm = Create(Part(design: 0, user: "Draft"), target);

        Assert.Equal(PartStatus.WorkInProgress, vm.Status);
        vm.ApplyCommand.Execute(null);
        Assert.Equal([[Design(1), User("WORK IN PROGRESS")]], target.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public void UnreadableDesignStatus_ShowsNoStatusAndWritesNothing(int? design)
    {
        var vm = Create(Part(design: design, user: null));
        Assert.Null(vm.Status);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Revise_IsAPendingChangeUntilApply()
    {
        var target = new FakeTarget();
        var vm = Create(Part(revision: "A"), target);

        vm.ReviseCommand.Execute(null);
        Assert.Equal("B", vm.RevisionDisplay);
        Assert.True(vm.HasChanges);
        Assert.Empty(target.Calls);

        vm.ApplyCommand.Execute(null);
        Assert.Equal([[PropertyWrite.SetText(PartPropertyStorage.RevisionNumber, "B")]], target.Calls);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Revise_TwiceStepsTwice_AndBlankBecomesA()
    {
        var vm = Create(Part(revision: "H"));
        vm.ReviseCommand.Execute(null);
        vm.ReviseCommand.Execute(null);
        Assert.Equal("K", vm.RevisionDisplay);

        var blank = Create(Part(revision: ""));
        Assert.Equal(PartPropertiesViewModel.NoRevisionText, blank.RevisionDisplay);
        blank.ReviseCommand.Execute(null);
        Assert.Equal("A", blank.RevisionDisplay);
    }

    [Fact]
    public void UnreadableRevision_ReviseIsDisabled_AndApplyDoesNotWriteIt()
    {
        var target = new FakeTarget();
        var vm = Create(Part(revision: null, design: 3, user: null), target);

        Assert.False(vm.CanRevise);
        Assert.False(vm.ReviseCommand.CanExecute(null));
        vm.ReviseCommand.Execute(null);
        vm.ApplyCommand.Execute(null);
        Assert.Equal([[User("RELEASED")]], target.Calls);
    }

    [Fact]
    public void FileThatCannotBeEdited_ReviseDoesNothing()
    {
        var vm = Create(Part(design: 3, user: null, block: EditBlock.ReadOnlyFile));

        Assert.False(vm.ReviseCommand.CanExecute(null));
        vm.ReviseCommand.Execute(null);
        Assert.Equal("A", vm.RevisionDisplay);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PlanWithNullStatusAndRevision_LeavesThemAlone()
    {
        var writes = PartPropertiesWritePlan.Build(
            Part(design: 3, user: null), new PartEditedValues("M", "Alex Example", "Sam Sample", 12.5m));
        Assert.Empty(writes);
    }
}
