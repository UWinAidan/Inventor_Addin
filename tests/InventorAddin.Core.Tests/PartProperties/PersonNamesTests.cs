using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PersonNamesTests
{
    private const string WindowsUser = "testuser";
    private const string Person = "A. Person";

    private static readonly IReadOnlyList<string> JustTheUser = [WindowsUser];

    // NamesThatMeanMe

    [Fact]
    public void NamesThatMeanMe_IsTheWindowsUserThenEachOtherName()
    {
        Assert.Equal(
            ["testuser", "T.User", "tu"],
            PersonNames.NamesThatMeanMe("testuser", "T.User, tu"));
    }

    [Fact]
    public void NamesThatMeanMe_TrimsAndDropsBlankEntries()
    {
        Assert.Equal(
            ["testuser", "T.User", "tu", "Test User"],
            PersonNames.NamesThatMeanMe("  testuser ", " ,T.User,, tu ,  , Test User ,"));
    }

    [Fact]
    public void NamesThatMeanMe_DropsDuplicatesIgnoringCase_KeepingTheFirst()
    {
        Assert.Equal(
            ["testuser", "T.User"],
            PersonNames.NamesThatMeanMe("testuser", "TESTUSER, T.User, t.user, testuser"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", " , ,, ")]
    public void NamesThatMeanMe_IsEmptyWhenEverythingIsBlank(string? windowsUserName, string? otherNames)
    {
        Assert.Empty(PersonNames.NamesThatMeanMe(windowsUserName, otherNames));
    }

    [Fact]
    public void NamesThatMeanMe_WithoutAWindowsUserName_IsTheOtherNames()
    {
        Assert.Equal(["T.User"], PersonNames.NamesThatMeanMe(null, "T.User"));
    }

    [Fact]
    public void NamesThatMeanMe_WithNoOtherNames_IsTheWindowsUser()
    {
        Assert.Equal(["testuser"], PersonNames.NamesThatMeanMe("testuser", null));
    }

    // MeansMe

    [Theory]
    [InlineData("testuser")]
    [InlineData("TESTUSER")]
    [InlineData("  TestUser  ")]
    [InlineData("T.User")]
    [InlineData(" t.user ")]
    public void MeansMe_IsTrueForAnyNameIgnoringCaseAndSpaces(string value)
    {
        Assert.True(PersonNames.MeansMe(value, ["testuser", "T.User"]));
    }

    [Theory]
    [InlineData("testuser2")]
    [InlineData("test user")]
    [InlineData("T. User")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MeansMe_IsFalseForOtherValues(string? value)
    {
        Assert.False(PersonNames.MeansMe(value, ["testuser", "T.User"]));
    }

    [Fact]
    public void MeansMe_TrimsTheNamesToo()
    {
        Assert.True(PersonNames.MeansMe("testuser", ["  TestUser "]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MeansMe_BlankNamesNeverMatch(string? value)
    {
        Assert.False(PersonNames.MeansMe(value, ["", "   "]));
        Assert.False(PersonNames.MeansMe(value, []));
    }

    [Fact]
    public void MeansMe_RejectsNullNames()
    {
        Assert.Throws<ArgumentNullException>(() => PersonNames.MeansMe("testuser", null!));
    }

    // IsMePlaceholder

    [Theory]
    [InlineData("testuser", "testuser")]
    [InlineData("TESTUSER", "testuser")]
    [InlineData("  TestUser  ", "testuser")]
    [InlineData("testuser", "  TestUser ")]
    public void ValueEqualToTheWindowsUser_IsAPlaceholder(string value, string windowsUserName)
    {
        Assert.True(PersonNames.IsMePlaceholder(value, [windowsUserName], Person));
    }

    [Theory]
    [InlineData("T.User")]
    [InlineData("tu")]
    public void ValueEqualToAnOtherName_IsAPlaceholder(string value)
    {
        Assert.True(PersonNames.IsMePlaceholder(value, PersonNames.NamesThatMeanMe(WindowsUser, "T.User, TU"), Person));
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
        Assert.False(PersonNames.IsMePlaceholder(value, JustTheUser, Person));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankWindowsUserName_IsNeverMatched(string? windowsUserName)
    {
        var names = PersonNames.NamesThatMeanMe(windowsUserName, null);
        Assert.False(PersonNames.IsMePlaceholder(windowsUserName, names, Person));
        Assert.False(PersonNames.IsMePlaceholder(WindowsUser, names, Person));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDefaultDesigner_KeepsTheName(string? defaultDesigner)
    {
        Assert.False(PersonNames.IsMePlaceholder(WindowsUser, JustTheUser, defaultDesigner));
        Assert.False(PersonNames.IsMePlaceholder("T.User", ["testuser", "T.User"], defaultDesigner));
    }
}
