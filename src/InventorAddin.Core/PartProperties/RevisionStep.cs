namespace InventorAddin.Core.PartProperties;

/// <summary>The next revision letter (Aidan, 2026-11-22, task 210).</summary>
public static class RevisionStep
{
    // I and O are left out (Default, so they are not mistaken for 1 and 0).
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// The revision after <paramref name="revision"/>: A to B, H to J, N to P, Z to AA, AZ to BA. A blank revision, or
    /// anything that is not letters, becomes <see cref="PartStatuses.FirstRevision"/>. Case is ignored and the
    /// result is upper case. A stored I or O (not produced here) steps as the letter before it, so I gives J.
    /// </summary>
    public static string Next(string? revision)
    {
        var text = PropertyValues.NormaliseText(revision).ToUpperInvariant();
        if (text.Length == 0 || text.Any(c => c is < 'A' or > 'Z'))
        {
            return PartStatuses.FirstRevision;
        }

        var digits = text.Select(c => c switch { 'I' => 'H', 'O' => 'N', _ => c }).ToArray();
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var index = Letters.IndexOf(digits[i]);
            if (index < Letters.Length - 1)
            {
                digits[i] = Letters[index + 1];
                return new string(digits);
            }

            digits[i] = Letters[0];
        }

        return Letters[0] + new string(digits);
    }
}
