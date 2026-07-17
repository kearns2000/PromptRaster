using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptRaster.Internal;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class LayoutFallbackTests
{
    [Fact]
    public async Task LayoutFailure_FallsBackToText_WhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddPromptRaster();
        services.RemoveAll<ITextPageLayoutEngine>();
        services.AddSingleton<ITextPageLayoutEngine, ThrowingLayoutEngine>();

        var rasterizer = services.BuildServiceProvider().GetRequiredService<IPromptRasterizer>();

        var result = await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(8_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always, TreatAsProse = true });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.RenderingFailed);
        result.OriginalText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LayoutFailure_Throws_WhenFallbackDisabled()
    {
        var services = new ServiceCollection();
        services.AddPromptRaster(static options => options.FallbackToText = false);
        services.RemoveAll<ITextPageLayoutEngine>();
        services.AddSingleton<ITextPageLayoutEngine, ThrowingLayoutEngine>();

        var rasterizer = services.BuildServiceProvider().GetRequiredService<IPromptRasterizer>();

        var act = async () => await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(8_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always, TreatAsProse = true });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("layout failed");
    }

    private sealed class ThrowingLayoutEngine : ITextPageLayoutEngine
    {
        public TextPageLayoutResult Layout(
            string text,
            PromptRasterRenderSettings settings,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("layout failed");
    }
}
