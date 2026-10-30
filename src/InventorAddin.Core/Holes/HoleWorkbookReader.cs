using System.Globalization;
using System.Xml;
using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Holes;

/// <summary>
/// Reads the hole workbook, <c>libraries/AWB_Holes.xlsx</c> (spec 09, "The workbook"), without Excel. The workbook
/// is edited by hand and never written by the add-in, so it is read leniently, as the finish list is: tabs by name,
/// columns by header text in any order, extra columns ignored, and a bad row left out with a problem while the rest
/// load. Tab names and headers match ignoring case and surrounding spaces.
/// </summary>
public static class HoleWorkbookReader
{
    public const string SettingsTab = "Settings";
    public const string ClearanceTab = "Clearance";
    public const string CounterboreTab = "Counterbore";
    public const string CountersinkTab = "Countersink";
    public const string SpotfaceTab = "Spotface";
    public const string TappedTab = "Tapped";
    public const string TaperTappedTab = "Taper Tapped";
    public const string DowelTab = "Dowel";
    public const string PrecisionTolTab = "Precision Tol";
    public const string DrillPointTab = "Drill Point";

    /// <summary>
    /// The tabs a workbook may leave out with no problem (task 111): added after the others, so a workbook made before
    /// them (Aidan's own copy until he adds them) still loads. A missing one is listed in
    /// <see cref="HoleWorkbook.MissingOptionalTabs"/>.
    /// </summary>
    public static IReadOnlyList<string> OptionalTabs { get; } = [SpotfaceTab, TaperTappedTab];

    /// <summary>The row that holds the headers on every data tab (title in row 1, note in row 2).</summary>
    public const int HeaderRow = 4;

    // Settings tab: a key/value table. Its header row is found by its "Setting" header, since the starter workbook
    // has it in row 3 (no note row).
    public const string SettingColumn = "Setting";
    public const string ValueColumn = "Value";
    public const string RedColumn = "R";
    public const string GreenColumn = "G";
    public const string BlueColumn = "B";

    public const string DefaultUnitsSetting = "Default units";
    public const string ThreadDepthSetting = "Thread depth (x D)";
    public const string DrillExtraDepthSetting = "Drill extra depth (x D)";
    public const string PressFitColourSetting = "Press fit colour";
    public const string NormalFitColourSetting = "Normal fit colour";
    public const string SlipFitColourSetting = "Slip fit colour";

    /// <summary>Optional: a workbook without the row (made before task 097) reads with the starter value, unreported.</summary>
    public const string BlindDepthSetting = "Blind depth multiplier";

    // Shared data-tab headers.
    public const string UnitsColumn = "Units";
    public const string SizeColumn = "Size";
    public const string NominalColumn = "Nominal";
    public const string ShowColumn = "Show";
    public const string ScrewColumn = "Screw";
    public const string HoleFitColumn = "Hole fit";
    public const string DefaultColumn = "Default";
    public const string NoteColumn = "Note";

    public const string CloseColumn = "Close";
    public const string NormalColumn = "Normal";
    public const string LooseColumn = "Loose";
    public const string CboreDiaColumn = "Cbore dia";
    public const string CboreDepthColumn = "Cbore depth";
    public const string CsinkDiaColumn = "Csink dia";
    public const string AngleColumn = "Angle (deg)";
    public const string ThreadColumn = "Thread";
    public const string PitchColumn = "Pitch / TPI";
    public const string TapDrillColumn = "Tap drill";
    public const string ClassColumn = "Class";
    public const string ThreadDepthColumn = "Default thread depth";
    public const string DrillDepthColumn = "Default drill depth";
    public const string FitColumn = "Fit";
    public const string NameColumn = "Name / purpose";
    public const string TypeColumn = "Type";
    public const string IsoCodeColumn = "ISO code";
    public const string UpperColumn = "Upper";
    public const string LowerColumn = "Lower";
    public const string DrillPointNameColumn = "Name";
    public const string SpotfaceDiaColumn = "Spotface dia";
    public const string SpotfaceDepthColumn = "Spotface depth";
    public const string ThreadTypeColumn = "Thread type";

    /// <summary>
    /// The Dowel tab's optional seat columns (task 106), after its required ones: where a dowel's counterbore or
    /// countersink starts. A workbook without them reads with no problem; <see cref="CboreDiaColumn"/>,
    /// <see cref="CboreDepthColumn"/> and <see cref="CsinkDiaColumn"/> are the same headers as on the seat tabs.
    /// </summary>
    public const string CsinkAngleColumn = "Csink angle";

    /// <summary>The Dowel tab's optional seat columns, in the starter workbook's order.</summary>
    public static IReadOnlyList<string> DowelSeatHeaders { get; } =
        [CboreDiaColumn, CboreDepthColumn, CsinkDiaColumn, CsinkAngleColumn];

    /// <summary>The headers each data tab must have, in the starter workbook's order.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [ClearanceTab] = [UnitsColumn, SizeColumn, NominalColumn, CloseColumn, NormalColumn, LooseColumn, ShowColumn],
            [CounterboreTab] = [UnitsColumn, SizeColumn, ScrewColumn, CboreDiaColumn, CboreDepthColumn, HoleFitColumn, ShowColumn],
            [CountersinkTab] = [UnitsColumn, SizeColumn, ScrewColumn, CsinkDiaColumn, AngleColumn, HoleFitColumn, ShowColumn],
            [SpotfaceTab] = [UnitsColumn, SizeColumn, SpotfaceDiaColumn, SpotfaceDepthColumn, ShowColumn],
            [TappedTab] =
            [
                UnitsColumn, ThreadColumn, NominalColumn, PitchColumn, TapDrillColumn, ClassColumn,
                ThreadDepthColumn, DrillDepthColumn, ShowColumn,
            ],
            [TaperTappedTab] =
            [
                UnitsColumn, SizeColumn, ThreadTypeColumn, TapDrillColumn, ThreadDepthColumn, DrillDepthColumn,
                ShowColumn,
            ],
            [DowelTab] = [UnitsColumn, SizeColumn, NominalColumn, ShowColumn],
            [PrecisionTolTab] =
            [
                UnitsColumn, FitColumn, NameColumn, TypeColumn, IsoCodeColumn, UpperColumn, LowerColumn,
                DefaultColumn, NoteColumn,
            ],
            [DrillPointTab] = [DrillPointNameColumn, AngleColumn, DefaultColumn, ShowColumn, NoteColumn],
        };

    /// <summary>The headers the Settings tab must have.</summary>
    public static IReadOnlyList<string> SettingsHeaders { get; } =
        [SettingColumn, ValueColumn, RedColumn, GreenColumn, BlueColumn];

    /// <summary>
    /// Reads the workbook at <paramref name="path"/>. A missing or unreadable file gives one problem and
    /// <see cref="HoleWorkbook.Empty"/>; nothing is thrown for a bad file.
    /// </summary>
    public static HoleWorkbookReadResult Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return ReadFile(
            path,
            () => $"The hole workbook was not found at {path}.",
            reason => $"Could not read the hole workbook at {path}. Check that it is an .xlsx file. ({reason})");
    }

    /// <summary>
    /// Reads the workbook <see cref="LibraryPaths.HoleWorkbook"/> resolved, as <see cref="Read(string)"/> does, with a
    /// missing or unreadable file reported by <see cref="HoleWorkbookMessages"/>, which say whether it is the starter
    /// workbook or the one set in Settings (task 086).
    /// </summary>
    public static HoleWorkbookReadResult Read(LibraryPath workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return ReadFile(
            workbook.Path,
            () => HoleWorkbookMessages.Missing(workbook),
            reason => HoleWorkbookMessages.Unreadable(workbook, reason));
    }

    private static HoleWorkbookReadResult ReadFile(string path, Func<string> missing, Func<string, string> unreadable)
    {
        if (!File.Exists(path))
        {
            return Failed(missing());
        }

        try
        {
            using var stream = File.OpenRead(path);
            return Read(XlsxPackage.Read(stream));
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return Failed(unreadable(ex.Message));
        }
    }

    /// <summary>Reads a workbook from a stream, as <see cref="Read(string)"/> does.</summary>
    public static HoleWorkbookReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return Read(XlsxPackage.Read(stream));
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return Failed($"Could not read the hole workbook. Check that it is an .xlsx file. ({ex.Message})");
        }
    }

    /// <summary>Reads every tab of a package that has already been opened.</summary>
    public static HoleWorkbookReadResult Read(XlsxPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var problems = new List<HoleWorkbookProblem>();
        var settings = ReadSettings(package, problems);
        var missingOptional = OptionalTabs.Where(tab => package.Sheet(tab) is null).ToList();

        var workbook = new HoleWorkbook(
            settings,
            ReadTab(package, ClearanceTab, problems, ReadClearance),
            ReadTab(package, CounterboreTab, problems, ReadCounterbore),
            ReadTab(package, CountersinkTab, problems, ReadCountersink),
            ReadTab(package, TappedTab, problems, r => ReadTapped(r, settings)),
            ReadTab(package, DowelTab, problems, ReadDowel),
            ReadTab(package, PrecisionTolTab, problems, ReadPrecisionTolerance),
            ReadTab(package, DrillPointTab, problems, ReadDrillPoint))
        {
            Spotface = ReadTab(package, SpotfaceTab, problems, ReadSpotface),
            TaperTapped = ReadTab(package, TaperTappedTab, problems, r => ReadTaperTapped(r, settings)),
            MissingOptionalTabs = missingOptional.AsReadOnly(),
        };

        return new HoleWorkbookReadResult(workbook, problems.AsReadOnly());
    }

    private static bool IsReadFailure(Exception ex) =>
        ex is InvalidDataException or XmlException or IOException or UnauthorizedAccessException;

    private static HoleWorkbookReadResult Failed(string message) =>
        new(HoleWorkbook.Empty, [new HoleWorkbookProblem(null, null, message)]);

    private static string Normalise(string text) => text.Trim();

    private static bool SameText(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);

    private static string NoTab(string tab) => $"The hole workbook has no \"{tab}\" tab; its rows are not available.";

    /// <summary>Maps header text (trimmed, ignoring case) to its column in a row; the first of a repeated header wins.</summary>
    private static Dictionary<string, int> HeaderColumns(XlsxSheet sheet, int row)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= sheet.LastColumn; column++)
        {
            var text = sheet.Text(row, column);
            if (text.Length > 0)
            {
                columns.TryAdd(text, column);
            }
        }

        return columns;
    }

    private static HoleWorkbookProblem? MissingHeaders(
        string tab,
        int row,
        Dictionary<string, int> columns,
        IReadOnlyList<string> required)
    {
        var missing = required.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count == 0)
        {
            return null;
        }

        var names = string.Join(", ", missing.Select(h => $"\"{h}\""));
        var noun = missing.Count == 1 ? "column" : "columns";
        return new HoleWorkbookProblem(
            tab,
            row,
            $"The {tab} tab has no {names} {noun}. Row {row} should read: {string.Join(", ", required)}; tab left out.");
    }

    private static IReadOnlyList<T> ReadTab<T>(
        XlsxPackage package,
        string tab,
        List<HoleWorkbookProblem> problems,
        Func<RowReader, T?> readRow)
        where T : class
    {
        var sheet = package.Sheet(tab);
        if (sheet is null)
        {
            // An optional tab that is not there is no problem (task 111); the caller lists it as missing.
            if (!OptionalTabs.Contains(tab))
            {
                problems.Add(new HoleWorkbookProblem(null, null, NoTab(tab)));
            }

            return [];
        }

        var columns = HeaderColumns(sheet, HeaderRow);
        if (MissingHeaders(tab, HeaderRow, columns, Headers[tab]) is { } missing)
        {
            problems.Add(missing);
            return [];
        }

        var rows = new List<T>();
        for (var row = HeaderRow + 1; row <= sheet.LastRow && !sheet.IsRowBlank(row); row++)
        {
            var reader = new RowReader(sheet, row, columns);
            var value = readRow(reader);
            if (value is not null && reader.Problems.Count == 0)
            {
                rows.Add(value);
            }
            else
            {
                problems.AddRange(reader.Problems.Select(p => new HoleWorkbookProblem(tab, row, $"{p}; row left out.")));
            }
        }

        return rows.AsReadOnly();
    }

    private static ClearanceRow? ReadClearance(RowReader r)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var nominal = r.Positive(NominalColumn);
        var close = r.Positive(CloseColumn);
        var normal = r.Positive(NormalColumn);
        var loose = r.Positive(LooseColumn);
        var hidden = r.Hidden();
        return r.Ok ? new ClearanceRow(r.Row, units, size, nominal, close, normal, loose, hidden) : null;
    }

    private static CounterboreRow? ReadCounterbore(RowReader r)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var screw = r.Required(ScrewColumn);
        var diameter = r.Positive(CboreDiaColumn);
        var depth = r.Positive(CboreDepthColumn);
        var fit = r.ClearanceFit();
        var hidden = r.Hidden();
        return r.Ok ? new CounterboreRow(r.Row, units, size, screw, diameter, depth, fit, hidden) : null;
    }

    private static CountersinkRow? ReadCountersink(RowReader r)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var screw = r.Required(ScrewColumn);
        var diameter = r.Positive(CsinkDiaColumn);
        var angle = r.Angle(AngleColumn, required: true) ?? 0m;
        var fit = r.ClearanceFit();
        var hidden = r.Hidden();
        return r.Ok ? new CountersinkRow(r.Row, units, size, screw, diameter, angle, fit, hidden) : null;
    }

    private static TappedRow? ReadTapped(RowReader r, HoleSettings settings)
    {
        var units = r.Units();
        var thread = r.Required(ThreadColumn);
        var nominal = r.Positive(NominalColumn);
        var pitch = r.Positive(PitchColumn);
        var tapDrill = r.Positive(TapDrillColumn);
        var threadClass = r.Required(ClassColumn);
        var threadDepthCell = r.OptionalPositive(ThreadDepthColumn);
        var drillDepthCell = r.OptionalPositive(DrillDepthColumn);
        var hidden = r.Hidden();
        if (!r.Ok)
        {
            return null;
        }

        var threadDepth = threadDepthCell ?? settings.DefaultThreadDepth(nominal);
        var drillDepth = drillDepthCell ?? settings.DefaultDrillDepth(nominal, threadDepth);
        return new TappedRow(r.Row, units, thread, nominal, pitch, tapDrill, threadClass, threadDepth, drillDepth, hidden);
    }

    private static SpotfaceRow? ReadSpotface(RowReader r)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var diameter = r.Positive(SpotfaceDiaColumn);

        // A depth of 0 is allowed: Inventor accepts a spotface of depth 0 (spike 108; Aidan, 2026-10-28).
        var depth = r.NonNegative(SpotfaceDepthColumn);
        var hidden = r.Hidden();
        return r.Ok ? new SpotfaceRow(r.Row, units, size, diameter, depth, hidden) : null;
    }

    private static TaperTappedRow? ReadTaperTapped(RowReader r, HoleSettings settings)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var threadType = r.Required(ThreadTypeColumn);
        var tapDrill = r.Positive(TapDrillColumn);
        var threadDepthCell = r.OptionalPositive(ThreadDepthColumn);
        var drillDepthCell = r.OptionalPositive(DrillDepthColumn);
        var hidden = r.Hidden();
        if (!r.Ok)
        {
            return null;
        }

        // The tab has no Nominal column, so a blank depth is worked out with the tap drill as D (Default, task 111).
        var threadDepth = threadDepthCell ?? settings.DefaultThreadDepth(tapDrill);
        var drillDepth = drillDepthCell ?? settings.DefaultDrillDepth(tapDrill, threadDepth);
        return new TaperTappedRow(r.Row, units, size, threadType, tapDrill, threadDepth, drillDepth, hidden);
    }

    private static DowelRow? ReadDowel(RowReader r)
    {
        var units = r.Units();
        var size = r.Required(SizeColumn);
        var nominal = r.Positive(NominalColumn);
        var hidden = r.Hidden();

        // The seat columns are optional: a missing column or a blank cell is null (task 106).
        var cboreDiameter = r.OptionalPositive(CboreDiaColumn);
        var cboreDepth = r.OptionalPositive(CboreDepthColumn);
        var csinkDiameter = r.OptionalPositive(CsinkDiaColumn);
        var csinkAngle = r.Angle(CsinkAngleColumn, required: false);
        return r.Ok
            ? new DowelRow(r.Row, units, size, nominal, hidden)
            {
                CboreDiameter = cboreDiameter,
                CboreDepth = cboreDepth,
                CsinkDiameter = csinkDiameter,
                CsinkAngleDeg = csinkAngle,
            }
            : null;
    }

    private static PrecisionToleranceRow? ReadPrecisionTolerance(RowReader r)
    {
        var units = r.Units();
        var fit = r.PrecisionFit();
        var name = r.Required(NameColumn);
        var kind = r.ToleranceKind();
        var isDefault = r.YesNo(DefaultColumn, blankMeans: false);
        var note = r.Text(NoteColumn);

        string? code = null;
        decimal? upper = null;
        decimal? lower = null;
        if (kind == ToleranceKind.Iso)
        {
            code = r.Text(IsoCodeColumn);
            if (code.Length == 0)
            {
                r.Problem($"{IsoCodeColumn} is blank on a Type ISO row");
            }
        }
        else if (kind == ToleranceKind.Limits)
        {
            upper = r.Number(UpperColumn, "for example 0.0005");
            lower = r.Number(LowerColumn, "for example -0.0005");
            if (upper < lower)
            {
                r.Problem($"{UpperColumn} {r.Text(UpperColumn)} is less than {LowerColumn} {r.Text(LowerColumn)}");
            }
        }

        return r.Ok && kind is { } k ? new PrecisionToleranceRow(r.Row, units, fit, name, k, code, upper, lower, isDefault, note) : null;
    }

    private static DrillPointRow? ReadDrillPoint(RowReader r)
    {
        var name = r.Required(DrillPointNameColumn);
        var angle = r.Angle(AngleColumn, required: false);
        var isDefault = r.YesNo(DefaultColumn, blankMeans: false);
        var hidden = r.Hidden();
        var note = r.Text(NoteColumn);
        return r.Ok ? new DrillPointRow(r.Row, name, angle, isDefault, hidden, note) : null;
    }

    private static HoleSettings ReadSettings(XlsxPackage package, List<HoleWorkbookProblem> problems)
    {
        var starter = HoleSettings.Starter;
        var sheet = package.Sheet(SettingsTab);
        if (sheet is null)
        {
            problems.Add(new HoleWorkbookProblem(
                null,
                null,
                $"The hole workbook has no \"{SettingsTab}\" tab; the starter settings are used."));
            return starter;
        }

        // The header row is the first row with a "Setting" cell; rows below it are settings, to the first blank row.
        var headerRow = 0;
        for (var row = 1; row <= sheet.LastRow && headerRow == 0; row++)
        {
            for (var column = 1; column <= sheet.LastColumn; column++)
            {
                if (SameText(sheet.Text(row, column), SettingColumn))
                {
                    headerRow = row;
                    break;
                }
            }
        }

        if (headerRow == 0)
        {
            problems.Add(new HoleWorkbookProblem(
                SettingsTab,
                null,
                $"No row has a \"{SettingColumn}\" header. The header row should read: {string.Join(", ", SettingsHeaders)}; the starter settings are used."));
            return starter;
        }

        var columns = HeaderColumns(sheet, headerRow);
        var missing = SettingsHeaders.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(h => $"\"{h}\""));
            var noun = missing.Count == 1 ? "column" : "columns";
            problems.Add(new HoleWorkbookProblem(
                SettingsTab,
                headerRow,
                $"The {SettingsTab} tab has no {names} {noun}. Row {headerRow} should read: {string.Join(", ", SettingsHeaders)}; the starter settings are used."));
            return starter;
        }

        var rowsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var row = headerRow + 1; row <= sheet.LastRow && !sheet.IsRowBlank(row); row++)
        {
            var name = sheet.Text(row, columns[SettingColumn]);
            if (name.Length > 0)
            {
                rowsByName.TryAdd(name, row);
            }
        }

        RowReader? Setting(string name, string starterText)
        {
            if (rowsByName.TryGetValue(name, out var row))
            {
                return new RowReader(sheet, row, columns);
            }

            problems.Add(new HoleWorkbookProblem(
                SettingsTab,
                null,
                $"There is no \"{name}\" setting; {starterText} is used."));
            return null;
        }

        T Use<T>(RowReader? reader, string name, T value, T fallback, string starterText)
        {
            if (reader is null)
            {
                return fallback;
            }

            if (reader.Ok)
            {
                return value;
            }

            problems.AddRange(reader.Problems.Select(p =>
                new HoleWorkbookProblem(SettingsTab, reader.Row, $"{name}: {p}; {starterText} is used.")));
            return fallback;
        }

        var unitsReader = Setting(DefaultUnitsSetting, "Metric");
        var units = unitsReader?.Units(ValueColumn) ?? starter.DefaultUnits;
        units = Use(unitsReader, DefaultUnitsSetting, units, starter.DefaultUnits, "Metric");

        var threadReader = Setting(ThreadDepthSetting, "1.5");
        var thread = threadReader?.Positive(ValueColumn) ?? starter.ThreadDepthMultiplier;
        thread = Use(threadReader, ThreadDepthSetting, thread, starter.ThreadDepthMultiplier, "1.5");

        var drillReader = Setting(DrillExtraDepthSetting, "0.5");
        var drill = drillReader?.NonNegative(ValueColumn) ?? starter.DrillExtraDepthMultiplier;
        drill = Use(drillReader, DrillExtraDepthSetting, drill, starter.DrillExtraDepthMultiplier, "0.5");

        // The blind depth multiplier is newer than the rest, so a missing row is not reported.
        var blindReader = rowsByName.TryGetValue(BlindDepthSetting, out var blindRow)
            ? new RowReader(sheet, blindRow, columns)
            : null;
        var blind = blindReader?.Positive(ValueColumn) ?? HoleSettings.StarterBlindDepthMultiplier;
        blind = Use(blindReader, BlindDepthSetting, blind, HoleSettings.StarterBlindDepthMultiplier, "2");

        HoleColour Colour(string name, HoleColour fallback)
        {
            var starterText = $"{fallback.R}, {fallback.G}, {fallback.B}";
            var reader = Setting(name, starterText);
            var colour = reader is null
                ? fallback
                : new HoleColour(reader.Byte(RedColumn), reader.Byte(GreenColumn), reader.Byte(BlueColumn));
            return Use(reader, name, colour, fallback, starterText);
        }

        return new HoleSettings(
            units,
            thread,
            drill,
            Colour(PressFitColourSetting, starter.PressFitColour),
            Colour(NormalFitColourSetting, starter.NormalFitColour),
            Colour(SlipFitColourSetting, starter.SlipFitColour))
        {
            BlindDepthMultiplier = blind,
        };
    }

    /// <summary>Reads the cells of one row by header, collecting what is wrong instead of throwing.</summary>
    private sealed class RowReader(XlsxSheet sheet, int row, Dictionary<string, int> columns)
    {
        public int Row { get; } = row;

        public List<string> Problems { get; } = [];

        public bool Ok => Problems.Count == 0;

        public void Problem(string message) => Problems.Add(message);

        /// <summary>
        /// Reports a cell that could not be read: blank, an Excel error value (a formula that shows <c>#DIV/0!</c>
        /// and so on), or a value that fails for <paramref name="reason"/>.
        /// </summary>
        private void Invalid(string header, string text, string reason)
        {
            if (text.Length == 0)
            {
                Problem($"{header} is blank");
            }
            else if (columns.TryGetValue(header, out var column) && sheet.Cell(Row, column)?.Kind == XlsxCellKind.Error)
            {
                Problem($"{header} shows the error {text}; check its formula");
            }
            else
            {
                Problem($"{header} \"{text}\" {reason}");
            }
        }

        public string Text(string header) =>
            columns.TryGetValue(header, out var column) ? sheet.Text(Row, column) : "";

        public string Required(string header)
        {
            var text = Text(header);
            if (text.Length == 0)
            {
                Problem($"{header} is blank");
            }

            return text;
        }

        public decimal? Number(string header, string example)
        {
            var text = Text(header);
            if (TryParseNumber(text, out var value))
            {
                return value;
            }

            Invalid(header, text, $"is not a number, {example}");
            return null;
        }

        public decimal Positive(string header)
        {
            var text = Text(header);
            if (TryParseNumber(text, out var value) && value > 0)
            {
                return value;
            }

            Invalid(header, text, "is not a number greater than 0, for example 6.4");
            return 0m;
        }

        public decimal NonNegative(string header)
        {
            var text = Text(header);
            if (TryParseNumber(text, out var value) && value >= 0)
            {
                return value;
            }

            Invalid(header, text, "is not a number of 0 or more, for example 0.5");
            return 0m;
        }

        /// <summary>A positive number, or null when the cell is blank.</summary>
        public decimal? OptionalPositive(string header) =>
            Text(header).Length == 0 ? null : Positive(header);

        /// <summary>An angle in degrees, more than 0 and less than 180. Null when blank and not required.</summary>
        public decimal? Angle(string header, bool required)
        {
            var text = Text(header);
            if (text.Length == 0 && !required)
            {
                return null;
            }

            if (TryParseNumber(text, out var value) && value > 0 && value < 180)
            {
                return value;
            }

            Invalid(header, text, "is not an angle between 0 and 180 degrees, for example 90");
            return null;
        }

        public byte Byte(string header)
        {
            var text = Text(header);
            if (TryParseNumber(text, out var value) && value >= 0 && value <= 255 && value == decimal.Truncate(value))
            {
                return (byte)value;
            }

            Invalid(header, text, "is not a whole number from 0 to 255");
            return 0;
        }

        public HoleUnits Units(string header = UnitsColumn)
        {
            var text = Text(header);
            if (SameText(text, nameof(HoleUnits.Metric)))
            {
                return HoleUnits.Metric;
            }

            if (SameText(text, nameof(HoleUnits.Imperial)))
            {
                return HoleUnits.Imperial;
            }

            Problem($"{header} \"{text}\" is not Metric or Imperial");
            return HoleUnits.Metric;
        }

        public Holes.ClearanceFit ClearanceFit()
        {
            var text = Text(HoleFitColumn);
            if (TryParseEnum<Holes.ClearanceFit>(text, out var fit))
            {
                return fit;
            }

            Problem($"{HoleFitColumn} \"{text}\" is not one of Close, Normal or Loose");
            return default;
        }

        public Holes.PrecisionFit PrecisionFit()
        {
            var text = Text(FitColumn);
            if (TryParseEnum<Holes.PrecisionFit>(text, out var fit))
            {
                return fit;
            }

            Problem($"{FitColumn} \"{text}\" is not one of Slip, Normal or Press");
            return default;
        }

        public Holes.ToleranceKind? ToleranceKind()
        {
            var text = Text(TypeColumn);
            if (TryParseEnum<Holes.ToleranceKind>(text, out var kind))
            {
                return kind;
            }

            Problem($"{TypeColumn} \"{text}\" is not ISO or Limits");
            return null;
        }

        /// <summary>Y or N (also Yes, No, TRUE, FALSE; ignoring case). A blank cell gives <paramref name="blankMeans"/>.</summary>
        public bool YesNo(string header, bool blankMeans)
        {
            var text = Text(header);
            if (text.Length == 0)
            {
                return blankMeans;
            }

            if (SameText(text, "Y") || SameText(text, "Yes") || SameText(text, "TRUE"))
            {
                return true;
            }

            if (SameText(text, "N") || SameText(text, "No") || SameText(text, "FALSE"))
            {
                return false;
            }

            Problem($"{header} \"{text}\" is not Y or N");
            return blankMeans;
        }

        /// <summary>True for <c>Show = N</c>. A blank Show cell shows the row.</summary>
        public bool Hidden() => !YesNo(ShowColumn, blankMeans: true);

        private static bool TryParseEnum<T>(string text, out T value)
            where T : struct, Enum
        {
            foreach (var candidate in Enum.GetValues<T>())
            {
                if (SameText(text, candidate.ToString()))
                {
                    value = candidate;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Reads a cell's number in invariant culture, as the package stores it: an optional sign, a decimal point and
    /// an exponent are allowed (cached values such as <c>1E-3</c>); group separators are not.
    /// </summary>
    internal static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent
                | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture,
            out value);
}
