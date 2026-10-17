using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class StarterMaterialsCsvTests
{
    private const string Header = "Name,Category,BaseMaterial,Density_kg_m3,Appearance";

    /// <summary>An invented material row, with any cell replaceable.</summary>
    private static string Row(
        string name = "\"Testium, 12 soft\"",
        string category = "Testmetal",
        string baseMaterial = "Stock Testium",
        string density = "4321.5",
        string appearance = "Test Shine") =>
        string.Join(',', name, category, baseMaterial, density, appearance);

    private static StarterMaterialsReadResult ParseRows(params string[] rows) =>
        StarterMaterialsCsv.Parse(Header + "\r\n" + string.Join("\r\n", rows) + "\r\n");

    private static string ReadRepoFile(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
            {
                return File.ReadAllText(Path.Combine(dir.FullName, "libraries", fileName));
            }
        }

        throw new InvalidOperationException(
            $"Could not find the repository root (the folder holding InventorAddin.slnx) above {AppContext.BaseDirectory}.");
    }

    // ---- The repo's own files ----

    [Fact]
    public void RepoFile_ParsesWithNoProblems()
    {
        var result = StarterMaterialsCsv.Parse(ReadRepoFile(StarterMaterialsCsv.FileName));

        Assert.Empty(result.Problems);
        Assert.InRange(result.Materials.Count, 15, 30);
        Assert.All(result.Materials, m => Assert.NotEqual("", m.Appearance));
    }

    [Fact]
    public void RepoFile_UsesTheFinishListCategories()
    {
        var categories = StarterMaterialsCsv.Parse(ReadRepoFile(StarterMaterialsCsv.FileName)).Materials
            .Select(m => m.Category)
            .Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(["Aluminum", "Brass", "Bronze", "Copper", "Iron", "Plastic", "Steel"], categories);
    }

    [Fact]
    public void RepoFinishList_EveryAppliesToEntry_IsACategoryOrMaterialInTheStarterFile()
    {
        var materials = StarterMaterialsCsv.Parse(ReadRepoFile(StarterMaterialsCsv.FileName)).Materials;
        var known = new HashSet<string>(
            materials.Select(m => m.Category).Concat(materials.Select(m => m.Name)),
            StringComparer.OrdinalIgnoreCase);
        var finishes = FinishListCsv.Parse(ReadRepoFile(LibraryPaths.StarterFinishListFileName)).Finishes;

        var unknown = finishes
            .SelectMany(f => f.AppliesTo.Select(a => $"{f.Code}: {a}"))
            .Where(entry => !known.Contains(entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.NotEmpty(finishes);
        Assert.Empty(unknown);
    }

    // ---- A good row ----

    [Fact]
    public void GoodRow_ReadsEveryField()
    {
        var result = ParseRows(Row());

        Assert.False(result.HasProblems);
        var material = Assert.Single(result.Materials);
        Assert.Equal("Testium, 12 soft", material.Name);
        Assert.Equal("Testmetal", material.Category);
        Assert.Equal("Stock Testium", material.BaseMaterial);
        Assert.Equal(4321.5, material.DensityKgM3);
        Assert.Equal("Test Shine", material.Appearance);
    }

    [Fact]
    public void QuotedName_WithComma_AndTrimmedCells_Read()
    {
        var result = ParseRows(Row(name: "\" Testium, 12 soft \"", category: " Testmetal ", baseMaterial: "\"Stock, Testium\"", density: " 900 ", appearance: " Test Shine "));

        var material = Assert.Single(result.Materials);
        Assert.Equal("Testium, 12 soft", material.Name);
        Assert.Equal("Testmetal", material.Category);
        Assert.Equal("Stock, Testium", material.BaseMaterial);
        Assert.Equal(900, material.DensityKgM3);
        Assert.Equal("Test Shine", material.Appearance);
    }

    [Fact]
    public void BlankAppearance_IsAllowed()
    {
        var material = Assert.Single(ParseRows(Row(appearance: " ")).Materials);

        Assert.Equal("", material.Appearance);
    }

    [Fact]
    public void ColumnsInAnyOrder_WithExtraColumns_Read()
    {
        var text = "Appearance,Notes,Density_kg_m3,BaseMaterial,Category,Name\nShine,x,1000,Stock,Cat,Thing\n";

        var material = Assert.Single(StarterMaterialsCsv.Parse(text).Materials);

        Assert.Equal(new StarterMaterial("Thing", "Cat", "Stock", 1000, "Shine"), material);
    }

    [Fact]
    public void BlankRows_AreSkipped()
    {
        var result = ParseRows(",,,,", Row(), "");

        Assert.Empty(result.Problems);
        Assert.Single(result.Materials);
    }

    // ---- Problems ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankName_IsAProblem(string name)
    {
        var result = ParseRows(Row(name: name));

        Assert.Empty(result.Materials);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Equal("Line 2: Name is blank; row left out.", problem.ToString());
    }

    [Fact]
    public void DuplicateName_IgnoringCase_IsAProblem_AndTheFirstRowLoads()
    {
        var result = ParseRows(Row(name: "\"Testium, 12 soft\""), Row(name: "\"TESTIUM, 12 SOFT \""));

        var material = Assert.Single(result.Materials);
        Assert.Equal("Testium, 12 soft", material.Name);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("already used", problem.Message);
    }

    [Fact]
    public void BlankCategory_IsAProblem()
    {
        var problem = Assert.Single(ParseRows(Row(category: " ")).Problems);

        Assert.Equal("Category is blank; row left out.", problem.Message);
    }

    [Fact]
    public void BlankBaseMaterial_IsAProblem()
    {
        var problem = Assert.Single(ParseRows(Row(baseMaterial: "")).Problems);

        Assert.Equal("BaseMaterial is blank; row left out.", problem.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("-7850")]
    [InlineData("heavy")]
    [InlineData("7.85e3")]
    [InlineData("1.2.3")]
    public void DensityThatIsNotAPositiveNumber_IsAProblem(string density)
    {
        var result = ParseRows(Row(density: density));

        Assert.Empty(result.Materials);
        var problem = Assert.Single(result.Problems);
        Assert.StartsWith($"Density_kg_m3 \"{density.Trim()}\" is not a number greater than 0", problem.Message);
    }

    [Fact]
    public void EveryProblemInARow_IsReported_WithItsLine()
    {
        var result = ParseRows(Row(), Row(name: "", category: "", density: "x"));

        Assert.Single(result.Materials);
        Assert.Equal(3, result.Problems.Count);
        Assert.All(result.Problems, p => Assert.Equal(3, p.Line));
    }

    [Fact]
    public void UnterminatedQuote_IsAProblem()
    {
        var result = ParseRows(Row(), "\"Testium, open,Cat,Stock,1,Shine");

        Assert.Single(result.Materials);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("no closing quote", problem.Message);
    }

    [Fact]
    public void EmptyText_IsOneProblem()
    {
        var result = StarterMaterialsCsv.Parse("");

        Assert.Empty(result.Materials);
        var problem = Assert.Single(result.Problems);
        Assert.Null(problem.Line);
        Assert.Contains(StarterMaterialsCsv.Header, problem.Message);
    }

    [Fact]
    public void MissingColumns_AreOneProblem_NamingThem()
    {
        var result = StarterMaterialsCsv.Parse("Name,Category,Appearance\nThing,Cat,Shine\n");

        Assert.Empty(result.Materials);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(1, problem.Line);
        Assert.Contains("\"BaseMaterial\", \"Density_kg_m3\" columns", problem.Message);
    }

    [Fact]
    public void Parse_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => StarterMaterialsCsv.Parse(null!));
}
