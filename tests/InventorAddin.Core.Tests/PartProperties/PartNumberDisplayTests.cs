using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

/// <summary>
/// The display rule itself is tested on the view-model (PartPropertiesViewModelTests, "Part number display"), since
/// it needs the register's answer. Only the placeholder text is left here.
/// </summary>
public sealed class PartNumberDisplayTests
{
    [Fact]
    public void NotAssignedText()
    {
        Assert.Equal("Not assigned", PartNumberDisplay.NotAssignedText);
    }
}
