using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// One attribute of the <see cref="HoleStoredData.AttributeSetName"/> attribute set on a hole feature. The value is a
/// <see cref="string"/>, an <see cref="int"/> or a <see cref="double"/>, the types Inventor attributes hold.
/// </summary>
public sealed record HoleAttribute(string Name, object Value);

/// <summary>
/// What a hole looks like in Inventor where the AWB fit shows: its diameter, its tolerance and whether its faces are
/// coloured. Stored on the hole when the add-in applies it, and read back from the hole by the add-in, so
/// <see cref="HoleStateComparison"/> can tell whether something other than AWB Hole changed it (spec 09).
/// </summary>
/// <param name="HoleDiameterCm">The modelled hole diameter.</param>
/// <param name="ToleranceIsoCode">The ISO fit code on the diameter (<c>H7</c>), or null.</param>
/// <param name="ToleranceUpperCm">The upper deviation, or null when the diameter has no tolerance.</param>
/// <param name="ToleranceLowerCm">The lower deviation, or null when the diameter has no tolerance.</param>
/// <param name="Coloured">The hole's faces carry a fit colour.</param>
public sealed record HoleAppliedState(
    double HoleDiameterCm,
    string? ToleranceIsoCode,
    double? ToleranceUpperCm,
    double? ToleranceLowerCm,
    bool Coloured)
{
    /// <summary>The state the add-in leaves a hole in when it applies <paramref name="geometry"/>.</summary>
    public static HoleAppliedState From(HoleGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return new HoleAppliedState(
            geometry.HoleDiameterCm,
            geometry.Tolerance?.IsoCode,
            geometry.Tolerance?.UpperCm,
            geometry.Tolerance?.LowerCm,
            geometry.IsColoured);
    }

    public bool HasTolerance => ToleranceIsoCode is not null || ToleranceUpperCm is not null || ToleranceLowerCm is not null;
}

/// <summary>Whether a hole still matches its stored data, and if not, what differs (one line each).</summary>
public sealed record HoleStateMatch(IReadOnlyList<string> Differences)
{
    public bool Matches => Differences.Count == 0;
}

/// <summary>
/// Compares a hole's current state with the state stored when AWB Hole last applied it, for the re-apply offer
/// (spec 09: a hole changed while Inventor's Hole was restored).
/// </summary>
public static class HoleStateComparison
{
    /// <summary>Lengths closer than this (cm, a tenth of a micrometre) are the same.</summary>
    public const double LengthToleranceCm = 1e-5;

    private const string CommandName = Branding.ShortName + " Hole";

    public static HoleStateMatch Compare(HoleAppliedState stored, HoleAppliedState current) =>
        Compare(stored, current, HoleUnits.Metric);

    /// <summary>As <see cref="Compare(HoleAppliedState, HoleAppliedState)"/>, with lengths written in <paramref name="units"/>' length (mm or in).</summary>
    public static HoleStateMatch Compare(HoleAppliedState stored, HoleAppliedState current, HoleUnits units)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(current);

        var differences = new List<string>();

        if (!Same(stored.HoleDiameterCm, current.HoleDiameterCm))
        {
            differences.Add(
                $"The hole diameter is {LengthText(current.HoleDiameterCm, units)}; {CommandName} made it {LengthText(stored.HoleDiameterCm, units)}.");
        }

        if (!SameTolerance(stored, current))
        {
            differences.Add(
                $"The tolerance is {ToleranceText(current, units)}; {CommandName} set {ToleranceText(stored, units)}.");
        }

        if (stored.Coloured != current.Coloured)
        {
            differences.Add(stored.Coloured
                ? "The hole is no longer coloured by fit."
                : $"The hole is coloured, but {CommandName} did not colour it.");
        }

        return new HoleStateMatch(differences.AsReadOnly());
    }

    private static bool SameTolerance(HoleAppliedState stored, HoleAppliedState current)
    {
        if (!stored.HasTolerance || !current.HasTolerance)
        {
            return stored.HasTolerance == current.HasTolerance;
        }

        // An ISO fit matches by its code when the hole reports one; otherwise by its limits.
        if (stored.ToleranceIsoCode is not null && current.ToleranceIsoCode is not null)
        {
            return string.Equals(
                stored.ToleranceIsoCode.Trim(), current.ToleranceIsoCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (stored.ToleranceIsoCode is null && current.ToleranceIsoCode is not null)
        {
            return false;
        }

        return Same(stored.ToleranceUpperCm, current.ToleranceUpperCm)
            && Same(stored.ToleranceLowerCm, current.ToleranceLowerCm);
    }

    private static bool Same(double? a, double? b) =>
        a is { } x && b is { } y ? Math.Abs(x - y) < LengthToleranceCm : a is null && b is null;

    /// <summary>A length in centimetres written in the units' length: <c>12.5 mm</c> to 4 decimals, <c>0.5 in</c> to 5.</summary>
    public static string LengthText(double cm, HoleUnits units) =>
        units == HoleUnits.Imperial
            ? (cm / 2.54).ToString("0.#####", CultureInfo.InvariantCulture) + " in"
            : (cm * 10).ToString("0.####", CultureInfo.InvariantCulture) + " mm";

    private static string ToleranceText(HoleAppliedState state, HoleUnits units)
    {
        if (!state.HasTolerance)
        {
            return "none";
        }

        if (state.ToleranceIsoCode is { } code)
        {
            return code;
        }

        var imperial = units == HoleUnits.Imperial;
        string Limit(double? cm) => cm is { } c
            ? (imperial ? c / 2.54 : c * 10).ToString(imperial ? "+0.#####;-0.#####;0" : "+0.####;-0.####;0", CultureInfo.InvariantCulture)
            : "?";
        return $"{Limit(state.ToleranceUpperCm)}/{Limit(state.ToleranceLowerCm)} {(imperial ? "in" : "mm")}";
    }
}

/// <summary>The result of <see cref="HoleStoredData.Read"/>: the data, or why it cannot be read.</summary>
public sealed record HoleStoredDataReadResult(HoleStoredData? Data, string Message)
{
    public bool Succeeded => Data is not null;
}

/// <summary>
/// The AWB settings stored on a hole feature, as the <see cref="AttributeSetName"/> attribute set (spec 09, "The
/// command"; spike 081): the spec the user chose and the state the add-in applied. Written with
/// <see cref="ToAttributes"/> and read back with <see cref="Read"/>. Enums are stored as their names, lengths as
/// doubles in the spec's units, flags as integers 0 or 1. A depth typed as an Inventor expression stores its text too
/// (<see cref="DepthExpressionName"/> and the like, task 100), beside the value it came to; data written before
/// task 100 has none, and reads as plain numbers, so the version is unchanged. A dowel's seat stores its own sizes
/// (<see cref="CboreDiameterName"/> and the like, task 106) and no fastener; a dowel stored before task 106 has a
/// fastener and no seat sizes, and reads with the sizes null, which <see cref="HoleReadBack"/> fills from the hole, so
/// the version is unchanged again.
/// </summary>
public sealed record HoleStoredData(HoleSpec Spec, HoleAppliedState Applied)
{
    /// <summary>The attribute set's name on the hole feature.</summary>
    public const string AttributeSetName = Branding.ShortName + "_Hole";

    /// <summary>The version written now. Data with another version is not read.</summary>
    public const int CurrentVersion = 1;

    public const string VersionName = "Version";
    public const string UnitsName = "Units";
    public const string KindName = "Kind";
    public const string SeatName = "Seat";
    public const string SizeName = "Size";
    public const string FastenerName = "Fastener";
    public const string ClearanceFitName = "ClearanceFit";
    public const string PrecisionFitName = "PrecisionFit";
    public const string ToleranceName = "Tolerance";
    public const string ThreadDepthName = "ThreadDepth";
    public const string DrillDepthName = "DrillDepth";
    public const string TerminationName = "Termination";
    public const string DepthName = "Depth";
    public const string DrillPointName = "DrillPoint";
    public const string ThreadDepthExpressionName = "ThreadDepthExpression";
    public const string DrillDepthExpressionName = "DrillDepthExpression";
    public const string DepthExpressionName = "DepthExpression";
    public const string FlipName = "Flip";
    public const string CboreDiameterName = "CboreDiameter";
    public const string CboreDepthName = "CboreDepth";
    public const string CboreDepthExpressionName = "CboreDepthExpression";
    public const string CsinkDiameterName = "CsinkDiameter";
    public const string CsinkAngleName = "CsinkAngleDeg";
    public const string AppliedDiameterName = "AppliedDiameterCm";
    public const string AppliedIsoCodeName = "AppliedToleranceIsoCode";
    public const string AppliedUpperName = "AppliedToleranceUpperCm";
    public const string AppliedLowerName = "AppliedToleranceLowerCm";
    public const string AppliedColouredName = "AppliedColoured";

    /// <summary>
    /// The data as attributes, in a fixed order. The spec is normalised first; a choice that does not apply, or a
    /// default left null, is not written. The spec must be resolved (<see cref="HoleBuildResult.Resolved"/>): a value
    /// <see cref="Read"/> requires but the spec leaves blank (the size, a seated hole's fastener, a dowel's tolerance,
    /// a blind clearance or dowel hole's depth) throws <see cref="ArgumentException"/>, so nothing is stored that
    /// cannot be read back.
    /// </summary>
    public IReadOnlyList<HoleAttribute> ToAttributes()
    {
        var spec = Spec.Normalised();
        RequireResolved(spec);
        var attributes = new List<HoleAttribute>
        {
            new(VersionName, CurrentVersion),
            new(UnitsName, spec.Units.ToString()),
            new(KindName, spec.Kind.ToString()),
            new(SeatName, spec.Seat.ToString()),
            new(SizeName, spec.Size),
        };

        void AddText(string name, string? value)
        {
            if (value is not null)
            {
                attributes.Add(new HoleAttribute(name, value));
            }
        }

        void AddNumber(string name, decimal? value)
        {
            if (value is { } v)
            {
                attributes.Add(new HoleAttribute(name, (double)v));
            }
        }

        AddText(FastenerName, spec.Fastener);
        if (UsesClearanceFit(spec))
        {
            attributes.Add(new HoleAttribute(ClearanceFitName, spec.ClearanceFit.ToString()));
        }

        if (spec.Kind == HoleKind.Dowel)
        {
            attributes.Add(new HoleAttribute(PrecisionFitName, spec.PrecisionFit.ToString()));
        }

        AddText(ToleranceName, spec.Tolerance);
        AddNumber(ThreadDepthName, spec.ThreadDepth);
        AddNumber(DrillDepthName, spec.DrillDepth);
        attributes.Add(new HoleAttribute(TerminationName, spec.Termination.ToString()));
        AddNumber(DepthName, spec.Depth);
        AddText(DrillPointName, spec.DrillPoint);
        attributes.Add(new HoleAttribute(FlipName, spec.Flip ? 1 : 0));
        AddText(ThreadDepthExpressionName, spec.ThreadDepthExpression);
        AddText(DrillDepthExpressionName, spec.DrillDepthExpression);
        AddText(DepthExpressionName, spec.DepthExpression);
        AddNumber(CboreDiameterName, spec.CboreDiameter);
        AddNumber(CboreDepthName, spec.CboreDepth);
        AddText(CboreDepthExpressionName, spec.CboreDepthExpression);
        AddNumber(CsinkDiameterName, spec.CsinkDiameter);
        AddNumber(CsinkAngleName, spec.CsinkAngleDeg);

        attributes.Add(new HoleAttribute(AppliedDiameterName, Applied.HoleDiameterCm));
        AddText(AppliedIsoCodeName, Applied.ToleranceIsoCode);
        if (Applied.ToleranceUpperCm is { } upper)
        {
            attributes.Add(new HoleAttribute(AppliedUpperName, upper));
        }

        if (Applied.ToleranceLowerCm is { } lower)
        {
            attributes.Add(new HoleAttribute(AppliedLowerName, lower));
        }

        attributes.Add(new HoleAttribute(AppliedColouredName, Applied.Coloured ? 1 : 0));
        return attributes.AsReadOnly();
    }

    /// <summary>
    /// Reads the data back from a hole's attributes. Names match exactly; attributes not listed here are ignored.
    /// An unknown version, a missing required value or a value of the wrong type gives a "cannot read" result,
    /// never a guess.
    /// </summary>
    public static HoleStoredDataReadResult Read(IEnumerable<HoleAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            values.TryAdd(attribute.Name, attribute.Value);
        }

        try
        {
            var version = new Reader(values).RequiredInt(VersionName);
            if (version != CurrentVersion)
            {
                return CannotRead($"it has version {version}, and this add-in reads version {CurrentVersion}");
            }

            return new HoleStoredDataReadResult(new Reader(values).Data(), "");
        }
        catch (StoredDataException ex)
        {
            return CannotRead(ex.Message);
        }
    }

    private static void RequireResolved(HoleSpec spec)
    {
        static void Require(bool present, string name)
        {
            if (!present)
            {
                throw new ArgumentException(
                    $"The hole spec has no {name}; store the resolved spec from HoleRules.Build.", nameof(Spec));
            }
        }

        Require(!string.IsNullOrWhiteSpace(spec.Size), SizeName);
        if (spec.Kind == HoleKind.Dowel)
        {
            // A dowel's seat follows no fastener; it has its own sizes (task 106).
            if (spec.Seat == HoleSeat.Counterbore)
            {
                Require(spec.CboreDiameter is not null, CboreDiameterName);
                Require(spec.CboreDepth is not null, CboreDepthName);
            }
            else if (spec.Seat == HoleSeat.Countersink)
            {
                Require(spec.CsinkDiameter is not null, CsinkDiameterName);
                Require(spec.CsinkAngleDeg is not null, CsinkAngleName);
            }
        }
        else if (spec.Seat != HoleSeat.None)
        {
            Require(!string.IsNullOrWhiteSpace(spec.Fastener), FastenerName);
        }

        if (spec.Kind == HoleKind.Dowel)
        {
            Require(!string.IsNullOrWhiteSpace(spec.Tolerance), ToleranceName);
        }

        if (spec.IsBlind && spec.Kind != HoleKind.Tapped)
        {
            Require(spec.Depth is not null, DepthName);
        }
    }

    private static HoleStoredDataReadResult CannotRead(string reason) =>
        new(null, $"Cannot read the {Branding.ShortName} hole data on this hole: {reason}.");

    private static bool UsesClearanceFit(HoleSpec spec) => spec.Kind == HoleKind.Clearance && spec.Seat == HoleSeat.None;

    private sealed class StoredDataException(string message) : Exception(message);

    private sealed class Reader(Dictionary<string, object> values)
    {
        public HoleStoredData Data()
        {
            var kind = RequiredEnum<HoleKind>(KindName);
            var seat = RequiredEnum<HoleSeat>(SeatName);
            var termination = RequiredEnum<HoleTermination>(TerminationName);
            var blind = termination == HoleTermination.Distance;
            // A seated clearance or tapped hole stores its fastener (a tapped hole's seat row too, task 099). A dowel's
            // seat has its own sizes instead (task 106); one stored before that has neither, and its sizes read as null.
            var dowel = kind == HoleKind.Dowel;
            var seated = seat != HoleSeat.None && !dowel;
            var dowelCbore = dowel && seat == HoleSeat.Counterbore;
            var dowelCsink = dowel && seat == HoleSeat.Countersink;

            var spec = new HoleSpec(
                RequiredEnum<HoleUnits>(UnitsName),
                kind,
                seat,
                RequiredText(SizeName),
                seated ? RequiredText(FastenerName) : null,
                kind == HoleKind.Clearance && seat == HoleSeat.None
                    ? RequiredEnum<ClearanceFit>(ClearanceFitName)
                    : ClearanceFit.Normal,
                kind == HoleKind.Dowel ? RequiredEnum<PrecisionFit>(PrecisionFitName) : PrecisionFit.Normal,
                kind == HoleKind.Dowel ? RequiredText(ToleranceName) : null,
                kind == HoleKind.Tapped ? OptionalDecimal(ThreadDepthName) : null,
                kind == HoleKind.Tapped ? OptionalDecimal(DrillDepthName) : null,
                termination,
                blind && kind != HoleKind.Tapped ? RequiredDecimal(DepthName) : null,
                blind ? OptionalText(DrillPointName) : null,
                RequiredFlag(FlipName))
            {
                ThreadDepthExpression = kind == HoleKind.Tapped ? OptionalText(ThreadDepthExpressionName) : null,
                DrillDepthExpression = kind == HoleKind.Tapped ? OptionalText(DrillDepthExpressionName) : null,
                DepthExpression = blind && kind != HoleKind.Tapped ? OptionalText(DepthExpressionName) : null,
                CboreDiameter = dowelCbore ? OptionalDecimal(CboreDiameterName) : null,
                CboreDepth = dowelCbore ? OptionalDecimal(CboreDepthName) : null,
                CboreDepthExpression = dowelCbore ? OptionalText(CboreDepthExpressionName) : null,
                CsinkDiameter = dowelCsink ? OptionalDecimal(CsinkDiameterName) : null,
                CsinkAngleDeg = dowelCsink ? OptionalDecimal(CsinkAngleName) : null,
            };

            var applied = new HoleAppliedState(
                RequiredDouble(AppliedDiameterName),
                OptionalText(AppliedIsoCodeName),
                OptionalDouble(AppliedUpperName),
                OptionalDouble(AppliedLowerName),
                RequiredFlag(AppliedColouredName));

            return new HoleStoredData(spec, applied);
        }

        private static StoredDataException Missing(string name) => new($"the value {name} is missing");

        private static StoredDataException Wrong(string name, object value, string expected) =>
            new($"the value {name} is \"{Convert.ToString(value, CultureInfo.InvariantCulture)}\", not {expected}");

        public string RequiredText(string name) => OptionalText(name) ?? throw Missing(name);

        public string? OptionalText(string name)
        {
            if (!values.TryGetValue(name, out var value))
            {
                return null;
            }

            return value is string text && text.Trim().Length > 0 ? text.Trim() : throw Wrong(name, value, "text");
        }

        public T RequiredEnum<T>(string name)
            where T : struct, Enum
        {
            var text = RequiredText(name);
            return Enum.TryParse<T>(text, ignoreCase: true, out var result) && Enum.IsDefined(result)
                && !int.TryParse(text, out _)
                ? result
                : throw Wrong(name, text, "one of " + string.Join(", ", Enum.GetNames<T>()));
        }

        public double RequiredDouble(string name) => OptionalDouble(name) ?? throw Missing(name);

        public double? OptionalDouble(string name)
        {
            if (!values.TryGetValue(name, out var value))
            {
                return null;
            }

            double? number = value switch
            {
                double d => d,
                float f => f,
                int i => i,
                short s => s,
                long l => l,
                decimal m => (double)m,
                _ => null,
            };

            return number is { } n && double.IsFinite(n) ? n : throw Wrong(name, value, "a number");
        }

        public decimal RequiredDecimal(string name) => OptionalDecimal(name) ?? throw Missing(name);

        // Lengths are written as doubles; converting back keeps 15 significant digits, so 1.25 reads back as 1.25.
        public decimal? OptionalDecimal(string name) => OptionalDouble(name) is { } d ? (decimal)d : null;

        public int RequiredInt(string name)
        {
            var number = RequiredDouble(name);
            return number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue
                ? (int)number
                : throw Wrong(name, number, "a whole number");
        }

        public bool RequiredFlag(string name) => RequiredInt(name) switch
        {
            0 => false,
            1 => true,
            var other => throw Wrong(name, other, "0 or 1"),
        };
    }
}
