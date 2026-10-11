using InventorAddin.Core.Numbering;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class NewFileNameCheckTests
{
    [Fact]
    public void ValidName_PreviewsWithThePlaceholder()
    {
        var check = NewFileNameCheck.For("  mounting  bracket ", ".ipt", @" C:\Work ");

        Assert.Equal("mounting_bracket,AWB-?????.ipt", check.FileName);
        Assert.Null(check.Problem);
    }

    [Theory]
    [InlineData("MOUNTING BRACKET", ".ipt", "MOUNTING_BRACKET,AWB-?????.ipt")]
    [InlineData("FRAME", ".iam", "FRAME,AWB-?????.iam")]
    [InlineData("A,B", ".ipt", "A,B,AWB-?????.ipt")]
    public void Preview_IsNameThenPlaceholderNumber(string name, string extension, string expected)
    {
        Assert.Equal(expected, NewFileNameCheck.For(name, extension, @"C:\Work").FileName);
    }

    [Fact]
    public void NameProblem_ComesFirst_AndClearsThePreview()
    {
        var check = NewFileNameCheck.For("a|b", ".iam", "");

        Assert.Equal(string.Empty, check.FileName);
        Assert.Equal("A part name cannot contain the character '|'.", check.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankFolder_AsksForOne(string? folder)
    {
        var check = NewFileNameCheck.For("plate", ".ipt", folder);

        Assert.Equal("plate,AWB-?????.ipt", check.FileName);
        Assert.Equal(NewFileNameCheck.ChooseFolderMessage, check.Problem);
    }

    [Fact]
    public void LongPath_IsRefused()
    {
        var check = NewFileNameCheck.For(new string('x', 250), ".ipt", @"C:\Work");

        Assert.Equal(PartFileName.ValidatePath(@"C:\Work", check.FileName), check.Problem);
        Assert.NotNull(check.Problem);
    }

    [Fact]
    public void NewPartViewModel_UsesTheSameConstants()
    {
        Assert.Equal(NewFileNameCheck.NumberPlaceholder, NewPartViewModel.NumberPlaceholder);
        Assert.Equal(NewFileNameCheck.ChooseFolderMessage, NewPartViewModel.ChooseFolderMessage);
    }
}
