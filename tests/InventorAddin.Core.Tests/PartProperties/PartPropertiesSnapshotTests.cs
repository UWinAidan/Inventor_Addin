using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartPropertiesSnapshotTests
{
    [Fact]
    public void Defaults_AreAnEditableNeverSavedFile()
    {
        var snapshot = new PartPropertiesSnapshot();

        Assert.Equal("", snapshot.FullFileName);
        Assert.Equal(EditBlock.None, snapshot.EditBlock);
        Assert.Null(snapshot.WeightDisplay);
        Assert.Null(snapshot.Cost);
    }

    [Fact]
    public void EditBlockMessage_NullForNone()
    {
        Assert.Null(PartPropertiesSnapshot.EditBlockMessage(EditBlock.None));
    }

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile)]
    [InlineData(EditBlock.NotModifiable)]
    public void EditBlockMessage_GivesALineForEveryBlock(EditBlock block)
    {
        Assert.False(string.IsNullOrWhiteSpace(PartPropertiesSnapshot.EditBlockMessage(block)));
    }

    [Fact]
    public void EditBlockMessage_DiffersPerBlock()
    {
        Assert.NotEqual(
            PartPropertiesSnapshot.EditBlockMessage(EditBlock.ReadOnlyFile),
            PartPropertiesSnapshot.EditBlockMessage(EditBlock.NotModifiable));
    }

    [Fact]
    public void EditBlockMessage_CoversEveryEnumValue()
    {
        foreach (var block in Enum.GetValues<EditBlock>())
        {
            var message = PartPropertiesSnapshot.EditBlockMessage(block);
            Assert.Equal(block == EditBlock.None, message is null);
        }
    }
}
