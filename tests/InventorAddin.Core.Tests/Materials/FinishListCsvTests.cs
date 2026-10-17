using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class FinishListCsvTests
{
    private const string Header =
        "Code,Name,Kind,AppliesTo,Appearance,ThicknessMin_mm,ThicknessMax_mm,DimensionsAfterFinish,PrintNote";

    /// <summary>An invented finish row, with any cell replaceable.</summary>
    private static string Row(
        string code = "TF1",
        string name = "Test finish",
        string kind = "Plating",
        string appliesTo = "Metal;Wood",
        string appearance = "Test Shine",
        string min = "0.010",
        string max = "0.020",
        string dims = "Yes",
        string note = "TEST NOTE.") =>
        string.Join(',', code, name, kind, appliesTo, appearance, min, max, dims, note);

    private static FinishListReadResult ParseRows(params string[] rows) =>
        FinishListCsv.Parse(Header + "\r\n" + string.Join("\r\n", rows) + "\r\n");

    internal static string ReadRepoFinishList()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
            {
                return File.ReadAllText(Path.Combine(dir.FullName, "libraries", "finishes.csv"));
            }
        }

        throw new InvalidOperationException(
            $"Could not find the repository root (the folder holding InventorAddin.slnx) above {AppContext.BaseDirectory}.");
    }

    // ---- The repo's own file ----

    [Fact]
    public void RepoFinishList_ParsesWithNoProblems_And14Finishes()
    {
        var result = FinishListCsv.Parse(ReadRepoFinishList());

        Assert.Empty(result.Problems);
        Assert.Equal(14, result.Finishes.Count);
    }

    [Fact]
    public void RepoFinishList_ReadsKindsAndNoChangeAppearance()
    {
        var result = FinishListCsv.Parse(ReadRepoFinishList());

        Assert.Contains(result.Finishes, f => f.Kind == FinishKind.HeatTreat);
        Assert.Contains(result.Finishes, f => f.Kind == FinishKind.Paint);
        Assert.Contains(result.Finishes, f => f.AppearanceName is null);
    }

    // ---- A good row ----

    [Fact]
    public void GoodRow_ReadsEveryField()
    {
        var result = ParseRows(Row());

        Assert.Empty(result.Problems);
        Assert.False(result.HasProblems);
        var finish = Assert.Single(result.Finishes);
        Assert.Equal("TF1", finish.Code);
        Assert.Equal("Test finish", finish.Name);
        Assert.Equal(FinishKind.Plating, finish.Kind);
        Assert.Equal(["Metal", "Wood"], finish.AppliesTo);
        Assert.False(finish.AppliesToAll);
        Assert.Equal("Test Shine", finish.AppearanceName);
        Assert.Equal("0.010", finish.ThicknessMinMm);
        Assert.Equal("0.020", finish.ThicknessMaxMm);
        Assert.True(finish.DimensionsAfterFinish);
        Assert.Equal("TEST NOTE.", finish.PrintNote);
    }

    [Fact]
    public void TextValues_AreTrimmed()
    {
        var result = ParseRows(Row(code: " TF1 ", name: "  Test finish ", appearance: " Test Shine ", min: " 0.010", max: "0.020 ", note: " TEST NOTE. "));

        var finish = Assert.Single(result.Finishes);
        Assert.Equal("TF1", finish.Code);
        Assert.Equal("Test finish", finish.Name);
        Assert.Equal("Test Shine", finish.AppearanceName);
        Assert.Equal("0.010", finish.ThicknessMinMm);
        Assert.Equal("0.020", finish.ThicknessMaxMm);
        Assert.Equal("TEST NOTE.", finish.PrintNote);
    }

    [Fact]
    public void QuotedCells_WithCommasAndLineBreaks_Read()
    {
        var result = ParseRows(Row(name: "\"Coat, test\"", note: "\"LINE ONE, \"\"A\"\"\r\nLINE TWO\""));

        var finish = Assert.Single(result.Finishes);
        Assert.Equal("Coat, test", finish.Name);
        Assert.Equal("LINE ONE, \"A\"\r\nLINE TWO", finish.PrintNote);
    }

    [Theory]
    [InlineData("Metal; Wood ;;", new[] { "Metal", "Wood" })]
    [InlineData("Metal", new[] { "Metal" })]
    public void AppliesTo_SplitsOnSemicolons_Trimmed(string cell, string[] expected)
    {
        var finish = Assert.Single(ParseRows(Row(appliesTo: cell)).Finishes);

        Assert.Equal(expected, finish.AppliesTo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(";")]
    public void BlankAppliesTo_SuitsEveryMaterial(string cell)
    {
        var finish = Assert.Single(ParseRows(Row(appliesTo: cell)).Finishes);

        Assert.Empty(finish.AppliesTo);
        Assert.True(finish.AppliesToAll);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("(no change)")]
    [InlineData("(No Change)")]
    [InlineData(" (NO CHANGE) ")]
    public void Appearance_BlankOrNoChange_IsNull(string cell)
    {
        var finish = Assert.Single(ParseRows(Row(appearance: cell)).Finishes);

        Assert.Null(finish.AppearanceName);
    }

    [Theory]
    [InlineData("Plating", FinishKind.Plating)]
    [InlineData("conversion", FinishKind.Conversion)]
    [InlineData("ANODIZE", FinishKind.Anodize)]
    [InlineData("Heat treat", FinishKind.HeatTreat)]
    [InlineData("heat treat", FinishKind.HeatTreat)]
    [InlineData("HeatTreat", FinishKind.HeatTreat)]
    [InlineData(" heat  Treat ", FinishKind.HeatTreat)]
    [InlineData("Paint", FinishKind.Paint)]
    public void Kind_ReadsIgnoringCaseAndSpaces(string cell, FinishKind expected)
    {
        var finish = Assert.Single(ParseRows(Row(kind: cell)).Finishes);

        Assert.Equal(expected, finish.Kind);
    }

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("yes", true)]
    [InlineData(" YES ", true)]
    [InlineData("No", false)]
    [InlineData("no", false)]
    public void DimensionsAfterFinish_YesOrNo(string cell, bool expected)
    {
        var finish = Assert.Single(ParseRows(Row(dims: cell)).Finishes);

        Assert.Equal(expected, finish.DimensionsAfterFinish);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.5", "0.5")]
    [InlineData(".5", "1")]
    [InlineData("0.050", "0.05")]
    [InlineData("2", "10")]
    public void Thickness_GoodNumbers_Read(string min, string max)
    {
        var result = ParseRows(Row(min: min, max: max));

        Assert.Empty(result.Problems);
        Assert.Single(result.Finishes);
    }

    // ---- Columns ----

    [Fact]
    public void Columns_InAnyOrderAndCase_WithExtraColumns_Read()
    {
        var text =
            "printnote,Extra,KIND,code,Name,appliesto,APPEARANCE,thicknessmax_mm,ThicknessMin_MM,dimensionsafterfinish\r\n" +
            "NOTE,ignored,Paint,TF2,Test coat,Metal,Test Shine,0.2,0.1,No\r\n";

        var result = FinishListCsv.Parse(text);

        Assert.Empty(result.Problems);
        var finish = Assert.Single(result.Finishes);
        Assert.Equal("TF2", finish.Code);
        Assert.Equal("Test coat", finish.Name);
        Assert.Equal(FinishKind.Paint, finish.Kind);
        Assert.Equal("0.1", finish.ThicknessMinMm);
        Assert.Equal("0.2", finish.ThicknessMaxMm);
        Assert.False(finish.DimensionsAfterFinish);
        Assert.Equal("NOTE", finish.PrintNote);
    }

    [Theory]
    [InlineData("Code")]
    [InlineData("Name")]
    [InlineData("Kind")]
    [InlineData("AppliesTo")]
    [InlineData("Appearance")]
    [InlineData("ThicknessMin_mm")]
    [InlineData("ThicknessMax_mm")]
    [InlineData("DimensionsAfterFinish")]
    [InlineData("PrintNote")]
    public void MissingColumn_MakesFileInvalid_WithOneProblemNamingIt(string column)
    {
        var header = string.Join(',', FinishListCsv.Columns.Where(c => c != column));
        var text = header + "\r\n" + Row() + "\r\n";

        var result = FinishListCsv.Parse(text);

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(1, problem.Line);
        Assert.Contains($"\"{column}\"", problem.Message);
    }

    [Fact]
    public void SeveralMissingColumns_GiveOneProblemNamingEach()
    {
        var result = FinishListCsv.Parse("Code,Name,Kind,AppliesTo,Appearance,PrintNote\r\nX,Y,Paint,,,Z\r\n");

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("\"ThicknessMin_mm\"", problem.Message);
        Assert.Contains("\"ThicknessMax_mm\"", problem.Message);
        Assert.Contains("\"DimensionsAfterFinish\"", problem.Message);
    }

    [Fact]
    public void EmptyFile_IsOneProblem()
    {
        var result = FinishListCsv.Parse("");

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Null(problem.Line);
        Assert.Equal(problem.Message, problem.ToString());
    }

    [Fact]
    public void HeaderOnly_HasNoFinishesAndNoProblems()
    {
        var result = FinishListCsv.Parse(Header + "\r\n");

        Assert.Empty(result.Finishes);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void ByteOrderMark_BeforeHeader_IsIgnored()
    {
        var result = FinishListCsv.Parse("﻿" + Header + "\r\n" + Row() + "\r\n");

        Assert.Empty(result.Problems);
        Assert.Single(result.Finishes);
    }

    [Fact]
    public void EmptyRowsFromExcel_AreSkipped()
    {
        var result = ParseRows(Row(code: "A1"), ",,,,,,,,", "", "  ,  ,,,,,,,", Row(code: "A2"));

        Assert.Empty(result.Problems);
        Assert.Equal(["A1", "A2"], result.Finishes.Select(f => f.Code));
    }

    [Fact]
    public void ShortRow_MissingTrailingCellsReadAsBlank()
    {
        // No PrintNote cell at all.
        var text = Header + "\r\n" + "TF1,Test finish,Plating,Metal,Test Shine,0,0,No\r\n";

        var finish = Assert.Single(FinishListCsv.Parse(text).Finishes);

        Assert.Equal("", finish.PrintNote);
    }

    // ---- Row problems ----

    [Fact]
    public void BlankCode_LeavesRowOut_RestLoad()
    {
        var result = ParseRows(Row(code: "A1"), Row(code: " "), Row(code: "A3"));

        Assert.Equal(["A1", "A3"], result.Finishes.Select(f => f.Code));
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("Code is blank", problem.Message);
        Assert.StartsWith("Line 3: ", problem.ToString());
    }

    [Fact]
    public void BlankName_LeavesRowOut()
    {
        var result = ParseRows(Row(name: ""));

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Contains("Name is blank", problem.Message);
    }

    [Fact]
    public void DuplicateCode_IgnoringCase_LeavesLaterRowOut()
    {
        var result = ParseRows(Row(code: "AB", name: "First"), Row(code: "ab", name: "Second"));

        var finish = Assert.Single(result.Finishes);
        Assert.Equal("First", finish.Name);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("\"ab\" is already used", problem.Message);
    }

    [Fact]
    public void DuplicateCode_OfARowThatWasLeftOut_IsStillADuplicate()
    {
        var result = ParseRows(Row(code: "AB", kind: "Glitter"), Row(code: "AB"));

        Assert.Empty(result.Finishes);
        Assert.Equal([2, 3], result.Problems.Select(p => p.Line));
        Assert.Contains("already used", result.Problems[1].Message);
    }

    [Theory]
    [InlineData("Glitter")]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("Heat-treat")]
    public void UnknownKind_LeavesRowOut(string kind)
    {
        var result = ParseRows(Row(kind: kind));

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("Kind", problem.Message);
    }

    [Theory]
    [InlineData("-0.01", "0.02")]
    [InlineData("abc", "0.02")]
    [InlineData("", "0.02")]
    [InlineData("\"0,01\"", "0.02")]
    [InlineData("1e-2", "0.02")]
    [InlineData("0.01", "-1")]
    [InlineData("0.01", "lots")]
    public void BadThickness_LeavesRowOut(string min, string max)
    {
        var result = ParseRows(Row(min: min, max: max));

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("is not a number of 0 or more", problem.Message);
    }

    [Fact]
    public void MinGreaterThanMax_LeavesRowOut()
    {
        var result = ParseRows(Row(min: "0.030", max: "0.02"));

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("greater than", problem.Message);
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("True")]
    [InlineData("")]
    [InlineData("Maybe")]
    public void DimensionsAfterFinish_NotYesOrNo_LeavesRowOut(string dims)
    {
        var result = ParseRows(Row(dims: dims));

        Assert.Empty(result.Finishes);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("is not Yes or No", problem.Message);
    }

    [Fact]
    public void SeveralThingsWrong_GiveOneProblemEach_OnTheSameLine()
    {
        var result = ParseRows(Row(code: "", kind: "Glitter", dims: "Maybe"));

        Assert.Empty(result.Finishes);
        Assert.Equal(3, result.Problems.Count);
        Assert.All(result.Problems, p => Assert.Equal(2, p.Line));
    }

    [Fact]
    public void UnclosedQuote_LeavesRowOut()
    {
        var result = ParseRows(Row(code: "A1"), "A2,\"Open name,Paint");

        Assert.Equal(["A1"], result.Finishes.Select(f => f.Code));
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("closing quote", problem.Message);
    }

    [Fact]
    public void ProblemLine_CountsLineBreaksInsideQuotes()
    {
        var result = ParseRows(Row(note: "\"ONE\r\nTWO\""), Row(code: "", note: "X"));

        var problem = Assert.Single(result.Problems);
        Assert.Equal(4, problem.Line);
    }
}
