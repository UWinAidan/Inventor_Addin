using InventorAddin.Core.Drawing;
using Xunit;

namespace InventorAddin.Core.Tests.Drawing;

public class NotesTextTests
{
    [Fact]
    public void ListFormatted_HasABoldHeadingThenNumberedLines()
    {
        string text = NotesText.ListFormatted(new[] { "HARDEN TO 50 HRC.", "MASK THE BORE." });

        Assert.Equal("<StyleOverride Bold='True'>NOTES:</StyleOverride><Br/>1. HARDEN TO 50 HRC.<Br/>2. MASK THE BORE.", text);
    }

    [Fact]
    public void ListFormatted_IsEmptyWithNoNotes()
    {
        Assert.Equal(string.Empty, NotesText.ListFormatted(Array.Empty<string>()));
        Assert.Equal(string.Empty, NotesText.ListFormatted(new[] { "  ", "" }));
    }

    [Fact]
    public void ListFormatted_NumbersOnlyTheNotesThatHaveText()
    {
        string text = NotesText.ListFormatted(new[] { "ONE", " ", "TWO" });

        Assert.EndsWith("<Br/>1. ONE<Br/>2. TWO", text);
    }

    [Fact]
    public void ListFormatted_EscapesMarkupAndFlattensLineBreaks()
    {
        string text = NotesText.ListFormatted(new[] { "R<2 & \r\n  A>B" });

        Assert.EndsWith("<Br/>1. R&lt;2 &amp; A&gt;B", text);
    }

    [Fact]
    public void BottomFormatted_MakesEachNoteBoldOnItsOwnLine()
    {
        string text = NotesText.BottomFormatted(new[] { "NOTE: 3D PRINTED PART", "SECOND" });

        Assert.Equal(
            "<StyleOverride Bold='True'>NOTE: 3D PRINTED PART</StyleOverride><Br/><StyleOverride Bold='True'>SECOND</StyleOverride>",
            text);
    }

    [Fact]
    public void BottomFormatted_IsEmptyWithNoNotes() =>
        Assert.Equal(string.Empty, NotesText.BottomFormatted(Array.Empty<string>()));

    [Theory]
    [InlineData(AwbNoteKind.List, "List")]
    [InlineData(AwbNoteKind.Bottom, "Bottom")]
    public void KindValue_NamesTheKind(AwbNoteKind kind, string expected) =>
        Assert.Equal(expected, NotesText.KindValue(kind));

    [Fact]
    public void TheAttributeNamesAreTheOnesSpikeFoundWorking()
    {
        Assert.Equal("AwbNotes", NotesText.AttributeSetName);
        Assert.Equal("Kind", NotesText.KindAttributeName);
    }
}

public class NotesPlacementTests
{
    // A3 landscape: 10 mm margin, a 180 by 55 mm title block in the bottom-right corner of the border.
    private static readonly SheetRect Border = new(10, 10, 410, 287);
    private static readonly SheetRect TitleBlock = new(230, 10, 410, 65);

    [Fact]
    public void List_SitsJustAboveTheTitleBlock_AsWideAsIt()
    {
        NoteBox box = NotesPlacement.List(Border, TitleBlock);

        Assert.Equal(230, box.LeftMm);
        Assert.Equal(65 + NotesPlacement.GapMm, box.BottomMm);
        Assert.Equal(180, box.WidthMm);
    }

    [Fact]
    public void List_WithNoTitleBlock_SitsInTheBottomLeftCorner()
    {
        NoteBox box = NotesPlacement.List(Border, null);

        Assert.Equal(15, box.LeftMm);
        Assert.Equal(10 + NotesPlacement.BottomMarginMm, box.BottomMm);
        Assert.Equal(NotesPlacement.NoTitleBlockWidthMm, box.WidthMm);
    }

    [Fact]
    public void Bottom_SitsBesideTheTitleBlock_ClearOfTheTriad()
    {
        NoteBox box = NotesPlacement.Bottom(Border, TitleBlock, listTopMm: 120);

        Assert.Equal(10 + NotesPlacement.BottomInsetMm, box.LeftMm);
        Assert.True(box.LeftMm >= 10 + 5 + 17.5, "starts past the triad's labels");
        Assert.Equal(10 + NotesPlacement.BottomMarginMm, box.BottomMm);
        Assert.Equal(NotesPlacement.MaxBottomWidthMm, box.WidthMm);
        Assert.True(box.LeftMm + box.WidthMm <= TitleBlock.MinXMm - NotesPlacement.GapMm);
    }

    [Fact]
    public void Bottom_IsNarrowedToTheRoomBesideTheTitleBlock()
    {
        var narrow = new SheetRect(160, 10, 410, 65);

        NoteBox box = NotesPlacement.Bottom(Border, narrow, listTopMm: 120);

        Assert.Equal(160 - NotesPlacement.GapMm - box.LeftMm, box.WidthMm, 6);
    }

    [Fact]
    public void Bottom_GoesAboveTheList_WhenTheTitleBlockLeavesNoRoom()
    {
        // A4 portrait: the title block spans nearly the whole border.
        var border = new SheetRect(10, 10, 200, 287);
        var titleBlock = new SheetRect(20, 10, 200, 65);

        NoteBox box = NotesPlacement.Bottom(border, titleBlock, listTopMm: 100);

        Assert.Equal(20, box.LeftMm);
        Assert.Equal(100 + NotesPlacement.GapMm, box.BottomMm);
        Assert.Equal(180, box.WidthMm);
    }

    [Fact]
    public void Bottom_WithNoTitleBlock_StartsPastTheTriad()
    {
        NoteBox box = NotesPlacement.Bottom(Border, null, listTopMm: 30);

        Assert.Equal(10 + NotesPlacement.BottomInsetMm, box.LeftMm);
        Assert.Equal(10 + NotesPlacement.BottomMarginMm, box.BottomMm);
        Assert.Equal(NotesPlacement.MaxBottomWidthMm, box.WidthMm);
    }
}

public class NotesMessagesTests
{
    [Theory]
    [InlineData(1, 0, "The NOTES list has 1 note.")]
    [InlineData(3, 0, "The NOTES list has 3 notes.")]
    [InlineData(0, 1, "The bottom line has 1 note.")]
    [InlineData(2, 2, "The NOTES list has 2 notes and the bottom line has 2 notes.")]
    public void Done_SaysWhereTheNotesWent(int list, int bottom, string expected) =>
        Assert.Equal(expected, NotesMessages.Done(list, bottom));

    [Fact]
    public void Done_WithNothing_SaysSo() =>
        Assert.Contains("No notes apply", NotesMessages.Done(0, 0));

    [Fact]
    public void WorkbookProblems_ListsEachProblem()
    {
        string text = NotesMessages.WorkbookProblems(new[]
        {
            new NotesWorkbookProblem("Material", 5, "the Pattern is blank.").ToString(),
            "The notes workbook was not found.",
        });

        Assert.Contains("- Material tab, row 5: the Pattern is blank.", text);
        Assert.Contains("- The notes workbook was not found.", text);
    }
}

public class NotesWorkbookReadResultTests
{
    [Fact]
    public void CouldNotRead_IsTrue_ForAMissingFile()
    {
        NotesWorkbookReadResult result = NotesWorkbookReader.Read(Path.Combine(Path.GetTempPath(), "no-such-notes-" + Guid.NewGuid().ToString("N") + ".xlsx"));

        Assert.True(result.CouldNotRead);
    }

    [Fact]
    public void CouldNotRead_IsFalse_ForABadRow()
    {
        var result = new NotesWorkbookReadResult(
            NotesWorkbook.Empty, new[] { new NotesWorkbookProblem("Material", 5, "the Pattern is blank.") });

        Assert.True(result.HasProblems);
        Assert.False(result.CouldNotRead);
    }

    [Fact]
    public void CouldNotRead_IsFalse_WithNoProblems() =>
        Assert.False(new NotesWorkbookReadResult(NotesWorkbook.Empty, Array.Empty<NotesWorkbookProblem>()).CouldNotRead);

    [Fact]
    public void CouldNotRead_IsTrue_ForAProblemOnTheWholeWorkbook() =>
        Assert.True(new NotesWorkbookReadResult(NotesWorkbook.Empty, new[] { new NotesWorkbookProblem(null, null, "x") }).CouldNotRead);

    [Fact]
    public void LeftAsTheyWere_SaysSo() =>
        Assert.Equal("The notes workbook could not be read, so the notes were left as they were.", NotesMessages.LeftAsTheyWere);

    [Fact]
    public void Warnings_HasANeutralHeading_AndListsEach()
    {
        string text = NotesMessages.Warnings(new[] { "a", "b" });

        Assert.DoesNotContain("workbook", text);
        Assert.Contains("- a", text);
        Assert.Contains("- b", text);
    }
}
