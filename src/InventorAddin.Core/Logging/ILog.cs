namespace InventorAddin.Core.Logging;

/// <summary>
/// Diagnostic log. Implementations never throw to the caller: an entry that cannot be written is dropped.
/// </summary>
public interface ILog
{
    void Info(string message);

    void Warn(string message);

    /// <summary>Logs an error. When <paramref name="ex"/> is given, its full text follows the entry.</summary>
    void Error(string message, Exception? ex = null);
}

/// <summary>A log that discards everything. For code that runs before the real log exists, and for tests.</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string message) { }

    public void Warn(string message) { }

    public void Error(string message, Exception? ex = null) { }
}
