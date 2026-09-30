using System.Globalization;
using XISOStudio.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using AvaloniaLogEventLevel = Avalonia.Logging.LogEventLevel;
using SerilogLogEventLevel = Serilog.Events.LogEventLevel;

namespace XISOStudio.Tests.Services;

/// <summary>Tests that <see cref="AvaloniaSerilogSink"/> forwards Avalonia's diagnostics into the Serilog pipeline.</summary>
public class AvaloniaSerilogSinkTests
{
    [Theory]
    [InlineData(AvaloniaLogEventLevel.Verbose, false)]
    [InlineData(AvaloniaLogEventLevel.Debug, false)]
    [InlineData(AvaloniaLogEventLevel.Information, false)]
    [InlineData(AvaloniaLogEventLevel.Warning, true)]
    [InlineData(AvaloniaLogEventLevel.Error, true)]
    [InlineData(AvaloniaLogEventLevel.Fatal, true)]
    public void IsEnabledReturnsExpectedValue(AvaloniaLogEventLevel level, bool expected)
    {
        var (sink, _) = CreateSink();

        Assert.Equal(expected, sink.IsEnabled(level, "Layout"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Layout")]
    [InlineData("Visual")]
    [InlineData("Property")]
    public void IsEnabledIsIndependentOfArea(string area)
    {
        var (sink, _) = CreateSink();

        Assert.True(sink.IsEnabled(AvaloniaLogEventLevel.Warning, area));
        Assert.False(sink.IsEnabled(AvaloniaLogEventLevel.Information, area));
    }

    [Theory]
    [InlineData(AvaloniaLogEventLevel.Verbose, SerilogLogEventLevel.Verbose)]
    [InlineData(AvaloniaLogEventLevel.Debug, SerilogLogEventLevel.Debug)]
    [InlineData(AvaloniaLogEventLevel.Information, SerilogLogEventLevel.Information)]
    [InlineData(AvaloniaLogEventLevel.Warning, SerilogLogEventLevel.Warning)]
    [InlineData(AvaloniaLogEventLevel.Error, SerilogLogEventLevel.Error)]
    [InlineData(AvaloniaLogEventLevel.Fatal, SerilogLogEventLevel.Fatal)]
    public void LogMapsAvaloniaLevelToSerilogLevel(AvaloniaLogEventLevel level, SerilogLogEventLevel expected)
    {
        var (sink, log) = CreateSink();

        sink.Log(level, "Layout", new object(), "mapped event");

        Assert.Single(log.Events);
        Assert.Equal(expected, log.Events[0].Level);
    }

    [Fact]
    public void LogAddsAreaContextProperty()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", new object(), "area test");

        Assert.Single(log.Events);
        Assert.True(log.Events[0].Properties.TryGetValue("Area", out var area));
        Assert.Equal("Layout", Assert.IsType<ScalarValue>(area).Value);
    }

    [Fact]
    public void LogAddsSourceObjectProperty()
    {
        var (sink, log) = CreateSink();
        var source = new object();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", source, "source test");

        Assert.Single(log.Events);
        Assert.True(log.Events[0].Properties.TryGetValue("Source", out var sourceValue));
        Assert.Equal(source.ToString(), Assert.IsType<ScalarValue>(sourceValue).Value);
    }

    [Fact]
    public void LogAddsAvaloniaSourceContextProperty()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", new object(), "context test");

        Assert.Single(log.Events);
        Assert.True(log.Events[0].Properties.TryGetValue("SourceContext", out var context));
        Assert.Equal("Avalonia", Assert.IsType<ScalarValue>(context).Value);
    }

    [Fact]
    public void NonParamsOverloadForwardsEvent()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Error, "Visual", new object(), "simple message");

        Assert.Single(log.Events);
        Assert.Equal(SerilogLogEventLevel.Error, log.Events[0].Level);
        Assert.Equal("simple message", log.Events[0].RenderMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ParamsOverloadForwardsPropertyValues()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", new object(), "User {Name} has {Count} items", "bob", 3);

        Assert.Single(log.Events);
        Assert.Equal("User \"bob\" has 3 items", log.Events[0].RenderMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ParamsOverloadForwardsPropertyNames()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", new object(), "User {Name} has {Count} items", "bob", 3);

        Assert.Single(log.Events);
        Assert.True(log.Events[0].Properties.ContainsKey("Name"));
        Assert.True(log.Events[0].Properties.ContainsKey("Count"));
    }

    [Fact]
    public void ParamsOverloadRendersTemplateWithAllValues()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", new object(), "{First}-{Second}-{Third}", "a", "b", "c");

        Assert.Single(log.Events);
        Assert.Equal("\"a\"-\"b\"-\"c\"", log.Events[0].RenderMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LogWithNullSourceDoesNotThrow()
    {
        var (sink, log) = CreateSink();

        var exception = Record.Exception(() => sink.Log(AvaloniaLogEventLevel.Warning, "Layout", null, "null source"));

        Assert.Null(exception);
        Assert.Single(log.Events);
    }

    [Fact]
    public void LogWithStringSourceKeepsValue()
    {
        var (sink, log) = CreateSink();

        sink.Log(AvaloniaLogEventLevel.Warning, "Layout", "text-source", "string source");

        Assert.Single(log.Events);
        Assert.True(log.Events[0].Properties.TryGetValue("Source", out var sourceValue));
        Assert.Equal("text-source", Assert.IsType<ScalarValue>(sourceValue).Value);
    }

    [Fact]
    public void LogWithThrowingSinkDoesNotPropagate()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ThrowingSink())
            .CreateLogger();
        var sink = new AvaloniaSerilogSink(logger);

        var exception =
            Record.Exception(() => sink.Log(AvaloniaLogEventLevel.Error, "Layout", new object(), "will throw"));

        Assert.Null(exception);
    }

    [Fact]
    public void ParamsOverloadWithThrowingSinkDoesNotPropagate()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ThrowingSink())
            .CreateLogger();
        var sink = new AvaloniaSerilogSink(logger);

        var exception = Record.Exception(() =>
            sink.Log(AvaloniaLogEventLevel.Error, "Layout", new object(), "will throw {Value}", 1));

        Assert.Null(exception);
    }

    private static (AvaloniaSerilogSink Sink, TestLogger Log) CreateSink()
    {
        var log = new TestLogger();
        return (new AvaloniaSerilogSink(log.Logger), log);
    }

    private sealed class ThrowingSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            throw new InvalidOperationException("sink failure");
        }
    }
}