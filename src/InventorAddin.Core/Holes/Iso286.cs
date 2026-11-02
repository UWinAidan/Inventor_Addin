namespace InventorAddin.Core.Holes;

/// <summary>
/// The limits of an ISO 286 hole tolerance class, for example H7 at 6 mm, or why they could not be worked out.
/// Deviations are relative to the nominal size; upper is ES, lower is EI.
/// </summary>
/// <param name="Succeeded">False when the size or code is not covered; <see cref="Message"/> says why.</param>
/// <param name="Code">The code as given, trimmed.</param>
/// <param name="NominalMm">The nominal size as given, in mm.</param>
/// <param name="UpperUm">The upper deviation (ES) in micrometres; 0 on failure.</param>
/// <param name="LowerUm">The lower deviation (EI) in micrometres; 0 on failure.</param>
/// <param name="Message">Blank on success; the reason on failure.</param>
public sealed record Iso286Limits(
    bool Succeeded, string Code, double NominalMm, decimal UpperUm, decimal LowerUm, string Message)
{
    /// <summary>The upper deviation (ES) in mm.</summary>
    public double UpperMm => (double)(UpperUm / 1000m);

    /// <summary>The lower deviation (EI) in mm.</summary>
    public double LowerMm => (double)(LowerUm / 1000m);

    internal static Iso286Limits Success(string code, double nominalMm, decimal upperUm, decimal lowerUm) =>
        new(true, code, nominalMm, upperUm, lowerUm, "");

    internal static Iso286Limits Failure(string code, double nominalMm, string message) =>
        new(false, code, nominalMm, 0m, 0m, message);
}

/// <summary>
/// ISO 286-1 limits for holes (spec 09, "Dowel and precision holes"; shared with the hole table wizard, spec 04).
/// Inventor applies the fit itself in the model; this is for display and checking.
/// Covers fundamental deviations F, G, H, JS, K, M, N, P and grades IT5 to IT11, nominal sizes over 0 up to 500 mm.
/// Values are typed from the ISO 286-1 tables (standard tolerance grades, fundamental deviations for holes, and the
/// delta values), in micrometres.
/// </summary>
public static class Iso286
{
    /// <summary>The smallest grade covered.</summary>
    public const int MinGrade = 5;

    /// <summary>The largest grade covered.</summary>
    public const int MaxGrade = 11;

    /// <summary>The largest nominal size covered, in mm (inclusive).</summary>
    public const double MaxNominalMm = 500;

    /// <summary>The hole fundamental deviation letters covered, as ISO writes them.</summary>
    public static IReadOnlyList<string> Letters { get; } = new[] { "F", "G", "H", "JS", "K", "M", "N", "P" };

    // Upper bound (inclusive) of each nominal size step; a step runs "over" the previous bound "up to and including"
    // this one. The fundamental deviations of F to P do not change across the intermediate steps, so the main steps do.
    private static readonly double[] StepUpperMm = { 3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500 };

    // Standard tolerance grades IT4 to IT11, in micrometres, per size step (ISO 286-1, Table 1).
    // IT4 is kept for checking the delta table against the grades.
    private static readonly int[,] ItUm =
    {
        //  IT4 IT5 IT6 IT7 IT8  IT9 IT10 IT11
        {   3,   4,  6, 10, 14,  25,  40,  60 }, // over 0 to 3
        {   4,   5,  8, 12, 18,  30,  48,  75 }, // 3 to 6
        {   4,   6,  9, 15, 22,  36,  58,  90 }, // 6 to 10
        {   5,   8, 11, 18, 27,  43,  70, 110 }, // 10 to 18
        {   6,   9, 13, 21, 33,  52,  84, 130 }, // 18 to 30
        {   7,  11, 16, 25, 39,  62, 100, 160 }, // 30 to 50
        {   8,  13, 19, 30, 46,  74, 120, 190 }, // 50 to 80
        {  10,  15, 22, 35, 54,  87, 140, 220 }, // 80 to 120
        {  12,  18, 25, 40, 63, 100, 160, 250 }, // 120 to 180
        {  14,  20, 29, 46, 72, 115, 185, 290 }, // 180 to 250
        {  16,  23, 32, 52, 81, 130, 210, 320 }, // 250 to 315
        {  18,  25, 36, 57, 89, 140, 230, 360 }, // 315 to 400
        {  20,  27, 40, 63, 97, 155, 250, 400 }, // 400 to 500
    };

    private const int ItFirstGrade = 4;

    // Delta values for grades IT5 to IT8, in micrometres (ISO 286-1, Table 3, delta columns).
    private static readonly int[,] DeltaUm =
    {
        // IT5 IT6 IT7 IT8
        {  0,  0,  0,  0 }, // over 0 to 3
        {  1,  3,  4,  6 }, // 3 to 6
        {  2,  3,  6,  7 }, // 6 to 10
        {  3,  3,  7,  9 }, // 10 to 18
        {  3,  4,  8, 12 }, // 18 to 30
        {  4,  5,  9, 14 }, // 30 to 50
        {  5,  6, 11, 16 }, // 50 to 80
        {  5,  7, 13, 19 }, // 80 to 120
        {  6,  7, 15, 23 }, // 120 to 180
        {  6,  9, 17, 26 }, // 180 to 250
        {  7,  9, 20, 29 }, // 250 to 315
        {  7, 11, 21, 32 }, // 315 to 400
        {  7, 13, 23, 34 }, // 400 to 500
    };

    private const int DeltaFirstGrade = 5;

    // Lower deviation EI of F and G, in micrometres, per size step.
    private static readonly int[] FLowerUm = { 6, 10, 13, 16, 20, 25, 30, 36, 43, 50, 56, 62, 68 };
    private static readonly int[] GLowerUm = { 2, 4, 5, 6, 7, 9, 10, 12, 14, 15, 17, 18, 20 };

    // Upper deviation ES of K, M, N and P before delta, in micrometres, per size step. For K, M and N up to IT8 and
    // P up to IT7, delta is added (except over 0 to 3, where delta is 0 and the table gives the value directly).
    private static readonly int[] KUpperUm = { 0, -1, -1, -1, -2, -2, -2, -3, -3, -4, -4, -4, -5 };
    private static readonly int[] MUpperUm = { -2, -4, -6, -7, -8, -9, -11, -13, -15, -17, -20, -21, -23 };
    private static readonly int[] NUpperUm = { -4, -8, -10, -12, -15, -17, -20, -23, -27, -31, -34, -37, -40 };
    private static readonly int[] PUpperUm = { -6, -12, -15, -18, -22, -26, -32, -37, -43, -50, -56, -62, -68 };

    /// <summary>
    /// The upper and lower deviations of a hole of <paramref name="nominalMm"/> with ISO tolerance class
    /// <paramref name="code"/>, for example "H7". Codes are case-sensitive: hole letters are capitals, and a lowercase
    /// letter (a shaft, such as "n7") is refused. Never throws; a size or code that is not covered gives a failure.
    /// </summary>
    public static Iso286Limits HoleLimits(double nominalMm, string? code)
    {
        var trimmed = (code ?? "").Trim();

        if (double.IsNaN(nominalMm) || double.IsInfinity(nominalMm) || nominalMm <= 0 || nominalMm > MaxNominalMm)
            return Iso286Limits.Failure(trimmed, nominalMm,
                $"ISO 286 limits cover sizes over 0 up to {MaxNominalMm:0} mm; {Format(nominalMm)} mm is outside that.");

        if (trimmed.Length == 0)
            return Iso286Limits.Failure(trimmed, nominalMm, "No ISO 286 code was given.");

        var split = 0;
        while (split < trimmed.Length && char.IsLetter(trimmed[split]))
            split++;
        var letter = trimmed[..split];
        var gradeText = trimmed[split..];

        if (letter.Length == 0 || gradeText.Length == 0 || !gradeText.All(char.IsAsciiDigit))
            return Iso286Limits.Failure(trimmed, nominalMm,
                $"'{trimmed}' is not an ISO 286 code; expected a letter and a grade, for example H7.");

        if (letter.Any(char.IsLower))
        {
            if (letter.All(char.IsLower) && Letters.Contains(letter.ToUpperInvariant()))
                return Iso286Limits.Failure(trimmed, nominalMm,
                    $"'{trimmed}' is a shaft tolerance; holes use capital letters, for example {letter.ToUpperInvariant()}{gradeText}.");
            return Iso286Limits.Failure(trimmed, nominalMm,
                $"'{trimmed}' is not a hole tolerance; holes use capital letters, for example H7.");
        }

        if (!Letters.Contains(letter))
            return Iso286Limits.Failure(trimmed, nominalMm,
                $"Hole deviation '{letter}' is not supported; supported: {string.Join(", ", Letters)}.");

        if (!int.TryParse(gradeText, out var grade) || grade < MinGrade || grade > MaxGrade
            || (gradeText.Length > 1 && gradeText[0] == '0'))
            return Iso286Limits.Failure(trimmed, nominalMm,
                $"Grade IT{gradeText} is not supported; supported: IT{MinGrade} to IT{MaxGrade}.");

        var step = StepIndex(nominalMm);
        decimal it = It(step, grade);

        switch (letter)
        {
            case "H":
                return Iso286Limits.Success(trimmed, nominalMm, it, 0m);

            case "F":
                return FromLower(trimmed, nominalMm, FLowerUm[step], it);

            case "G":
                return FromLower(trimmed, nominalMm, GLowerUm[step], it);

            case "JS":
            {
                // Symmetric, +/- IT/2. For JS7 to JS11 an odd IT value is rounded to the even value below first.
                var half = grade >= 7 && it % 2 != 0 ? (it - 1) / 2 : it / 2;
                return Iso286Limits.Success(trimmed, nominalMm, half, -half);
            }

            case "K":
                // Above IT8, ISO 286-1 gives K only for sizes up to 3 mm (ES = 0).
                if (grade > 8 && step > 0)
                    return Iso286Limits.Failure(trimmed, nominalMm,
                        $"ISO 286-1 does not define {trimmed} for sizes over 3 mm.");
                return FromUpper(trimmed, nominalMm, KUpperUm[step] + (grade <= 8 ? Delta(step, grade) : 0), it);

            case "M":
            {
                // Special case in ISO 286-1: M6 over 250 to 315 has ES = -9 (not -20 + 9 = -11).
                if (grade == 6 && StepUpperMm[step] == 315)
                    return FromUpper(trimmed, nominalMm, -9, it);
                return FromUpper(trimmed, nominalMm, MUpperUm[step] + (grade <= 8 ? Delta(step, grade) : 0), it);
            }

            case "N":
                // Above IT8: ES = 0 for sizes over 3 mm; over 0 to 3 keeps -4. ISO 286-1 says N above IT8 is not
                // to be used for sizes up to 1 mm.
                if (grade > 8)
                {
                    if (nominalMm <= 1)
                        return Iso286Limits.Failure(trimmed, nominalMm,
                            $"ISO 286-1 does not use {trimmed} for sizes up to 1 mm.");
                    return FromUpper(trimmed, nominalMm, step == 0 ? NUpperUm[0] : 0, it);
                }
                return FromUpper(trimmed, nominalMm, NUpperUm[step] + Delta(step, grade), it);

            case "P":
                return FromUpper(trimmed, nominalMm, PUpperUm[step] + (grade <= 7 ? Delta(step, grade) : 0), it);

            default:
                return Iso286Limits.Failure(trimmed, nominalMm, $"Hole deviation '{letter}' is not supported.");
        }
    }

    /// <summary>The standard tolerance IT<paramref name="grade"/> in micrometres for a nominal size, IT5 to IT11.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The size or grade is outside the covered range.</exception>
    public static int StandardToleranceUm(double nominalMm, int grade)
    {
        if (double.IsNaN(nominalMm) || nominalMm <= 0 || nominalMm > MaxNominalMm)
            throw new ArgumentOutOfRangeException(nameof(nominalMm), nominalMm, "Sizes over 0 up to 500 mm.");
        if (grade < MinGrade || grade > MaxGrade)
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "Grades IT5 to IT11.");
        return It(StepIndex(nominalMm), grade);
    }

    /// <summary>
    /// The ISO 286-1 delta value in micrometres for a nominal size and grade IT5 to IT8, the amount added to the
    /// upper deviation of K, M and N up to IT8 and P up to IT7. It equals IT n minus IT n-1 for the size.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The size or grade is outside the covered range.</exception>
    public static int DeltaUmFor(double nominalMm, int grade)
    {
        if (double.IsNaN(nominalMm) || nominalMm <= 0 || nominalMm > MaxNominalMm)
            throw new ArgumentOutOfRangeException(nameof(nominalMm), nominalMm, "Sizes over 0 up to 500 mm.");
        if (grade < DeltaFirstGrade || grade > 8)
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "Delta values cover IT5 to IT8.");
        return Delta(StepIndex(nominalMm), grade);
    }

    /// <summary>IT4 in micrometres for a nominal size; used with <see cref="DeltaUmFor"/> for IT5.</summary>
    public static int It4Um(double nominalMm)
    {
        if (double.IsNaN(nominalMm) || nominalMm <= 0 || nominalMm > MaxNominalMm)
            throw new ArgumentOutOfRangeException(nameof(nominalMm), nominalMm, "Sizes over 0 up to 500 mm.");
        return It(StepIndex(nominalMm), 4);
    }

    /// <summary>The upper bound of each nominal size step in mm; a step runs over the previous bound up to this one.</summary>
    public static IReadOnlyList<double> SizeStepUpperMm { get; } = Array.AsReadOnly(StepUpperMm);

    private static Iso286Limits FromLower(string code, double nominalMm, int lowerUm, decimal it) =>
        Iso286Limits.Success(code, nominalMm, lowerUm + it, lowerUm);

    private static Iso286Limits FromUpper(string code, double nominalMm, int upperUm, decimal it) =>
        Iso286Limits.Success(code, nominalMm, upperUm, upperUm - it);

    private static int StepIndex(double nominalMm)
    {
        for (var i = 0; i < StepUpperMm.Length; i++)
            if (nominalMm <= StepUpperMm[i])
                return i;
        throw new ArgumentOutOfRangeException(nameof(nominalMm));
    }

    private static int It(int step, int grade) => ItUm[step, grade - ItFirstGrade];

    private static int Delta(int step, int grade) => DeltaUm[step, grade - DeltaFirstGrade];

    private static string Format(double value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
