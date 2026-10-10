namespace InventorAddin.Core.Numbering;

/// <summary>
/// What <see cref="RegisterStore"/> needs from the machine it runs on: the time, who is assigning, on which computer,
/// and whether the shared register folder can be reached. Tests pass a fake with a fixed clock and a probe they control.
/// </summary>
public interface IRegisterEnvironment
{
    /// <summary>The current time. Used for <c>AssignedAt</c> and for the age of a lock file.</summary>
    DateTimeOffset Now { get; }

    /// <summary>The Windows user name, for <c>AssignedBy</c>.</summary>
    string UserName { get; }

    /// <summary>The computer name, for <c>Computer</c>.</summary>
    string ComputerName { get; }

    /// <summary>
    /// True when <paramref name="folder"/> exists and answers within <paramref name="timeout"/>. Must not block for
    /// much longer than the timeout, because a disconnected mapped drive can hang (spec 02, "Reachability").
    /// </summary>
    bool ProbeFolder(string folder, TimeSpan timeout);
}

/// <summary>The real <see cref="IRegisterEnvironment"/>: the system clock in UTC, <see cref="Environment"/> names, and a probe with a timeout.</summary>
public sealed class RegisterEnvironment : IRegisterEnvironment
{
    public static readonly RegisterEnvironment Instance = new();

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public string UserName => Environment.UserName;

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
