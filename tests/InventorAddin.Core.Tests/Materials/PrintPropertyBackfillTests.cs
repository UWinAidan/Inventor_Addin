using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Materials;

public sealed class PrintPropertyBackfillTests
{
    private static IReadOnlyList<PropertyWrite> Run(
        string? finish = null, string? spec = null, string? revision = "B", int? designStatus = 1, string? userStatus = "PENDING") =>
        PrintPropertyBackfill.For(finish, null, spec, revision, designStatus, userStatus, [], PrintUnits.Metric);

    private static PropertyWrite Rev => PropertyWrite.SetText(PartPropertyStorage.RevisionNumber, "A");

    private static PropertyWrite Design(int value) => PropertyWrite.SetInteger(PartPropertyStorage.DesignStatus, value);

    private static PropertyWrite User(string text) => PropertyWrite.SetText(PartPropertyStorage.UserStatus, text);

    [Fact]
    public void EverythingSet_WritesNothing()
    {
        Assert.Empty(Run(finish: "Old coat", spec: "Old coat"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingRevision_WritesA(string? revision)
    {
        Assert.Equal([Rev], Run(revision: revision));
    }

    [Fact]
    public void SetRevision_IsLeftAlone()
    {
        Assert.Empty(Run(revision: "C"));
    }

    [Theory]
    [InlineData(1, "WORK IN PROGRESS")]
    [InlineData(2, "PENDING")]
    [InlineData(3, "RELEASED")]
    public void MissingUserStatus_GetsWordsForDesignStatus(int designStatus, string words)
    {
        Assert.Equal([User(words)], Run(designStatus: designStatus, userStatus: null));
    }

    [Fact]
    public void BlankUserStatus_CountsAsMissing()
    {
        Assert.Equal([User("RELEASED")], Run(designStatus: 3, userStatus: "  "));
    }

    [Fact]
    public void SetUserStatus_IsLeftAlone()
    {
        Assert.Empty(Run(designStatus: 3, userStatus: "MY WORDS"));
    }

    [Fact]
    public void DesignStatusNotSet_BecomesWorkInProgressForBoth()
    {
        Assert.Equal([Design(1), User("WORK IN PROGRESS")], Run(designStatus: 0, userStatus: null));
    }

    [Fact]
    public void DesignStatusNotSet_KeepsASetUserStatus()
    {
        Assert.Equal([Design(1)], Run(designStatus: 0, userStatus: "PENDING"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(4)]
    [InlineData(-1)]
    public void UnreadableOrUnknownDesignStatus_WritesNoStatus(int? designStatus)
    {
        Assert.Empty(Run(designStatus: designStatus, userStatus: null));
    }

    [Fact]
    public void Everything_Missing_ReturnsOneListInOrder()
    {
        var list = new[]
        {
            new Finish("TF1", "Test finish", FinishKind.Plating, [], null, "0.013", "0.025", true, "TEST NOTE.", null),
        };

        var writes = PrintPropertyBackfill.For("Test finish", null, null, null, 0, null, list, PrintUnits.Metric);

        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.FinishSpec, "Test finish, 13-25 µm"),
                Rev,
                Design(1),
                User("WORK IN PROGRESS"),
            ],
            writes);
    }
}

public sealed class PartStatusTests
{
    [Theory]
    [InlineData(PartStatus.WorkInProgress, 1, "WORK IN PROGRESS")]
    [InlineData(PartStatus.Pending, 2, "PENDING")]
    [InlineData(PartStatus.Released, 3, "RELEASED")]
    public void Values_MatchInventor(PartStatus status, int number, string words)
    {
        Assert.Equal(number, PartStatuses.DesignStatusOf(status));
        Assert.Equal(words, PartStatuses.UserStatusText(status));
        Assert.Equal(status, PartStatuses.FromDesignStatus(number));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(4)]
    public void FromDesignStatus_UndefinedNumber_IsNull(int? number)
    {
        Assert.Null(PartStatuses.FromDesignStatus(number));
    }

    [Fact]
    public void WritesFor_SetsDesignStatusThenWords()
    {
        Assert.Equal(
            [
                PropertyWrite.SetInteger(PartPropertyStorage.DesignStatus, 2),
                PropertyWrite.SetText(PartPropertyStorage.UserStatus, "PENDING"),
            ],
            PartStatuses.WritesFor(PartStatus.Pending));
    }

    [Fact]
    public void SetInteger_RejectsATextLocation()
    {
        Assert.Throws<ArgumentException>(() => PropertyWrite.SetInteger(PartPropertyStorage.UserStatus, 1));
        Assert.Throws<ArgumentException>(() => PropertyWrite.SetText(PartPropertyStorage.DesignStatus, "1"));
    }
}
