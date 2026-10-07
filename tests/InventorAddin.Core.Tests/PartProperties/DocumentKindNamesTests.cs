using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class DocumentKindNamesTests
{
    [Theory]
    [InlineData(DocumentKind.Part, "Part")]
    [InlineData(DocumentKind.SheetMetalPart, "Sheet metal part")]
    [InlineData(DocumentKind.Assembly, "Assembly")]
    [InlineData(DocumentKind.WeldmentAssembly, "Weldment")]
    [InlineData(DocumentKind.Drawing, "Drawing")]
    [InlineData(DocumentKind.Presentation, "Presentation")]
    [InlineData(DocumentKind.Unknown, "Document")]
    [InlineData((DocumentKind)99, "Document")]
    public void For_NamesEachKind(DocumentKind kind, string expected)
    {
        Assert.Equal(expected, DocumentKindNames.For(kind));
    }

    [Fact]
    public void For_EveryDefinedKindHasAName()
    {
        foreach (var kind in Enum.GetValues<DocumentKind>().Where(k => k != DocumentKind.Unknown))
        {
            Assert.NotEqual(DocumentKindNames.UnknownText, DocumentKindNames.For(kind));
        }
    }
}
