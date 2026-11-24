using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class DesignerBackfillTests
{
    private static PropertyWrite Designer(string v) => PropertyWrite.SetText(PartPropertyStorage.Designer, v);

    private static PropertyWrite Author(string v) => PropertyWrite.SetText(PartPropertyStorage.Author, v);

    [Fact]
    public void LoginDesignerAndAuthor_AreReplaced() =>
        Assert.Equal([Designer("A. Tester"), Author("A. Tester")], DesignerBackfill.For("jtest", "jtest", "jtest", "A. Tester"));

    [Fact]
    public void Match_IgnoresCaseAndSpaces() =>
        Assert.Equal([Designer("A. Tester")], DesignerBackfill.For("  JTest ", "Someone Else", "jtest", " A. Tester "));

    [Fact]
    public void AnotherName_IsLeftAlone() =>
        Assert.Empty(DesignerBackfill.For("B. Other", "B. Other", "jtest", "A. Tester"));

    [Fact]
    public void BlankDesigner_IsLeftAlone() =>
        Assert.Empty(DesignerBackfill.For(null, "", "jtest", "A. Tester"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSettingsName_WritesNothing(string? name) =>
        Assert.Empty(DesignerBackfill.For("jtest", "jtest", "jtest", name));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void BlankLogin_WritesNothing(string? login) =>
        Assert.Empty(DesignerBackfill.For("jtest", "jtest", login, "A. Tester"));

    [Fact]
    public void SettingsNameEqualToStored_WritesNothing() =>
        Assert.Empty(DesignerBackfill.For("jtest", "jtest", "jtest", "jtest"));

    [Fact]
    public void SettingsNameDifferingOnlyInCase_IsWritten() =>
        Assert.Equal([Designer("JTest")], DesignerBackfill.For("jtest", null, "jtest", "JTest"));
}
