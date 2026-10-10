using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PersonNamesTests
{
    private const string WindowsUser = "testuser";
    private const string Person = "A. Person";

    [Theory]
    [InlineData("testuser", "testuser")]
    [InlineData("TESTUSER", "testuser")]
    [InlineData("  TestUser  ", "testuser")]
    [InlineData("testuser", "  TestUser ")]
    public void ValueEqualToTheWindowsUser_IsAPlaceholder(string value, string windowsUserName)
    {
        Assert.True(PersonNames.IsWindowsUserPlaceholder(value, windowsUserName, Person));
    }

    [Theory]
    [InlineData("testuser2")]
    [InlineData("test user")]
    [InlineData("A. Person")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void OtherValues_AreNotPlaceholders(string? value)
    {
        Assert.False(PersonNames.IsWindowsUserPlaceholder(value, WindowsUser, Person));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankWindowsUserName_IsNeverMatched(string? windowsUserName)
    {
        Assert.False(PersonNames.IsWindowsUserPlaceholder(windowsUserName, windowsUserName, Person));
        Assert.False(PersonNames.IsWindowsUserPlaceholder(WindowsUser, windowsUserName, Person));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDefaultDesigner_KeepsTheName(string? defaultDesigner)
    {
        Assert.False(PersonNames.IsWindowsUserPlaceholder(WindowsUser, WindowsUser, defaultDesigner));
    }
}
