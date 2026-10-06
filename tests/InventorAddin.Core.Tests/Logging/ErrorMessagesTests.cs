using System.Reflection;
using InventorAddin.Core.Logging;

namespace InventorAddin.Core.Tests.Logging;

public sealed class ErrorMessagesTests
{
    private const string LogPath = @"C:\Users\someone\AppData\Roaming\InventorWorkflowTools\workflowtools.log";

    private static string[] Lines(string text) => text.Split(Environment.NewLine);

    // ---- CommandFailed: layout ----

    [Fact]
    public void CommandFailed_FirstLine_NamesTheCommand()
    {
        var text = ErrorMessages.CommandFailed("Export Model Data", new InvalidOperationException("No document is open."), LogPath);

        Assert.Equal("Export Model Data failed.", Lines(text)[0]);
    }

    [Fact]
    public void CommandFailed_ContainsTheExceptionMessage()
    {
        var text = ErrorMessages.CommandFailed("Export Model Data", new InvalidOperationException("No document is open."), LogPath);

        Assert.Contains("No document is open.", Lines(text));
    }

    [Fact]
    public void CommandFailed_LastLine_GivesTheLogFilePath()
    {
        var text = ErrorMessages.CommandFailed("Export Model Data", new InvalidOperationException("x"), LogPath);

        Assert.Equal($"Details are in the log file: {LogPath}", Lines(text)[^1]);
    }

    [Fact]
    public void CommandFailed_HasThreePartsSeparatedByBlankLines()
    {
        var text = ErrorMessages.CommandFailed("Export Libraries", new InvalidOperationException("Boom"), LogPath);

        Assert.Equal(
            new[] { "Export Libraries failed.", "", "Boom", "", $"Details are in the log file: {LogPath}" },
            Lines(text));
    }

    [Fact]
    public void CommandFailed_DoesNotIncludeTheStackTraceOrTypeName()
    {
        Exception thrown;
        try { throw new InvalidOperationException("Boom"); }
        catch (Exception ex) { thrown = ex; }

        var text = ErrorMessages.CommandFailed("Export Libraries", thrown, LogPath);

        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain(" at ", text);
        Assert.DoesNotContain(nameof(CommandFailed_DoesNotIncludeTheStackTraceOrTypeName), text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandFailed_WithoutLogFile_LastLineSaysNoLogFile(string? logPath)
    {
        var text = ErrorMessages.CommandFailed("Export Libraries", new InvalidOperationException("Boom"), logPath);

        Assert.Equal("No log file is available for the details.", Lines(text)[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void CommandFailed_WithBlankDisplayName_UsesGenericName(string displayName)
    {
        var text = ErrorMessages.CommandFailed(displayName, new InvalidOperationException("Boom"), LogPath);

        Assert.Equal("The command failed.", Lines(text)[0]);
    }

    [Fact]
    public void CommandFailed_UnwrapsTargetInvocationException()
    {
        var ex = new TargetInvocationException(new ArgumentException("Bad value."));

        var text = ErrorMessages.CommandFailed("Export Libraries", ex, LogPath);

        Assert.Equal("Bad value.", Lines(text)[2]);
    }

    [Fact]
    public void CommandFailed_NullException_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ErrorMessages.CommandFailed("Export Libraries", null!, LogPath));
    }

    // ---- MessageOf ----

    [Fact]
    public void MessageOf_PlainException_UsesItsOwnMessage()
    {
        Assert.Equal("Outer", ErrorMessages.MessageOf(new InvalidOperationException("Outer")));
    }

    [Fact]
    public void MessageOf_NonWrapperWithInner_UsesOuterMessage()
    {
        var ex = new InvalidOperationException("Outer", new IOException("Inner"));

        Assert.Equal("Outer", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_TargetInvocationException_UsesInnerMessage()
    {
        var ex = new TargetInvocationException(new InvalidOperationException("Inner"));

        Assert.Equal("Inner", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_AggregateException_UsesInnerMessage()
    {
        var ex = new AggregateException(new InvalidOperationException("Inner"));

        Assert.Equal("Inner", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_AggregateExceptionWithSeveralInners_UsesFirstInner()
    {
        var ex = new AggregateException(new InvalidOperationException("First"), new IOException("Second"));

        Assert.Equal("First", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_NestedWrappers_UnwrapsToInnerMost()
    {
        var ex = new TargetInvocationException(
            new AggregateException(
                new TargetInvocationException(new InvalidOperationException("Deepest"))));

        Assert.Equal("Deepest", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_WrapperAroundNonWrapperWithInner_StopsAtFirstNonWrapper()
    {
        var ex = new TargetInvocationException(new InvalidOperationException("Real", new IOException("Cause")));

        Assert.Equal("Real", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_WrapperWithoutInner_UsesWrapperMessage()
    {
        var ex = new TargetInvocationException("Wrapper only", null);

        Assert.Equal("Wrapper only", ErrorMessages.MessageOf(ex));
    }

    [Fact]
    public void MessageOf_EmptyMessage_UsesTypeName()
    {
        Assert.Equal(nameof(EmptyMessageException), ErrorMessages.MessageOf(new EmptyMessageException()));
    }

    [Fact]
    public void MessageOf_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Spaced", ErrorMessages.MessageOf(new InvalidOperationException("  Spaced \r\n")));
    }

    private sealed class EmptyMessageException : Exception
    {
        public override string Message => "";
    }
}
