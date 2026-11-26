using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Settings;
using InventorAddin.Core.Tests.Holes;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class NotesWorkbookTests
{
    private static object?[][] Tab(string title, params object?[][] rows) =>
        [[title], ["A note."], [], ["Pattern", "Note", "Placement"], .. rows];

    private static NotesWorkbookReadResult Read(object?[][] material, object?[][] finish)
    {
        var xlsx = new TestXlsx()
            .Sheet("How to edit", [["Text only."]])
            .Sheet("Material", material)
            .Sheet("Finish", finish);
        using var stream = xlsx.Build();
        return NotesWorkbookReader.Read(stream);
    }

    private static NotesWorkbook Invented() => Read(
        Tab("Material notes",
            ["*Resin Print*", "NOTE: 3D PRINTED PART", "Bottom"],
            ["*Case Hardened*", "HARDEN AFTER MACHINING.", "List"]),
        Tab("Finish notes",
            ["*Anodise*", "MASK THREADED HOLES.", "List"],
            ["*Anodise*", "HANDLE WITH GLOVES.", null])).Workbook;

    // ---- Reading ----

    [Fact]
    public void Read_InventedWorkbook_GivesRulesInOrderWithNoProblems()
    {
        var result = Read(
            Tab("m", ["A*", "One", "Bottom"], ["B?", "Two", "list"]),
            Tab("f", ["*C", "Three", ""]));

        Assert.False(result.HasProblems, string.Join("; ", result.Problems));
        Assert.Equal(
            [new NoteRule("A*", "One", NotePlacement.Bottom), new NoteRule("B?", "Two", NotePlacement.List)],
            result.Workbook.MaterialRules);
        Assert.Equal([new NoteRule("*C", "Three", NotePlacement.List)], result.Workbook.FinishRules);
    }

    [Fact]
    public void Read_BadRows_NameTabAndRow_AndTheRestLoad()
    {
        var result = Read(
            Tab("m",
                ["A*", "One", "Bottom"],
                [null, "No pattern", "List"],
                ["C*", "Odd placement", "Top"],
                ["D*", "Four", "List"]),
            Tab("f", ["E*", null, "List"], ["F*", "Six", "List"]));

        Assert.Equal(["A*", "D*"], result.Workbook.MaterialRules.Select(r => r.Pattern));
        Assert.Equal(["F*"], result.Workbook.FinishRules.Select(r => r.Pattern));
        Assert.Equal(
            [
                "Material tab, row 6: Pattern is blank; row left out.",
                "Material tab, row 7: Placement \"Top\" is not List or Bottom; row left out.",
                "Finish tab, row 5: Note is blank; row left out.",
            ],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void Read_NumberAsPlacement_IsNotAPlacement()
    {
        var result = Read(Tab("m", ["A*", "One", 1]), Tab("f"));

        Assert.Empty(result.Workbook.MaterialRules);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void Read_MissingTab_IsAProblem_AndTheOtherTabLoads()
    {
        var xlsx = new TestXlsx().Sheet("Material", Tab("m", ["A*", "One", "List"]));
        using var stream = xlsx.Build();

        var result = NotesWorkbookReader.Read(stream);

        Assert.Single(result.Workbook.MaterialRules);
        Assert.Empty(result.Workbook.FinishRules);
        Assert.Equal("The notes workbook has no \"Finish\" tab; its rows are not available.", result.Problems.Single().ToString());
    }

    [Fact]
    public void Read_MissingHeader_LeavesTheTabOut()
    {
        var result = Read(
            [["m"], ["note"], [], ["Pattern", "Note"], ["A*", "One"]],
            Tab("f", ["B*", "Two", "List"]));

        Assert.Empty(result.Workbook.MaterialRules);
        Assert.Single(result.Workbook.FinishRules);
        Assert.Equal(
            "Material tab, row 4: The Material tab has no \"Placement\" column. Row 4 should read: Pattern, Note, Placement; tab left out.",
            result.Problems.Single().ToString());
    }

    [Fact]
    public void Read_ColumnsInAnyOrder_AndHeadersIgnoringCase()
    {
        var result = Read(
            [["m"], [], [], ["placement ", "NOTE", "pattern", "Extra"], ["Bottom", "One", "A*", "x"]],
            Tab("f"));

        Assert.False(result.HasProblems);
        Assert.Equal(new NoteRule("A*", "One", NotePlacement.Bottom), result.Workbook.MaterialRules.Single());
    }

    [Fact]
    public void Read_MissingFile_GivesOneProblem_ThatNamesTheStarterOrSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "AWB_Notes.xlsx");

        var starter = NotesWorkbookReader.Read(new LibraryPath(path, IsStarter: true));
        var custom = NotesWorkbookReader.Read(new LibraryPath(path, IsStarter: false));

        Assert.Contains("starter notes workbook was not found at " + path, starter.Problems.Single().Message);
        Assert.Contains("notes workbook set in Settings was not found at " + path, custom.Problems.Single().Message);
        Assert.Empty(starter.Workbook.MaterialRules);
    }

    [Fact]
    public void Read_NotAnXlsx_GivesOneProblem()
    {
        using var stream = new MemoryStream("not a zip"u8.ToArray());

        var result = NotesWorkbookReader.Read(stream);

        Assert.Single(result.Problems);
        Assert.Empty(result.Workbook.FinishRules);
    }

    [Fact]
    public void Starter_Loads_WithNoProblems_AndGivesTheExamples()
    {
        var result = NotesWorkbookReader.Read(StarterPath());

        Assert.False(result.HasProblems, string.Join("; ", result.Problems));

        var print = NotesRules.Resolve(result.Workbook, "Invented 3D Print Resin", null, null);
        Assert.Equal(["NOTE: 3D PRINTED PART"], print.BottomNotes);

        var steel = NotesRules.Resolve(result.Workbook, "Invented Hardened Tool Steel", null, null);
        Assert.Single(steel.ListNotes);
        Assert.Contains("HARDEN", steel.ListNotes[0]);

        var anodise = NotesRules.Resolve(result.Workbook, null, "Anodise, Clear", null);
        Assert.True(anodise.ListNotes.Count >= 2);
        Assert.Contains(anodise.ListNotes, n => n.Contains("MASK"));
    }

    private static string StarterPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
            {
                return Path.Combine(dir.FullName, "libraries", LibraryPaths.StarterNotesWorkbookFileName);
            }
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    // ---- Matching ----

    [Theory]
    [InlineData("*Resin Print*", "Invented Resin Print 2", true)]
    [InlineData("*resin print*", "RESIN PRINT", true)]
    [InlineData("Resin*", "Big Resin", false)]
    [InlineData("Plate ?", "Plate A", true)]
    [InlineData("Plate ?", "Plate AB", false)]
    [InlineData("Plate ?", "Plate ", false)]
    [InlineData("*", "Anything", true)]
    [InlineData("*", "", false)]
    [InlineData("A.B", "AxB", false)]
    [InlineData("A(1)", "a(1)", true)]
    [InlineData("  Steel*  ", " Steel 4 ", true)]
    [InlineData("", "Steel", false)]
    public void Matches_WildcardsIgnoringCase(string pattern, string name, bool expected) =>
        Assert.Equal(expected, NotesRules.Matches(pattern, name));

    // ---- Resolving ----

    [Fact]
    public void Resolve_PrintMaterial_GivesBottomNote()
    {
        var plan = NotesRules.Resolve(Invented(), "Invented Resin Print", null, null);

        Assert.Equal(["NOTE: 3D PRINTED PART"], plan.BottomNotes);
        Assert.Empty(plan.ListNotes);
    }

    [Fact]
    public void Resolve_HardenedMaterial_GivesListNote()
    {
        var plan = NotesRules.Resolve(Invented(), "Invented Case Hardened Steel", "Plain", null);

        Assert.Equal(["HARDEN AFTER MACHINING."], plan.ListNotes);
        Assert.Empty(plan.BottomNotes);
    }

    [Fact]
    public void Resolve_FinishRules_AddTheirNotesInWorkbookOrder()
    {
        var plan = NotesRules.Resolve(Invented(), "Plain", "Anodise, Clear", null);

        Assert.Equal(["MASK THREADED HOLES.", "HANDLE WITH GLOVES."], plan.ListNotes);
    }

    [Fact]
    public void Resolve_FinishNote_IsInTheListAfterMaterialNotes_BeforeFinishNotes()
    {
        var plan = NotesRules.Resolve(Invented(), "Case Hardened", "Anodise", "  BREAK ALL EDGES.  ");

        Assert.Equal(
            ["HARDEN AFTER MACHINING.", "BREAK ALL EDGES.", "MASK THREADED HOLES.", "HANDLE WITH GLOVES."],
            plan.ListNotes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankFinishNote_AddsNothing(string? finishNote)
    {
        var plan = NotesRules.Resolve(Invented(), "Case Hardened", null, finishNote);

        Assert.Equal(["HARDEN AFTER MACHINING."], plan.ListNotes);
    }

    [Fact]
    public void Resolve_FinishNoteWithNoRules_StillGivesTheFinishNote()
    {
        var plan = NotesRules.Resolve(NotesWorkbook.Empty, null, null, "KEEP CLEAN.");

        Assert.Equal(["KEEP CLEAN."], plan.ListNotes);
    }

    [Fact]
    public void Resolve_RuleNoteSameAsFinishNote_IsNotRepeated()
    {
        var plan = NotesRules.Resolve(Invented(), "Plain", "Anodise", "mask threaded holes.");

        Assert.Equal(["mask threaded holes.", "HANDLE WITH GLOVES."], plan.ListNotes);
    }

    [Fact]
    public void Resolve_SameNoteFromTwoRows_IsPlacedOnce()
    {
        var book = new NotesWorkbook(
            [
                new NoteRule("A*", "ONE.", NotePlacement.List),
                new NoteRule("*B", "one.", NotePlacement.Bottom),
                new NoteRule("AB", "TWO.", NotePlacement.Bottom),
            ],
            []);

        var plan = NotesRules.Resolve(book, "AB", null, null);

        Assert.Equal(["ONE."], plan.ListNotes);
        Assert.Equal(["TWO."], plan.BottomNotes);
    }

    [Fact]
    public void Resolve_NoMatch_GivesNothing()
    {
        var plan = NotesRules.Resolve(Invented(), "Other", "Other", null);

        Assert.Empty(plan.ListNotes);
        Assert.Empty(plan.BottomNotes);
    }

    // ---- Paths and settings ----

    [Fact]
    public void LibraryPaths_NotesWorkbook_BlankSettingIsTheStarter_ASettingIsItself()
    {
        var blank = LibraryPaths.NotesWorkbook(new AddinSettings(), @"C:\addin");
        var set = LibraryPaths.NotesWorkbook(new AddinSettings { NotesWorkbookPath = @"  D:\mine\AWB_Notes.xlsx " }, @"C:\addin");

        Assert.True(blank.IsStarter);
        Assert.Equal(Path.Combine(@"C:\addin", "libraries", "AWB_Notes.xlsx"), blank.Path);
        Assert.False(set.IsStarter);
        Assert.Equal(@"D:\mine\AWB_Notes.xlsx", set.Path);
    }

    [Fact]
    public void NotesWorkbookPath_NullIsBlank_AndItCountsInEquality()
    {
        var a = new AddinSettings { NotesWorkbookPath = null! };
        var b = new AddinSettings { NotesWorkbookPath = "x.xlsx" };

        Assert.Equal("", a.NotesWorkbookPath);
        Assert.NotEqual(a, b);
    }
}
