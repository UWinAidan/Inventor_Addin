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
/// <para>
/// Version 2 (task 126): every seated hole stores its seat's dimensions, since they are editable on every kind, and a
/// tapped hole's fastener is optional, since its seat needs no row of its size. Version 1 data still reads: a seated
/// clearance or tapped hole has its fastener and no dimensions, and its dimensions read as null, which
/// <see cref="HoleRules"/> and the window take from the row the fastener names, so the stored row resolves to its
/// dimensions.
/// </para>
/// <para>
/// Version 3 (task 112): the Simple and Taper Tapped kinds and the Spotface seat. A Simple hole stores its
/// <see cref="DiameterName"/> (and the expression it was typed as) and no size; a spotface stores its diameter and depth;
/// a taper tapped hole its drill depth. Versions 1 and 2 hold none of these, and read as before.
/// </para>
/// </summary>
public sealed record HoleStoredData(HoleSpec Spec, HoleAppliedState Applied)
{
    /// <summary>The attribute set's name on the hole feature.</summary>
    public const string AttributeSetName = Branding.ShortName + "_Hole";

    /// <summary>The version written now (task 112).</summary>
    public const int CurrentVersion = 3;

    /// <summary>The oldest version read. Data with a version outside this and <see cref="CurrentVersion"/> is not read.</summary>
    public const int OldestVersion = 1;

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
    public const string DiameterName = "Diameter";
    public const string DiameterExpressionName = "DiameterExpression";
    public const string SpotfaceDiameterName = "SpotfaceDiameter";
    public const string SpotfaceDepthName = "SpotfaceDepth";
    public const string SpotfaceDepthExpressionName = "SpotfaceDepthExpression";
    public const string AppliedDiameterName = "AppliedDiameterCm";
    public const string AppliedIsoCodeName = "AppliedToleranceIsoCode";
    public const string AppliedUpperName = "AppliedToleranceUpperCm";
    public const string AppliedLowerName = "AppliedToleranceLowerCm";
    public const string AppliedColouredName = "AppliedColoured";

    /// <summary>
    /// The data as attributes, in a fixed order. The spec is normalised first; a choice that does not apply, or a
    /// default left null, is not written. The spec must be resolved (<see cref="HoleBuildResult.Resolved"/>): a value
    /// <see cref="Read"/> requires but the spec leaves blank (the size, a seat's dimensions, a dowel's
    /// tolerance, a blind clearance or dowel hole's depth) throws <see cref="ArgumentException"/>, so nothing is stored
    /// that cannot be read back. A seated clearance hole with no fastener (no row of its size; task 137) stores its
    /// Clearance fit instead.
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
        };

        // A Simple hole has no size (task 112).
        if (spec.Size.Length > 0)
        {
            attributes.Add(new HoleAttribute(SizeName, spec.Size));
        }

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
        if (spec.UsesClearanceFit)
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
        AddNumber(DiameterName, spec.Diameter);
        AddText(DiameterExpressionName, spec.DiameterExpression);
        AddNumber(SpotfaceDiameterName, spec.SpotfaceDiameter);
        AddNumber(SpotfaceDepthName, spec.SpotfaceDepth);
        AddText(SpotfaceDepthExpressionName, spec.SpotfaceDepthExpression);

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
            var version = new Reader(values, CurrentVersion).RequiredInt(VersionName);
            if (version is < OldestVersion or > CurrentVersion)
            {
                return CannotRead(
                    $"it has version {version}, and this add-in reads versions {OldestVersion} to {CurrentVersion}");
            }

            return new HoleStoredDataReadResult(new Reader(values, version).Data(), "");
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

        if (spec.Kind == HoleKind.Simple)
        {
            Require(spec.Diameter is not null, DiameterName);
        }
        else
        {
            Require(!string.IsNullOrWhiteSpace(spec.Size), SizeName);
        }

        // Every seat has its own dimensions (tasks 106 and 126). A clearance or tapped hole's seat may name no row: a
        // size with no row of the seat has none (tasks 126 and 137), and a dowel's follows no fastener.
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
        else if (spec.Seat == HoleSeat.Spotface)
        {
            Require(spec.SpotfaceDiameter is not null, SpotfaceDiameterName);
            Require(spec.SpotfaceDepth is not null, SpotfaceDepthName);
        }

        if (spec.Kind == HoleKind.Dowel)
        {
            Require(!string.IsNullOrWhiteSpace(spec.Tolerance), ToleranceName);
        }

        if (spec.IsBlind && !HoleStandards.IsThreaded(spec.Kind))
        {
            Require(spec.Depth is not null, DepthName);
        }
    }

    private static HoleStoredDataReadResult CannotRead(string reason) =>
        new(null, $"Cannot read the {Branding.ShortName} hole data on this hole: {reason}.");

    private sealed class StoredDataException(string message) : Exception(message);

    private sealed class Reader(Dictionary<string, object> values, int version)
    {
        public HoleStoredData Data()
        {
            var kind = RequiredEnum<HoleKind>(KindName);
            var seat = RequiredEnum<HoleSeat>(SeatName);
            var termination = RequiredEnum<HoleTermination>(TerminationName);
            var blind = termination == HoleTermination.Distance;
            // A seated clearance hole stores its fastener, and a seated tapped hole did until version 2 (task 099); from
            // version 2 a tapped hole's seat may have no row of its size, and so no fastener (task 126). Every seat
            // stores its dimensions: a dowel's since task 106, every kind's since version 2. Ones stored before have
            // none, and read as null.
            // From version 3 (task 112) a Simple hole has no size but its diameter, a spotface follows no fastener, and a
            // taper tapped hole has a drill depth and no thread depth.
            var cbore = seat == HoleSeat.Counterbore;
            var csink = seat == HoleSeat.Countersink;
            var spotface = seat == HoleSeat.Spotface;
            var simple = kind == HoleKind.Simple;
            var threaded = HoleStandards.IsThreaded(kind);
            var seated = (cbore || csink) && kind is HoleKind.Clearance or HoleKind.Tapped;
            // A seated clearance hole may have no fastener since task 137 (spec 09 question 26): its size has no row of
            // the seat, and it stores the Clearance fit of its through hole instead. Data stored before always has one.
            var fastenerRequired = seated && version < 2;
            var fastener = fastenerRequired ? RequiredText(FastenerName) : seated ? OptionalText(FastenerName) : null;
            var clearanceFitStored = kind == HoleKind.Clearance
                && (seat is HoleSeat.None or HoleSeat.Spotface || string.IsNullOrWhiteSpace(fastener));

            var spec = new HoleSpec(
                RequiredEnum<HoleUnits>(UnitsName),
                kind,
                seat,
                simple ? OptionalText(SizeName) ?? "" : RequiredText(SizeName),
                fastener,
                clearanceFitStored ? RequiredEnum<ClearanceFit>(ClearanceFitName) : ClearanceFit.Normal,
                kind == HoleKind.Dowel ? RequiredEnum<PrecisionFit>(PrecisionFitName) : PrecisionFit.Normal,
                kind == HoleKind.Dowel ? RequiredText(ToleranceName) : null,
                kind == HoleKind.Tapped ? OptionalDecimal(ThreadDepthName) : null,
                threaded ? OptionalDecimal(DrillDepthName) : null,
                termination,
                blind && !threaded ? RequiredDecimal(DepthName) : null,
                blind ? OptionalText(DrillPointName) : null,
                RequiredFlag(FlipName))
            {
                ThreadDepthExpression = kind == HoleKind.Tapped ? OptionalText(ThreadDepthExpressionName) : null,
                DrillDepthExpression = threaded ? OptionalText(DrillDepthExpressionName) : null,
                DepthExpression = blind && !threaded ? OptionalText(DepthExpressionName) : null,
                CboreDiameter = cbore ? OptionalDecimal(CboreDiameterName) : null,
                CboreDepth = cbore ? OptionalDecimal(CboreDepthName) : null,
                CboreDepthExpression = cbore ? OptionalText(CboreDepthExpressionName) : null,
                CsinkDiameter = csink ? OptionalDecimal(CsinkDiameterName) : null,
                CsinkAngleDeg = csink ? OptionalDecimal(CsinkAngleName) : null,
                SpotfaceDiameter = spotface ? OptionalDecimal(SpotfaceDiameterName) : null,
                SpotfaceDepth = spotface ? OptionalDecimal(SpotfaceDepthName) : null,
                SpotfaceDepthExpression = spotface ? OptionalText(SpotfaceDepthExpressionName) : null,
                Diameter = simple ? RequiredDecimal(DiameterName) : null,
                DiameterExpression = simple ? OptionalText(DiameterExpressionName) : null,
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
