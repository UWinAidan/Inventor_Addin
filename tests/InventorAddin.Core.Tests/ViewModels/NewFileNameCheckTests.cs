using InventorAddin.Core.Numbering;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class NewFileNameCheckTests
{
    [Fact]
    public void ValidName_PreviewsWithThePlaceholder()
    {
        var check = NewFileNameCheck.For("  mounting  bracket ", ".ipt", @" C:\Work ");

        Assert.Equal("AWB-?????,mounting_bracket.ipt", check.FileName);
        Assert.Null(check.Problem);
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

        Assert.Equal("AWB-?????,plate.ipt", check.FileName);
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
