using System.Globalization;

namespace InventorAddin.Core.Materials;

/// <summary>
/// One material of the starter library, as written in <c>libraries/starter-materials.csv</c> (spec 03, "Libraries").
/// </summary>
/// <param name="Name">The material's display name in the library, for example <c>Aluminum, 6061-T6</c>.</param>
/// <param name="Category">The library category, for example <c>Aluminum</c>; the finish list's AppliesTo uses these.</param>
/// <param name="BaseMaterial">
/// The display name of a material in Inventor's stock material library that the generator copies as a template.
/// </param>
/// <param name="DensityKgM3">The density in kg/m³, as Inventor's <c>structural_Density</c> value holds it.</param>
/// <param name="Appearance">The display name of the material's appearance, or <c>""</c> to keep the base material's.</param>
public sealed record StarterMaterial(string Name, string Category, string BaseMaterial, double DensityKgM3, string Appearance);

/// <summary>
/// One thing wrong with the starter materials file. <see cref="Line"/> is the 1-based line of the file, or null when
/// the problem is with the file as a whole.
/// </summary>
public sealed record StarterMaterialProblem(int? Line, string Message)
{
    public override string ToString() => Line is { } line ? $"Line {line}: {Message}" : Message;
}

/// <summary>What <see cref="StarterMaterialsCsv.Parse"/> found: the materials that loaded, in file order, and the problems.</summary>
public sealed record StarterMaterialsReadResult(
    IReadOnlyList<StarterMaterial> Materials,
    IReadOnlyList<StarterMaterialProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Reads <c>libraries/starter-materials.csv</c>, the data the starter library generator
/// (<c>tools/StarterLibraryBuilder</c>) builds <c>StarterMaterials.adsklib</c> from (task 071). Columns are found by
/// header name in any order, extra columns are ignored, and a bad row is left out with a problem while the rest load.
/// </summary>
public static class StarterMaterialsCsv
{
    public const string NameColumn = "Name";
    public const string CategoryColumn = "Category";
    public const string BaseMaterialColumn = "BaseMaterial";
    public const string DensityColumn = "Density_kg_m3";
    public const string AppearanceColumn = "Appearance";

    /// <summary>The data file's name in the repo's <c>libraries</c> folder.</summary>
    public const string FileName = "starter-materials.csv";

    /// <summary>The columns every starter materials file must have, in the order the repo file uses.</summary>
    public static IReadOnlyList<string> Columns { get; } =
        [NameColumn, CategoryColumn, BaseMaterialColumn, DensityColumn, AppearanceColumn];

    /// <summary>The header line of the repo file, for messages.</summary>
    public static string Header { get; } = string.Join(',', Columns);

    /// <summary>
    /// Reads starter materials CSV text (see <see cref="CsvTable.Parse"/> for the CSV rules). Every column in
    /// <see cref="Columns"/> is required; a missing one makes the whole file invalid with one problem and no
    /// materials. Rows whose cells are all blank are skipped. A row is left out, with a problem naming its line for
    /// each thing wrong, when: Name is blank, or was used on an earlier row (ignoring case); Category is blank;
    /// BaseMaterial is blank; the density is not a positive number in invariant culture; or a quoted value has no
    /// closing quote. A blank Appearance is allowed and keeps the base material's appearance.
    /// </summary>
    public static StarterMaterialsReadResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var materials = new List<StarterMaterial>();
        var problems = new List<StarterMaterialProblem>();

        var table = CsvTable.Parse(text);
        if (table.Header is not { } header)
        {
            problems.Add(new StarterMaterialProblem(null, $"The starter materials file is empty. The first line should be: {Header}"));
            return Result(materials, problems);
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Fields.Count; i++)
        {
            columns.TryAdd(header.Fields[i].Trim(), i);
        }

        var missing = Columns.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(c => $"\"{c}\""));
            var noun = missing.Count == 1 ? "column" : "columns";
            problems.Add(new StarterMaterialProblem(
                header.Line,
                $"The starter materials file has no {names} {noun}. The first line should be: {Header}"));
            return Result(materials, problems);
        }

        var namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            if (row.IsBlank)
            {
                continue;
            }

            var rowProblems = new List<string>();
            var material = ReadRow(row, columns, namesSeen, rowProblems);
            if (material is not null)
            {
                materials.Add(material);
            }
            else
            {
                problems.AddRange(rowProblems.Select(p => new StarterMaterialProblem(row.Line, $"{p}; row left out.")));
            }
        }

        return Result(materials, problems);
    }

    private static StarterMaterialsReadResult Result(List<StarterMaterial> materials, List<StarterMaterialProblem> problems) =>
        new(materials.AsReadOnly(), problems.AsReadOnly());

    private static StarterMaterial? ReadRow(
        CsvRow row,
        Dictionary<string, int> columns,
        HashSet<string> namesSeen,
        List<string> problems)
    {
        if (row.Unterminated)
        {
            problems.Add("a quoted value has no closing quote");
            return null;
        }

        string Cell(string column) => row[columns[column]].Trim();

        var name = Cell(NameColumn);
        if (name.Length == 0)
        {
            problems.Add("Name is blank");
        }
        else if (!namesSeen.Add(name))
        {
            problems.Add($"Name \"{name}\" is already used on an earlier row");
        }

        var category = Cell(CategoryColumn);
        if (category.Length == 0)
        {
            problems.Add("Category is blank");
        }

        var baseMaterial = Cell(BaseMaterialColumn);
        if (baseMaterial.Length == 0)
        {
            problems.Add("BaseMaterial is blank");
        }

        var densityText = Cell(DensityColumn);
        if (!TryParseDensity(densityText, out var density))
        {
            problems.Add($"{DensityColumn} \"{densityText}\" is not a number greater than 0, for example 7850");
        }

        if (problems.Count > 0)
        {
            return null;
        }

        return new StarterMaterial(name, category, baseMaterial, density, Cell(AppearanceColumn));
    }

    /// <summary>
    /// Reads a density in kg/m³: digits with an optional <c>.</c> decimal point, invariant culture, greater than 0.
    /// No sign, group separator or exponent.
    /// </summary>
    internal static bool TryParseDensity(string text, out double value) =>
        double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
        && value > 0
        && double.IsFinite(value);
}
