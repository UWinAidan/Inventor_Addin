using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;

namespace InventorAddin.Core.Tests.Numbering.MassUpdate;

public sealed class ScannedFileStateTests
{
    private const string User = "testlogin";

    private static readonly PartRegister Register = new(
    [
        new RegisterEntry(
            new PartNumber(10001), "test plate", "M", DocumentKind.Part, @"C:\Work\AWB-10001,test_plate.ipt",
            new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero), "someone", "TESTPC"),
    ]);

    private static ScannedFile File(
        string partNumber = "",
        string designer = "Alex Example",
        string detailer = "Alex Example",
        bool open = false,
        string? error = null) =>
        new(@"C:\Work\test_bracket.ipt", partNumber, designer, detailer, "M", DocumentKind.Part, open, error);

    [Fact]
    public void RegisteredNumberOffersNoNumberAction()
    {
        var state = ScannedFileState.For(File("AWB-10001"), Register, User);

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
        var state = ScannedFileState.For(File(partNumber), Register, User);

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
        var state = ScannedFileState.For(File(partNumber), Register, User);

        Assert.Equal(NumberState.None, state.NumberState);
        Assert.Null(state.Number);
        Assert.Equal(MassUpdateActions.Assign, state.Actions);
    }

    [Fact]
    public void DesignerEqualToWindowsUserOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", designer: " TestLogin "), Register, User);

        Assert.True(state.DesignerIsWindowsUser);
        Assert.False(state.DetailerIsWindowsUser);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void DetailerEqualToWindowsUserOffersReplaceNames()
    {
        var state = ScannedFileState.For(File("AWB-10001", detailer: "TESTLOGIN"), Register, User);

        Assert.False(state.DesignerIsWindowsUser);
        Assert.True(state.DetailerIsWindowsUser);
        Assert.Equal(MassUpdateActions.ReplaceNames, state.Actions);
    }

    [Fact]
    public void NumberAndNameActionsCombine()
    {
        var state = ScannedFileState.For(File("", designer: User, detailer: User), Register, User);

        Assert.True(state.DesignerIsWindowsUser);
        Assert.True(state.DetailerIsWindowsUser);
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
        var state = ScannedFileState.For(File("AWB-10001", designer: "", detailer: " "), Register, user);

        Assert.False(state.DesignerIsWindowsUser);
        Assert.False(state.DetailerIsWindowsUser);
    }

    [Fact]
    public void FileOpenInInventorOffersNoActionAndSaysWhy()
    {
        var state = ScannedFileState.For(File("", designer: User, open: true), Register, User);

        Assert.Equal(MassUpdateActions.None, state.Actions);
        Assert.Equal("Open in Inventor; close it first.", state.BlockedReason);
        Assert.Equal(NumberState.None, state.NumberState);
        Assert.True(state.DesignerIsWindowsUser);
    }

    [Fact]
    public void FileWithReadErrorOffersNoActionAndGivesTheError()
    {
        var state = ScannedFileState.For(File("", designer: User, error: "The file could not be opened."), Register, User);

        Assert.Equal(MassUpdateActions.None, state.Actions);
        Assert.Equal("The file could not be opened.", state.BlockedReason);
    }

    [Fact]
    public void OpenInInventorIsGivenBeforeAReadError()
    {
        var state = ScannedFileState.For(File(open: true, error: "Some error"), Register, User);

        Assert.Equal(ScannedFileState.OpenInInventorText, state.BlockedReason);
    }

    [Fact]
    public void BlankReadErrorIsNoError()
    {
        var state = ScannedFileState.For(File(error: "  "), Register, User);

        Assert.Null(state.BlockedReason);
        Assert.Equal(MassUpdateActions.Assign, state.Actions);
    }

    [Fact]
    public void NullTextFieldsBecomeEmpty()
    {
        var file = new ScannedFile(null!, null!, null!, null!, null!, DocumentKind.Part);

        Assert.Equal("", file.FullPath);
        Assert.Equal("", file.PartNumber);
        Assert.Equal("", file.Designer);
        Assert.Equal("", file.Detailer);
        Assert.Equal("", file.PartType);
        Assert.False(file.OpenInInventor);
        Assert.Null(file.ReadError);
    }
}
