using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class LoggingSafetyTests
{
    [Fact]
    public async Task Rasterizer_DoesNotLogRawPromptText()
    {
        const string secretMarker = "UNIQUE_PROMPT_TEXT_MARKER_9f3c2a";
        var sink = new CapturingLoggerProvider();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(sink).SetMinimumLevel(LogLevel.Trace));
        services.AddPromptRaster();
        var rasterizer = services.BuildServiceProvider().GetRequiredService<IPromptRasterizer>();

        var text = ProseGenerator.Generate(8_000) + " " + secretMarker;

        await rasterizer.RasterizeAsync(
            text,
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        sink.Messages.Should().NotBeEmpty("the rasteriser emits decision/page metadata");
        sink.Messages.Should().NotContain(m => m.Contains(secretMarker, StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Add(formatter(state, exception));
            }
        }
    }
}
