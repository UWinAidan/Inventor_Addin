using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Gives the open file a part number from the register and writes it as the file's Part Number (spec 02, "Commands:
/// Generate part number"). The Part Properties window calls it from its Generate button; the add-in supplies it, and
/// tests use a fake.
/// </summary>
public interface IPartNumberAssigner
{
    /// <summary>
    /// Assigns the next number and writes it to the file. Throws, with a message fit to show the user, when it cannot;
    /// the message says whether a number may already have been taken from the register.
    /// </summary>
    PartNumberAssignment Assign();
}

/// <summary>What <see cref="IPartNumberAssigner.Assign"/> did.</summary>
/// <param name="PartNumber">The number assigned and written, as text, for example <c>AWB-10001</c>.</param>
/// <param name="Offline">
/// True when the shared register could not be reached: the number came from this PC's copy and is added to the shared
/// register at the next sync.
/// </param>
/// <param name="DuplicateMessages">One line per duplicate the sync found (see <see cref="DescribeDuplicate"/>); empty when none.</param>
public sealed record PartNumberAssignment(string PartNumber, bool Offline, IReadOnlyList<string> DuplicateMessages)
{
    /// <summary>
    /// A line for the user naming a number handed out twice and both files, for example
    /// <c>AWB-10005 was given to two files: 'C:\A\one.ipt' and 'C:\B\two.ipt'.</c>
    /// </summary>
    public static string DescribeDuplicate(RegisterDuplicate duplicate)
    {
        ArgumentNullException.ThrowIfNull(duplicate);
        return $"{duplicate.Number} was given to two files: '{duplicate.Existing.FilePath}' and '{duplicate.Added.FilePath}'.";
    }
}
