using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;

namespace InventorAddin.Core.Tests.Numbering.MassUpdate;

public sealed class ScannedFileStateTests
{
    private const string User = "testlogin";
    private const string OtherName = "T.Login";

    private static readonly IReadOnlyList<string> Me = [User];

    private static readonly PartRegister Register = new(
    [
        new RegisterEntry(
            new PartNumber(10001), "test plate", "M", DocumentKind.Part, @"C:\Work\test_plate,AWB-10001.ipt",
            new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero), "someone", "TESTPC"),
    ]);

    private static ScannedFile File(
        string partNumber = "",
        string designer = "Alex Example",
        string detailer = "Alex Example",
        bool open = false,
        string? error = null,
        string author = "Alex Example") =>
        new(@"C:\Work\test_bracket.ipt", partNumber, designer, detailer, "M", DocumentKind.Part, open, error, author);

    [Fact]
    public void RegisteredNumberOffersNoNumberAction()
    {
        var state = ScannedFileState.For(File("AWB-10001"), Register, Me);

        Assert.Equal(NumberState.Registered, state.NumberState);
        Assert.Equal(new PartNumber(10001), state.Number);
        Assert.Equal(MassUpdateActions.None, state.Actions);
        Assert.Null(state.BlockedReason);
    }

    [Theory]
    [InlineData("AWB-10002")]
    [InlineData(" awb-10002 ")]
    public void WellFormedUnregisteredNumberOffersRegister(string partNumber)
    {
        var state = ScannedFileState.For(File(partNumber), Register, Me);

        Assert.Equal(NumberState.WellFormedUnregistered, state.NumberState);
        Assert.Equal(new PartNumber(10002), state.Number);
        Assert.Equal(MassUpdateActions.Register, state.Actions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("test_bracket")]
    [InlineData("AWB-1234")]
    [InlineData("OLD-0042")]
    public void NoNumberOffersAssign(string partNumber)
    {
        var state = ScannedFileState.For(File(partNumber), Register, Me);

        Assert.Equal(NumberState.None, state.NumberState);
        Assert.Null(state.Number);
        Assert.Equal(MassUpdateActions.Assign, state.Actions);
    }

    [Fact]
    public void DesignerEqualToWindowsUserOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", designer: " TestLogin "), Register, Me);

        Assert.True(state.DesignerIsMe);
        Assert.False(state.DetailerIsMe);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void DetailerEqualToWindowsUserOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", detailer: "TESTLOGIN"), Register, Me);

        Assert.False(state.DesignerIsMe);
        Assert.True(state.DetailerIsMe);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void AuthorEqualToWindowsUserOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", author: " TestLogin "), Register, Me);

        Assert.False(state.DesignerIsMe);
        Assert.False(state.DetailerIsMe);
        Assert.True(state.AuthorIsMe);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void AuthorEqualToAnOtherNameOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", author: "t.login"), Register, [User, OtherName]);

        Assert.True(state.AuthorIsMe);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void AuthorNotInTheListIsNotMe()
    {
        var state = ScannedFileState.For(File("AWB-10001", author: "Sam Sample"), Register, Me);

        Assert.False(state.AuthorIsMe);
        Assert.Equal(MassUpdateActions.None, state.Actions);
    }

    [Fact]
    public void AuthorInAFileOpenInInventorOffersNothing()
    {
        var state = ScannedFileState.For(File("AWB-10001", open: true, author: User), Register, Me);

        Assert.True(state.AuthorIsMe);
        Assert.Equal(MassUpdateActions.None, state.Actions);
    }

    [Fact]
    public void NumberAndNameActionsCombine()
    {
        var state = ScannedFileState.For(File("", designer: User, detailer: User), Register, Me);

        Assert.True(state.DesignerIsMe);
        Assert.True(state.DetailerIsMe);
        Assert.Equal(MassUpdateActions.Assign | MassUpdateActions.ReplaceNames, state.Actions);
        Assert.True(state.Offers(MassUpdateActions.Assign));
        Assert.True(state.Offers(MassUpdateActions.ReplaceNames));
        Assert.False(state.Offers(MassUpdateActions.Register));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankWindowsUserNeverMatches(string user)
    {
        var state = ScannedFileState.For(File("AWB-10001", designer: "", detailer: " "), Register, [user]);

        Assert.False(state.DesignerIsMe);
        Assert.False(state.DetailerIsMe);
    }

    [Theory]
    [InlineData("T.Login", "Alex Example", true, false)]
    [InlineData("Alex Example", " t.login ", false, true)]
    [InlineData("t.login", "TESTLOGIN", true, true)]
    public void OtherNamesThatMeanMeOfferReplaceNames(string designer, string detailer, bool designerIsMe, bool detailerIsMe)
    {
        var state = ScannedFileState.For(File("AWB-10001", designer, detailer), Register, [User, OtherName]);

        Assert.Equal(designerIsMe, state.DesignerIsMe);
        Assert.Equal(detailerIsMe, state.DetailerIsMe);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void ANameNotInTheListIsNotMe()
    {
        var state = ScannedFileState.For(File("AWB-10001", designer: OtherName), Register, Me);

        Assert.False(state.DesignerIsMe);
        Assert.Equal(MassUpdateActions.None, state.Actions);
    }

    [Fact]
    public void NullNamesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ScannedFileState.For(File(), Register, null!));
    }

    [Fact]
    public void FileOpenInInventorOffersNoActionAndSaysWhy()
    {
        var state = ScannedFileState.For(File("", designer: User, open: true), Register, Me);

        Assert.Equal(MassUpdateActions.None, state.Actions);
        Assert.Equal("Open in Inventor; close it first.", state.BlockedReason);
        Assert.Equal(NumberState.None, state.NumberState);
        Assert.True(state.DesignerIsMe);
    }

    [Fact]
    public void FileWithReadErrorOffersNoActionAndGivesTheError()
    {
        var state = ScannedFileState.For(File("", designer: User, error: "The file could not be opened."), Register, Me);

        Assert.Equal(MassUpdateActions.None, state.Actions);
        Assert.Equal("The file could not be opened.", state.BlockedReason);
    }

    [Fact]
    public void OpenInInventorIsGivenBeforeAReadError()
    {
        var state = ScannedFileState.For(File(open: true, error: "Some error"), Register, Me);

        Assert.Equal(ScannedFileState.OpenInInventorText, state.BlockedReason);
    }

    [Fact]
    public void BlankReadErrorIsNoError()
    {
        var state = ScannedFileState.For(File(error: "  "), Register, Me);

        Assert.Null(state.BlockedReason);
        Assert.Equal(MassUpdateActions.Assign, state.Actions);
    }

    [Fact]
    public void NullTextFieldsBecomeEmpty()
    {
        var file = new ScannedFile(null!, null!, null!, null!, null!, DocumentKind.Part, Author: null!);

        Assert.Equal("", file.FullPath);
        Assert.Equal("", file.PartNumber);
        Assert.Equal("", file.Designer);
        Assert.Equal("", file.Detailer);
        Assert.Equal("", file.PartType);
        Assert.Equal("", file.Author);
        Assert.False(file.OpenInInventor);
        Assert.Null(file.ReadError);
    }
}
