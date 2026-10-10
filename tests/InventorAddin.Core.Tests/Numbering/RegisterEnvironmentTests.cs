using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

/// <summary>Spec 02, "AssignedBy is the person".</summary>
public sealed class RegisterEnvironmentTests
{
    [Theory]
    [InlineData("A. Person", "testuser", "A. Person")]
    [InlineData("  A. Person  ", "testuser", "A. Person")]
    [InlineData("", "testuser", "testuser")]
    [InlineData("   ", " testuser ", "testuser")]
    [InlineData(null, "testuser", "testuser")]
    [InlineData("A. Person", null, "A. Person")]
    [InlineData("A. Person", "", "A. Person")]
    [InlineData("", "", "")]
    [InlineData(" ", " ", "")]
    [InlineData(null, null, "")]
    public void AssignedByFor_IsTheDefaultDesigner_OrTheWindowsUserWhenItIsBlank(
        string? defaultDesigner, string? windowsUserName, string expected)
    {
        Assert.Equal(expected, RegisterEnvironment.AssignedByFor(defaultDesigner, windowsUserName));
    }
}
