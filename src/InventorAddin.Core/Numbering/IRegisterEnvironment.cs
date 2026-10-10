namespace InventorAddin.Core.Numbering;

/// <summary>
/// What <see cref="RegisterStore"/> needs from the machine it runs on: the time, who is assigning, on which computer,
/// and whether the shared register folder can be reached. Tests pass a fake with a fixed clock and a probe they control.
/// </summary>
public interface IRegisterEnvironment
{
    /// <summary>The current time. Used for <c>AssignedAt</c> and for the age of a lock file.</summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Who is assigning, for <c>AssignedBy</c> and the lock file: the person, as
    /// <see cref="RegisterEnvironment.AssignedByFor"/> gives it (spec 02, "AssignedBy is the person").
    /// </summary>
    string AssignedBy { get; }

    /// <summary>The computer name, for <c>Computer</c>.</summary>
    string ComputerName { get; }

    /// <summary>
    /// True when <paramref name="folder"/> exists and answers within <paramref name="timeout"/>. Must not block for
    /// much longer than the timeout, because a disconnected mapped drive can hang (spec 02, "Reachability").
    /// </summary>
    bool ProbeFolder(string folder, TimeSpan timeout);
}

/// <summary>
/// The real <see cref="IRegisterEnvironment"/>: the system clock in UTC, the <c>AssignedBy</c> value it was given,
/// the <see cref="Environment"/> computer name, and a probe with a timeout.
/// </summary>
public sealed class RegisterEnvironment : IRegisterEnvironment
{
    /// <param name="assignedBy">Who is assigning; build it with <see cref="AssignedByFor"/>. Null reads as blank.</param>
    public RegisterEnvironment(string? assignedBy)
    {
        AssignedBy = assignedBy ?? "";
    }

    /// <summary>
    /// Spec 02, "AssignedBy is the person": the trimmed <paramref name="defaultDesigner"/> from Settings when it is not
    /// blank, otherwise the trimmed <paramref name="windowsUserName"/>. Both blank gives <c>""</c>.
    /// </summary>
    public static string AssignedByFor(string? defaultDesigner, string? windowsUserName)
    {
        var designer = defaultDesigner?.Trim() ?? "";
        return designer.Length > 0 ? designer : windowsUserName?.Trim() ?? "";
    }

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public string AssignedBy { get; }

    public string ComputerName => Environment.MachineName;

    /// <summary>
    /// Runs <see cref="Directory.Exists(string)"/> on a background task and waits up to <paramref name="timeout"/>.
    /// No answer in time counts as unreachable; the background task is left to finish on its own.
    /// </summary>
    public bool ProbeFolder(string folder, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var probe = Task.Run(() => Directory.Exists(folder));
        try
        {
            return probe.Wait(timeout) && probe.Result;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
