using System.Reflection;

namespace InventorAddin.Core.Logging;

/// <summary>Builds the short text shown to the user when a command fails. The full exception goes to the log.</summary>
public static class ErrorMessages
{
    /// <summary>
    /// Text for a command that failed with <paramref name="ex"/>, in three parts separated by blank lines:
    /// which command failed, what went wrong, and (last line) where the log file is.
    /// </summary>
    /// <param name="commandDisplayName">The command's name as shown on its button.</param>
    /// <param name="ex">The exception that ended the command.</param>
    /// <param name="logFilePath">Full path of the log file, or null when no log file is being written.</param>
    public static string CommandFailed(string commandDisplayName, Exception ex, string? logFilePath)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var name = string.IsNullOrWhiteSpace(commandDisplayName) ? "The command" : commandDisplayName.Trim();
        var logLine = string.IsNullOrWhiteSpace(logFilePath)
            ? "No log file is available for the details."
            : $"Details are in the log file: {logFilePath}";

        return $"{name} failed.{Environment.NewLine}{Environment.NewLine}"
            + $"{MessageOf(ex)}{Environment.NewLine}{Environment.NewLine}"
            + logLine;
    }

    /// <summary>
    /// The message to show for <paramref name="ex"/>. A <see cref="TargetInvocationException"/> or
    /// <see cref="AggregateException"/> only wraps the real failure, so these are unwrapped (repeatedly)
    /// to the first exception that is not a wrapper. Other exceptions use their own message.
    /// An empty message is replaced by the exception's type name.
    /// </summary>
    public static string MessageOf(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var shown = Unwrap(ex);
        var message = shown.Message?.Trim();
        return string.IsNullOrEmpty(message) ? shown.GetType().Name : message;
    }

    private static Exception Unwrap(Exception ex)
    {
        var current = ex;
        // AggregateException.InnerException is its first inner exception.
        while (current is TargetInvocationException or AggregateException && current.InnerException is not null)
            current = current.InnerException;
        return current;
    }
}
