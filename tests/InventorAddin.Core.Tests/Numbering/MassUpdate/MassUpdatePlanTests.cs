using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Numbering.MassUpdate;

public sealed class MassUpdatePlanTests
{
    private const string User = "testlogin";
    private const string Designer = "Test Designer";

    private static readonly MassUpdateActions All =
        MassUpdateActions.Register | MassUpdateActions.Assign | MassUpdateActions.ReplaceNames;

    private static readonly PartRegister Register = new(
    [
        new RegisterEntry(
            new PartNumber(10001), "test plate", "M", DocumentKind.Part, @"C:\Work\AWB-10001,test_plate.ipt",
            new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero), "someone", "TESTPC"),
    ]);

    private static ScannedFileState State(
        string path,
        string partNumber = "",
        string designer = "Alex Example",
        string detailer = "Alex Example",
        string partType = "M",
        DocumentKind kind = DocumentKind.Part,
        bool open = false) =>
        ScannedFileState.For(new ScannedFile(path, partNumber, designer, detailer, partType, kind, open), Register, [User]);

    [Fact]
    public void NothingTickedGivesAnEmptyPlan()
    {
        var plan = MassUpdatePlan.Build([(State(@"C:\Work\a.ipt"), MassUpdateActions.None)], Designer);

        Assert.Empty(plan.Updates);
        Assert.Empty(plan.Duplicates);
    }

    [Fact]
    public void RegisterTickedOnAWellFormedNumberRegistersIt()
    {
        var plan = MassUpdatePlan.Build([(State(@"C:\Work\a.ipt", "AWB-10007"), MassUpdateActions.Register)], Designer);

        var update = Assert.Single(plan.Updates);
        Assert.Equal(new RegisterExisting(new PartNumber(10007)), update.Register);
        Assert.Empty(update.Writes);
    }

    [Fact]
    public void AssignTickedOnAFileWithoutANumberAssignsANewOne()
    {
        var plan = MassUpdatePlan.Build([(State(@"C:\Work\a.ipt", ""), MassUpdateActions.Assign)], Designer);

        var update = Assert.Single(plan.Updates);
        Assert.Same(AssignNew.Instance, update.Register);
        Assert.Empty(update.Writes);
    }

    [Fact]
    public void ActionsThatDoNotApplyAreIgnored()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\registered.ipt", "AWB-10001"), All),
            (State(@"C:\Work\unregistered.ipt", "AWB-10002"), MassUpdateActions.Assign),
            (State(@"C:\Work\none.ipt", ""), MassUpdateActions.Register),
        ], Designer);

        Assert.Empty(plan.Updates);
    }

    [Fact]
    public void AllTickedTakesOnlyTheActionsThatApply()
    {
        var plan = MassUpdatePlan.Build([(State(@"C:\Work\a.ipt", "AWB-10002", designer: User), All)], Designer);

        var update = Assert.Single(plan.Updates);
        Assert.Equal(new RegisterExisting(new PartNumber(10002)), update.Register);
        Assert.Equal([PropertyWrite.SetText(PartPropertyStorage.Designer, Designer)], update.Writes);
    }

    [Fact]
    public void FileOpenInInventorGetsNoUpdate()
    {
        var plan = MassUpdatePlan.Build([(State(@"C:\Work\a.ipt", "", designer: User, open: true), All)], Designer);

        Assert.Empty(plan.Updates);
    }

    [Fact]
    public void ReplaceNamesSetsBothWhenBothAreTheWindowsUser()
    {
        var plan = MassUpdatePlan.Build(
            [(State(@"C:\Work\a.ipt", "AWB-10001", designer: "TestLogin", detailer: " testlogin"), MassUpdateActions.ReplaceNames)],
            $"  {Designer} ");

        var update = Assert.Single(plan.Updates);
        Assert.Null(update.Register);
        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Designer, Designer),
                PropertyWrite.SetText(PartPropertyStorage.Detailer, Designer),
            ],
            update.Writes);
    }

    [Fact]
    public void ReplaceNamesSetsAnOtherNameThatMeansMe()
    {
        var state = ScannedFileState.For(
            new ScannedFile(@"C:\Work\a.ipt", "AWB-10001", "Sam Sample", " t.login", "M", DocumentKind.Part),
            Register,
            PersonNames.NamesThatMeanMe(User, "T.Login"));

        var plan = MassUpdatePlan.Build([(state, MassUpdateActions.ReplaceNames)], Designer);

        var update = Assert.Single(plan.Updates);
        var write = Assert.Single(update.Writes);
        Assert.Equal((PartPropertyStorage.Detailer, Designer), (write.Location, write.TextValue));
    }

    [Fact]
    public void ReplaceNamesLeavesADetailerThatIsNotTheWindowsUser()
    {
        var plan = MassUpdatePlan.Build(
            [(State(@"C:\Work\a.ipt", "AWB-10001", designer: User, detailer: "Sam Sample"), MassUpdateActions.ReplaceNames)],
            Designer);

        Assert.Equal([PropertyWrite.SetText(PartPropertyStorage.Designer, Designer)], Assert.Single(plan.Updates).Writes);
    }

    [Fact]
    public void ReplaceNamesLeavesADesignerThatIsNotTheWindowsUser()
    {
        var plan = MassUpdatePlan.Build(
            [(State(@"C:\Work\a.ipt", "AWB-10001", designer: "Sam Sample", detailer: User), MassUpdateActions.ReplaceNames)],
            Designer);

        Assert.Equal([PropertyWrite.SetText(PartPropertyStorage.Detailer, Designer)], Assert.Single(plan.Updates).Writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ReplaceNamesIsIgnoredWithABlankDefaultDesigner(string? defaultDesigner)
    {
        var plan = MassUpdatePlan.Build(
            [(State(@"C:\Work\a.ipt", "", designer: User, detailer: User), MassUpdateActions.Assign | MassUpdateActions.ReplaceNames)],
            defaultDesigner);

        var update = Assert.Single(plan.Updates);
        Assert.Same(AssignNew.Instance, update.Register);
        Assert.Empty(update.Writes);
    }

    [Fact]
    public void UpdatesKeepTheOrderGiven()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\c.ipt"), MassUpdateActions.Assign),
            (State(@"C:\Work\a.ipt"), MassUpdateActions.Assign),
            (State(@"C:\Work\b.ipt"), MassUpdateActions.None),
        ], Designer);

        Assert.Equal([@"C:\Work\c.ipt", @"C:\Work\a.ipt"], plan.Updates.Select(u => u.File.FullPath));
    }

    [Fact]
    public void SecondFileWithTheSameUnregisteredNumberIsReportedAsADuplicate()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\a.ipt", "AWB-10005"), MassUpdateActions.Register),
            (State(@"C:\Work\b.ipt", "awb-10005"), MassUpdateActions.Register),
        ], Designer);

        var update = Assert.Single(plan.Updates);
        Assert.Equal(@"C:\Work\a.ipt", update.File.FullPath);
        Assert.Equal(
            [new MassUpdateDuplicate(new PartNumber(10005), @"C:\Work\a.ipt", @"C:\Work\b.ipt")],
            plan.Duplicates);
    }

    [Fact]
    public void FirstCarrierKeepsTheNumberEvenWhenNotTicked()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\a.ipt", "AWB-10005"), MassUpdateActions.None),
            (State(@"C:\Work\b.ipt", "AWB-10005"), MassUpdateActions.Register),
        ], Designer);

        Assert.Empty(plan.Updates);
        Assert.Equal(@"C:\Work\a.ipt", Assert.Single(plan.Duplicates).FirstPath);
    }

    [Fact]
    public void UntickedSecondCarrierIsNotReported()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\a.ipt", "AWB-10005"), MassUpdateActions.Register),
            (State(@"C:\Work\b.ipt", "AWB-10005"), MassUpdateActions.None),
        ], Designer);

        Assert.Single(plan.Updates);
        Assert.Empty(plan.Duplicates);
    }

    [Fact]
    public void DuplicateStillGetsItsNameReplacement()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\a.ipt", "AWB-10005"), MassUpdateActions.Register),
            (State(@"C:\Work\b.ipt", "AWB-10005", designer: User), MassUpdateActions.Register | MassUpdateActions.ReplaceNames),
        ], Designer);

        Assert.Equal(2, plan.Updates.Count);
        Assert.Null(plan.Updates[1].Register);
        Assert.Single(plan.Updates[1].Writes);
        Assert.Single(plan.Duplicates);
    }

    [Fact]
    public void DifferentUnregisteredNumbersAreBothRegistered()
    {
        var plan = MassUpdatePlan.Build(
        [
            (State(@"C:\Work\a.ipt", "AWB-10005"), MassUpdateActions.Register),
            (State(@"C:\Work\b.ipt", "AWB-10006"), MassUpdateActions.Register),
        ], Designer);

        Assert.Equal(2, plan.Updates.Count);
        Assert.Empty(plan.Duplicates);
    }

    [Fact]
    public void EntryForRegisteringAnExistingNumberCarriesTheFileDetails()
    {
        var plan = MassUpdatePlan.Build(
            [(State(@"C:\Work\Sub\AWB-10005,side_frame.iam", "AWB-10005", partType: " a ", kind: DocumentKind.Assembly), MassUpdateActions.Register)],
            Designer);
        var update = Assert.Single(plan.Updates);
        var at = new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

        var entry = update.EntryFor(((RegisterExisting)update.Register!).Number, at, "testlogin", "TESTPC");

        Assert.Equal(
            new RegisterEntry(
                new PartNumber(10005), "side frame", "A", DocumentKind.Assembly, @"C:\Work\Sub\AWB-10005,side_frame.iam",
                at, "testlogin", "TESTPC"),
            entry);
    }

    [Fact]
    public void EntryForKeepsAnUnlistedPartTypeTrimmed()
    {
        var update = Assert.Single(MassUpdatePlan.Build(
            [(State(@"C:\Work\old_plate.ipt", "", partType: " X1 "), MassUpdateActions.Assign)], Designer).Updates);

        var entry = update.EntryFor(new PartNumber(10009), DateTimeOffset.UnixEpoch, "u", "c");

        Assert.Equal("X1", entry.PartType);
        Assert.Equal("old plate", entry.Name);
    }

    [Fact]
    public void PartNumberWriteSetsThePartNumber()
    {
        Assert.Equal(
            PropertyWrite.SetText(PartPropertyStorage.PartNumber, "AWB-10012"),
            FileUpdate.PartNumberWrite(new PartNumber(10012)));
    }
}
