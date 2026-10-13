namespace InventorAddin.Core.Materials;

/// <summary>
/// What applying a <see cref="MaterialFinishChoice"/> did (spec 03). The window shows <see cref="Message"/> and the
/// number of <see cref="Notes"/> in its status line, and lists every note in the status' details.
/// </summary>
/// <param name="Succeeded">False when nothing was applied.</param>
/// <param name="Message">The status message, for example "Applied to 2 parts." Blank uses the window's own text.</param>
/// <param name="Notes">
/// Things worth knowing that did not stop the apply, one line each, for example
/// "Appearance 'X' not found; the part keeps its material's appearance" or "Skipped: ASSY_X (not a part)".
/// </param>
public sealed record MaterialFinishResult(bool Succeeded, string Message, IReadOnlyList<string> Notes)
{
    /// <summary>A successful result.</summary>
    public static MaterialFinishResult Success(string message, IEnumerable<string>? notes = null) =>
        new(true, message ?? "", ToList(notes));

    /// <summary>A failed result: nothing was applied.</summary>
    public static MaterialFinishResult Failure(string message, IEnumerable<string>? notes = null) =>
        new(false, message ?? "", ToList(notes));

    private static IReadOnlyList<string> ToList(IEnumerable<string>? notes) =>
        (notes ?? Array.Empty<string>()).ToList().AsReadOnly();
}
