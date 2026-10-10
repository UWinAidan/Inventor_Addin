using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class PartRegisterTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private static RegisterEntry Entry(int value, string? filePath = null, DateTimeOffset? at = null, string computer = "TESTPC") =>
        new(
            new PartNumber(value),
            "test plate",
            "P",
            DocumentKind.Part,
            filePath ?? $@"C:\Work\AWB-{value},test_plate.ipt",
            at ?? At,
            "testuser",
            computer);

    // Entries, Contains, NextNumber

    [Fact]
    public void EntriesKeepTheOrderGiven()
    {
        var register = new PartRegister([Entry(10003), Entry(10001), Entry(10002)]);

        Assert.Equal([10003, 10001, 10002], register.Entries.Select(e => e.Number.Value));
    }

    [Fact]
    public void RegisterIsNotChangedByChangingTheListItWasMadeFrom()
    {
        var list = new List<RegisterEntry> { Entry(10001) };
        var register = new PartRegister(list);

        list.Add(Entry(10002));

        Assert.Single(register.Entries);
    }

    [Fact]
    public void EmptyRegisterHasNoEntries()
    {
        Assert.Empty(PartRegister.Empty.Entries);
    }

    [Fact]
    public void ContainsIsTrueOnlyForNumbersInTheRegister()
    {
        var register = new PartRegister([Entry(10001), Entry(10005)]);

        Assert.True(register.Contains(new PartNumber(10001)));
        Assert.True(register.Contains(new PartNumber(10005)));
        Assert.False(register.Contains(new PartNumber(10002)));
    }

    [Fact]
    public void NextNumberOfAnEmptyRegisterIsTheFirst()
    {
        Assert.Equal(new PartNumber(10001), PartRegister.Empty.NextNumber());
    }

    [Fact]
    public void NextNumberIsOneMoreThanTheHighestAndDoesNotFillGaps()
    {
        var register = new PartRegister([Entry(10001), Entry(10007), Entry(10003)]);

        Assert.Equal(new PartNumber(10008), register.NextNumber());
    }

    [Fact]
    public void NextNumberThrowsWhenTheSequenceIsFull()
    {
        var register = new PartRegister([Entry(99999)]);

        Assert.Throws<InvalidOperationException>(() => register.NextNumber());
    }

    // HasNumber

    [Theory]
    [InlineData("AWB-10001", true)]
    [InlineData(" awb-10001 ", true)]
    [InlineData("AWB-10002", false)]
    [InlineData("AWB-1", false)]
    [InlineData("AWB-10001,test_plate", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasNumberNeedsTheFormatAndTheRegister(string? partNumber, bool expected)
    {
        var register = new PartRegister([Entry(10001)]);

        Assert.Equal(expected, register.HasNumber(partNumber));
    }

    [Fact]
    public void HasNumberDoesNotCareWhichFileTheEntryNames()
    {
        var register = new PartRegister([Entry(10001, @"C:\Old\Place\somewhere_else.ipt")]);

        Assert.True(register.HasNumber("AWB-10001"));
    }

    // Append

    [Fact]
    public void AppendReturnsANewRegisterWithTheEntryAtTheEnd()
    {
        var original = new PartRegister([Entry(10001)]);

        var appended = original.Append(Entry(10002));

        Assert.Equal([10001, 10002], appended.Entries.Select(e => e.Number.Value));
        Assert.Single(original.Entries);
        Assert.False(original.Contains(new PartNumber(10002)));
        Assert.True(appended.Contains(new PartNumber(10002)));
    }

    [Fact]
    public void AppendKeepsAnEntryWithANumberAlreadyHeld()
    {
        var register = new PartRegister([Entry(10001)]).Append(Entry(10001, @"C:\Work\other.ipt"));

        Assert.Equal(2, register.Entries.Count);
    }

    // Merge

    [Fact]
    public void MergeAppendsPendingEntriesInOrder()
    {
        var shared = new PartRegister([Entry(10001), Entry(10002)]);

        var result = PartRegister.Merge(shared, [Entry(10004), Entry(10003)]);

        Assert.Equal([10001, 10002, 10004, 10003], result.Register.Entries.Select(e => e.Number.Value));
        Assert.Empty(result.Duplicates);
        Assert.Equal(2, shared.Entries.Count);
    }

    [Fact]
    public void MergeWithNothingPendingGivesTheSameEntries()
    {
        var shared = new PartRegister([Entry(10001)]);

        var result = PartRegister.Merge(shared, []);

        Assert.Equal(shared.Entries, result.Register.Entries);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void MergeKeepsBothRowsOfADuplicateAndReportsIt()
    {
        var existing = Entry(10002, @"C:\Work\AWB-10002,from_other_pc.ipt", computer: "OTHERPC");
        var shared = new PartRegister([Entry(10001), existing]);
        var offline = Entry(10002, @"C:\Work\AWB-10002,made_offline.ipt", At.AddMinutes(5));

        var result = PartRegister.Merge(shared, [offline]);

        Assert.Equal([existing, offline], result.Register.Entries.Skip(1));
        var duplicate = Assert.Single(result.Duplicates);
        Assert.Equal(new PartNumber(10002), duplicate.Number);
        Assert.Same(existing, duplicate.Existing);
        Assert.Same(offline, duplicate.Added);
    }

    [Fact]
    public void MergeReportsAPendingEntryThatRepeatsAnEarlierPendingNumber()
    {
        var first = Entry(10005, @"C:\Work\AWB-10005,first.ipt");
        var second = Entry(10005, @"C:\Work\AWB-10005,second.ipt", At.AddMinutes(1));

        var result = PartRegister.Merge(new PartRegister([Entry(10001)]), [first, second]);

        Assert.Equal(3, result.Register.Entries.Count);
        var duplicate = Assert.Single(result.Duplicates);
        Assert.Same(first, duplicate.Existing);
        Assert.Same(second, duplicate.Added);
    }

    [Fact]
    public void MergeReportsAgainstTheFirstRowHoldingTheNumber()
    {
        var first = Entry(10002, @"C:\Work\first.ipt");
        var second = Entry(10002, @"C:\Work\second.ipt", At.AddMinutes(1));
        var pending = Entry(10002, @"C:\Work\pending.ipt", At.AddMinutes(2));

        var result = PartRegister.Merge(new PartRegister([first, second]), [pending]);

        Assert.Same(first, Assert.Single(result.Duplicates).Existing);
    }

    [Fact]
    public void MergeSkipsAnEntryTheSharedRegisterAlreadyHoldsExactly()
    {
        var synced = Entry(10002);
        var shared = new PartRegister([Entry(10001), synced]);

        var result = PartRegister.Merge(shared, [Entry(10002), Entry(10003)]);

        Assert.Equal([10001, 10002, 10003], result.Register.Entries.Select(e => e.Number.Value));
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void MergeTreatsAnEntryReadBackFromCsvAsTheSameAssignment()
    {
        // The shared register was written to CSV (time to the second) by a sync that was then interrupted.
        var pending = Entry(10002, at: At.AddMilliseconds(400));
        var shared = new PartRegister(RegisterCsv.Read(RegisterCsv.Write([Entry(10001), pending])).Entries);

        var result = PartRegister.Merge(shared, [pending]);

        Assert.Equal(2, result.Register.Entries.Count);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void MergeComparesThePathIgnoringCase()
    {
        var shared = new PartRegister([Entry(10002, @"C:\Work\AWB-10002,Plate.ipt")]);

        var result = PartRegister.Merge(shared, [Entry(10002, @"c:\work\awb-10002,plate.IPT")]);

        Assert.Single(result.Register.Entries);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void MergeAppendsAndReportsAnEntryWithTheSameNumberAndPathButAnotherTime()
    {
        var shared = new PartRegister([Entry(10002)]);

        var result = PartRegister.Merge(shared, [Entry(10002, at: At.AddSeconds(1))]);

        Assert.Equal(2, result.Register.Entries.Count);
        Assert.Single(result.Duplicates);
    }

    [Fact]
    public void MergeSkipsAPendingEntryRepeatedExactlyInThePendingList()
    {
        var result = PartRegister.Merge(PartRegister.Empty, [Entry(10001), Entry(10001)]);

        Assert.Single(result.Register.Entries);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void MergedRegisterContainsThePendingNumbers()
    {
        var result = PartRegister.Merge(new PartRegister([Entry(10001)]), [Entry(10009)]);

        Assert.True(result.Register.Contains(new PartNumber(10009)));
        Assert.Equal(new PartNumber(10010), result.Register.NextNumber());
    }
}
