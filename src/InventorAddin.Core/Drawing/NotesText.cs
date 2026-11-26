using System.Text;

namespace InventorAddin.Core.Drawing;

/// <summary>Which of the two AWB notes on a sheet a note is, stored in its <c>Kind</c> attribute.</summary>
public enum AwbNoteKind
{
    /// <summary>The numbered NOTES list above the title block.</summary>
    List,

    /// <summary>The bold line (or lines) at the bottom of the sheet.</summary>
    Bottom,
}

/// <summary>
/// The text of AWB's automatic notes (spec 05, "Added for M5", item 1; spike 184, question 2). Inventor's formatted text has no
/// list tag, so the numbering is plain "1. " text; bold is a <c>StyleOverride</c> and a line break is <c>Br</c>.
/// </summary>
public static class NotesText
{
    /// <summary>The attribute set AWB's notes carry, so they are found again and hand-typed notes are never touched.</summary>
    public const string AttributeSetName = "AwbNotes";

    /// <summary>The attribute in that set that says which note it is.</summary>
    public const string KindAttributeName = "Kind";

    /// <summary>The heading of the numbered list.</summary>
    public const string ListHeading = "NOTES:";

    /// <summary>The attribute value for <paramref name="kind"/>.</summary>
    public static string KindValue(AwbNoteKind kind) => kind == AwbNoteKind.List ? "List" : "Bottom";

    /// <summary>
    /// The formatted text of the numbered list: a bold heading, then "1. note", "2. note" and so on, one per line. Line breaks and
    /// runs of spaces inside a note become one space. An empty list gives an empty string.
    /// </summary>
    public static string ListFormatted(IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var lines = notes.Select(Flatten).Where(n => n.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append("<StyleOverride Bold='True'>").Append(ListHeading).Append("</StyleOverride>");
        for (int i = 0; i < lines.Count; i++)
        {
            text.Append("<Br/>").Append(i + 1).Append(". ").Append(Escape(lines[i]));
        }

        return text.ToString();
    }

    /// <summary>The formatted text of the bottom line: each note bold, one per line. An empty list gives an empty string.</summary>
    public static string BottomFormatted(IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var lines = notes.Select(Flatten).Where(n => n.Length > 0).ToList();
        return string.Join("<Br/>", lines.Select(l => "<StyleOverride Bold='True'>" + Escape(l) + "</StyleOverride>"));
    }

    /// <summary>The text on one line: line breaks and runs of white space become one space, and the ends are trimmed.</summary>
    public static string Flatten(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The text with the characters formatted text reads as markup written as entities.</summary>
    public static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}

/// <summary>Where a note goes: its lower-left corner and width, in millimetres on the sheet, Y up.</summary>
public readonly record struct NoteBox(double LeftMm, double BottomMm, double WidthMm);

/// <summary>
/// Where AWB's notes sit (Default, spec 05 question 15): the list sits just above the title block, as wide as it and left-aligned
/// with it, growing upward; the bottom line sits at the bottom of the border, clear of the X/Y triad and to the left of the title
/// block, and goes above the list instead when the title block leaves it no room (A4 and ANSI A portrait).
/// </summary>
public static class NotesPlacement
{
    /// <summary>The gap between the title block (or the list) and the note above it.</summary>
    public const double GapMm = 2.0;

    /// <summary>The gap between the border and the bottom line.</summary>
    public const double BottomMarginMm = 3.0;

    /// <summary>The narrowest room beside the title block the bottom line is put in.</summary>
    public const double MinBottomWidthMm = 60.0;

    /// <summary>The widest the bottom line is made.</summary>
    public const double MaxBottomWidthMm = 150.0;

    /// <summary>The list's width on a sheet with no title block.</summary>
    public const double NoTitleBlockWidthMm = 120.0;

    /// <summary>How far in from the border the bottom line starts: past the triad and its labels.</summary>
    public static double BottomInsetMm { get; } = PrintTemplateLayout.Triad.OriginXMm + PrintTemplateLayout.TriadExtentMm + 7.5;

    /// <summary>The list's box: the title block's left edge and width, <see cref="GapMm"/> above it. With no title block, the border's bottom-left corner.</summary>
    public static NoteBox List(SheetRect border, SheetRect? titleBlock) =>
        titleBlock is { } tb
            ? new NoteBox(tb.MinXMm, tb.MaxYMm + GapMm, tb.MaxXMm - tb.MinXMm)
            : new NoteBox(border.MinXMm + 5, border.MinYMm + BottomMarginMm, NoTitleBlockWidthMm);

    /// <summary>
    /// The bottom line's box. <paramref name="listTopMm"/> is the top of the list, or the title block's top (the list's own bottom)
    /// when there is no list; it is used when the bottom line has to sit above the title block.
    /// </summary>
    public static NoteBox Bottom(SheetRect border, SheetRect? titleBlock, double listTopMm)
    {
        double left = border.MinXMm + BottomInsetMm;
        double bottom = border.MinYMm + BottomMarginMm;
        if (titleBlock is { } tb)
        {
            double room = tb.MinXMm - GapMm - left;
            if (room >= MinBottomWidthMm)
            {
                return new NoteBox(left, bottom, Math.Min(room, MaxBottomWidthMm));
            }

            return new NoteBox(tb.MinXMm, listTopMm + GapMm, tb.MaxXMm - tb.MinXMm);
        }

        double free = border.MaxXMm - left - 5;
        return new NoteBox(left, bottom, Math.Max(MinBottomWidthMm, Math.Min(free, MaxBottomWidthMm)));
    }
}

/// <summary>The words Update Notes shows (spec 05, task 197).</summary>
public static class NotesMessages
{
    public const string WrongDocument = "Open a drawing first. Update Notes works on a drawing.";

    public const string NoModel = "This drawing does not reference a part or assembly, so there is nothing to read the material and finish from.";

    /// <summary>Shown when the workbook as a whole could not be read: nothing was changed.</summary>
    public const string LeftAsTheyWere = "The notes workbook could not be read, so the notes were left as they were.";

    public const string NoSheet = "The drawing has no sheet to put the notes on.";

    /// <summary>What was done: how many notes went where.</summary>
    public static string Done(int listCount, int bottomCount) =>
        (listCount, bottomCount) switch
        {
            (0, 0) => "No notes apply to this material and finish. AWB's earlier notes, if any, were removed.",
            (_, 0) => $"The NOTES list has {Count(listCount)}.",
            (0, _) => $"The bottom line has {Count(bottomCount)}.",
            _ => $"The NOTES list has {Count(listCount)} and the bottom line has {Count(bottomCount)}.",
        };

    /// <summary>The warning shown when something went wrong: the workbook's problems (the rows it left out) and anything that could not be placed.</summary>
    public static string WorkbookProblems(IReadOnlyList<string> problems) =>
        "The notes workbook has problems, so some rules were left out:" + Environment.NewLine +
        string.Join(Environment.NewLine, problems.Select(p => "- " + p));

    /// <summary>The warning shown when something could not be read or placed, apart from the workbook: one line each.</summary>
    public static string Warnings(IReadOnlyList<string> warnings) =>
        "Some things could not be read or placed:" + Environment.NewLine +
        string.Join(Environment.NewLine, warnings.Select(w => "- " + w));

    private static string Count(int n) => n == 1 ? "1 note" : $"{n} notes";
}
