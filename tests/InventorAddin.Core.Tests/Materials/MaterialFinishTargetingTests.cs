using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class MaterialFinishTargetingTests
{
    private static MaterialFinishCandidate<string> C(string item, string name, string file, MaterialFinishSkip skip = MaterialFinishSkip.None) =>
        new(item, name, file, skip);

    private static Finish F(string code, string? appearance) =>
        new(code, "Test finish " + code, FinishKind.Plating, Array.Empty<string>(), appearance, "0", "0", false, "");

    // ForPart

    [Fact]
    public void ForPart_Modifiable_IsTheOnlyTarget()
    {
        var part = C("doc", "Bracket.ipt", @"C:\work\Bracket.ipt");

        var plan = MaterialFinishTargeting.ForPart(part);

        Assert.Equal(new[] { part }, plan.Parts);
        Assert.Empty(plan.Notes);
        Assert.Null(plan.StopMessage);
    }

    [Theory]
    [InlineData(MaterialFinishSkip.ReadOnlyFile, MaterialFinishTargeting.PartReadOnlyMessage)]
    [InlineData(MaterialFinishSkip.NotModifiable, MaterialFinishTargeting.PartNotModifiableMessage)]
    public void ForPart_Blocked_Stops(MaterialFinishSkip skip, string message)
    {
        var plan = MaterialFinishTargeting.ForPart(C("doc", "Bracket.ipt", @"C:\work\Bracket.ipt", skip));

        Assert.Empty(plan.Parts);
        Assert.Equal(message, plan.StopMessage);
    }

    [Fact]
    public void ForPart_SubAssemblySkip_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MaterialFinishTargeting.ForPart(C("doc", "X", "", MaterialFinishSkip.SubAssembly)));
    }

    // ForAssembly

    [Fact]
    public void ForAssembly_KeepsParts_InSelectionOrder()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[]
        {
            C("b", "B.ipt", @"C:\w\B.ipt"),
            C("a", "A.ipt", @"C:\w\A.ipt"),
        });

        Assert.Equal(new[] { "b", "a" }, plan.Parts.Select(p => p.Item));
        Assert.Empty(plan.Notes);
        Assert.Null(plan.StopMessage);
    }

    [Fact]
    public void ForAssembly_SameFileTwice_KeptOnce_IgnoringCase()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[]
        {
            C("first", "A.ipt", @"C:\w\A.ipt"),
            C("second", "A.ipt", @"c:\W\a.IPT"),
        });

        Assert.Equal("first", Assert.Single(plan.Parts).Item);
    }

    [Fact]
    public void ForAssembly_BlankFileKeys_AreNotMerged()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[] { C("a", "A", ""), C("b", "B", "  ") });

        Assert.Equal(2, plan.Parts.Count);
    }

    [Fact]
    public void ForAssembly_SkipsAreNoted_PartsKept()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[]
        {
            C("sub", "Frame:1", "", MaterialFinishSkip.SubAssembly),
            C("part", "A.ipt", @"C:\w\A.ipt"),
            C("virt", "Paint:1", "", MaterialFinishSkip.Virtual),
        });

        Assert.Equal("part", Assert.Single(plan.Parts).Item);
        Assert.Equal(
            new[] { "Skipped: Frame:1 (a sub-assembly)", "Skipped: Paint:1 (a virtual component)" },
            plan.Notes);
        Assert.Null(plan.StopMessage);
    }

    [Fact]
    public void ForAssembly_SameSkipTwice_NotedOnce()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[]
        {
            C("sub", "Frame:1", "", MaterialFinishSkip.SubAssembly),
            C("sub", "Frame:1", "", MaterialFinishSkip.SubAssembly),
            C("part", "A.ipt", @"C:\w\A.ipt"),
        });

        Assert.Single(plan.Notes);
    }

    [Fact]
    public void ForAssembly_NoPartLeft_StopsWithTheSelectMessage()
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[] { C("sub", "Frame:1", "", MaterialFinishSkip.SubAssembly) });

        Assert.Empty(plan.Parts);
        Assert.Equal("Select one or more parts in the assembly, then run Material and Finish.", plan.StopMessage);
        Assert.Single(plan.Notes);
    }

    [Fact]
    public void ForAssembly_NothingSelected_Stops()
    {
        var plan = MaterialFinishTargeting.ForAssembly(Array.Empty<MaterialFinishCandidate<string>>());

        Assert.Equal(MaterialFinishTargeting.SelectPartsMessage, plan.StopMessage);
        Assert.Empty(plan.Notes);
    }

    [Theory]
    [InlineData(1, "Skipped: 1 selected item that is not a component.")]
    [InlineData(3, "Skipped: 3 selected items that are not components.")]
    public void ForAssembly_OtherSelectedItems_OneNote(int other, string note)
    {
        var plan = MaterialFinishTargeting.ForAssembly(new[] { C("part", "A.ipt", @"C:\w\A.ipt") }, other);

        Assert.Equal(new[] { note }, plan.Notes);
    }

    [Theory]
    [InlineData(MaterialFinishSkip.SubAssembly, "Skipped: X (a sub-assembly)")]
    [InlineData(MaterialFinishSkip.Virtual, "Skipped: X (a virtual component)")]
    [InlineData(MaterialFinishSkip.Suppressed, "Skipped: X (suppressed)")]
    [InlineData(MaterialFinishSkip.ReadOnlyFile, "Skipped: X (read-only file)")]
    [InlineData(MaterialFinishSkip.NotModifiable, "Skipped: X (cannot be modified)")]
    [InlineData(MaterialFinishSkip.NotAPart, "Skipped: X (not a part)")]
    public void SkippedNote_NamesTheReason(MaterialFinishSkip skip, string expected)
    {
        Assert.Equal(expected, MaterialFinishTargeting.SkippedNote(" X ", skip));
    }

    [Fact]
    public void SkippedNote_BlankName_SaysUnnamed()
    {
        Assert.Equal("Skipped: (unnamed) (suppressed)", MaterialFinishTargeting.SkippedNote("", MaterialFinishSkip.Suppressed));
    }

    [Fact]
    public void SkippedNote_None_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MaterialFinishTargeting.SkippedNote("X", MaterialFinishSkip.None));
    }

    // Appearance

    [Fact]
    public void AppearanceToFind_NoFinish_Null()
    {
        Assert.Null(MaterialFinishTargeting.AppearanceToFind(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AppearanceToFind_NoAppearanceName_Null(string? appearance)
    {
        Assert.Null(MaterialFinishTargeting.AppearanceToFind(F("XX", appearance)));
    }

    [Fact]
    public void AppearanceToFind_Trims()
    {
        Assert.Equal("Test Shine", MaterialFinishTargeting.AppearanceToFind(F("XX", " Test Shine ")));
    }

    [Fact]
    public void AppearanceNote_Found_Null()
    {
        var found = new AppearanceLookupResult(AppearanceLookupStatus.Found, "A-1", 0, 1);

        Assert.Null(MaterialFinishTargeting.AppearanceNote("Test Shine", found, 1));
    }

    [Fact]
    public void AppearanceNote_Missing_OnePart()
    {
        Assert.Equal(
            "Appearance 'Test Shine' was not found; the part keeps its material's appearance.",
            MaterialFinishTargeting.AppearanceNote("Test Shine", AppearanceLookupResult.Missing, 1));
    }

    [Fact]
    public void AppearanceNote_Ambiguous_SeveralParts()
    {
        var ambiguous = new AppearanceLookupResult(AppearanceLookupStatus.Ambiguous, null, 1, 3);

        Assert.Equal(
            "Appearance 'Test Shine' names 3 different appearances; the parts keep their material's appearance.",
            MaterialFinishTargeting.AppearanceNote("Test Shine", ambiguous, 2));
    }

    // Materials

    [Fact]
    public void IndexOfMaterial_IgnoresCaseAndSpaces_FirstMatch()
    {
        var materials = new[]
        {
            new LibraryMaterial("Test Alloy A", "Aluminum", null),
            new LibraryMaterial(" Test Steel B ", "Steel", "Test Shine"),
            new LibraryMaterial("test steel b", "Steel", null),
        };

        Assert.Equal(1, MaterialFinishTargeting.IndexOfMaterial("TEST STEEL B", materials));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nowhere")]
    public void IndexOfMaterial_NotThere_MinusOne(string? name)
    {
        var materials = new[] { new LibraryMaterial("Test Alloy A", "Aluminum", null) };

        Assert.Equal(-1, MaterialFinishTargeting.IndexOfMaterial(name, materials));
    }

    [Fact]
    public void MaterialNotInLibraryMessage_NamesTheMaterial()
    {
        Assert.Equal(
            "The material 'Test Alloy A' is not in the material library. Nothing was changed.",
            MaterialFinishTargeting.MaterialNotInLibraryMessage("Test Alloy A"));
    }

    // Messages and log

    [Theory]
    [InlineData(1, "Applied to 1 part. Save to keep the changes.")]
    [InlineData(4, "Applied to 4 parts. Save to keep the changes.")]
    public void AppliedMessage_CountsParts(int parts, string expected)
    {
        Assert.Equal(expected, MaterialFinishTargeting.AppliedMessage(parts));
    }

    [Theory]
    [InlineData("Could not set the property 'Finish'.", "Could not apply to A.ipt: Could not set the property 'Finish'. Nothing was changed.")]
    [InlineData("Unspecified error", "Could not apply to A.ipt: Unspecified error. Nothing was changed.")]
    [InlineData("", "Could not apply to A.ipt. Nothing was changed.")]
    public void FailedMessage_NamesThePart(string reason, string expected)
    {
        Assert.Equal(expected, MaterialFinishTargeting.FailedMessage("A.ipt", reason));
    }

    [Fact]
    public void FailedMessage_NoPart_SaysUnnamed()
    {
        Assert.Equal("Could not apply to (unnamed). Nothing was changed.", MaterialFinishTargeting.FailedMessage(null, null));
    }

    [Fact]
    public void ApplyLogLine_ListsTargetsMaterialAndFinish()
    {
        Assert.Equal(
            "Material and Finish: apply material 'Test Alloy A' and finish 'XX' to 2 parts: A.ipt, B.ipt",
            MaterialFinishTargeting.ApplyLogLine(new[] { "A.ipt", "B.ipt" }, "Test Alloy A", F("XX", null)));
    }

    [Fact]
    public void ApplyLogLine_NoFinish()
    {
        Assert.Equal(
            "Material and Finish: apply material 'Test Alloy A' and no finish to 1 part: A.ipt",
            MaterialFinishTargeting.ApplyLogLine(new[] { "A.ipt" }, "Test Alloy A", null));
    }

    [Fact]
    public void NoteLogLine_Prefixed()
    {
        Assert.Equal("Material and Finish: Skipped: X (suppressed)", MaterialFinishTargeting.NoteLogLine("Skipped: X (suppressed)"));
    }
}
