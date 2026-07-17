using FluentAssertions;
using PromptRaster.Internal;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class DeterministicRenderingTests
{
    [Fact]
    public async Task IdenticalInputAndSettings_ProduceIdenticalPngBytes()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = ProseGenerator.Generate(12_000);
        var request = new PromptRasterRequest { Mode = PromptRasterMode.Always };

        var first = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI, request);
        var second = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI, request);

        first.Encoding.Should().Be(PromptRasterEncoding.Images);
        second.Pages.Should().HaveCount(first.Pages.Count);

        for (var i = 0; i < first.Pages.Count; i++)
        {
            second.Pages[i].Data.Should().Equal(first.Pages[i].Data);
        }
    }

    [Fact]
    public void CacheKey_IsStableForIdenticalInputs()
    {
        var settings = new PromptRasterRenderSettings
        {
            ImageWidth = 1_024,
            ImageHeight = 1_536,
            HorizontalPadding = 48,
            VerticalPadding = 48,
            FontSize = 17,
            LineSpacingMultiplier = 1.25f,
            IncludePageHeader = true,
        };

        const string text = "Stable cache key source text.";

        var first = PromptRasterCacheKey.Create(text, settings, profile: null);
        var second = PromptRasterCacheKey.Create(text, settings, profile: null);

        first.Should().Be(second);
    }

    [Fact]
    public void CacheKey_ChangesWhenFontSizeChanges()
    {
        var baseline = new PromptRasterRenderSettings
        {
            ImageWidth = 1_024,
            ImageHeight = 1_536,
            HorizontalPadding = 48,
            VerticalPadding = 48,
            FontSize = 17,
            LineSpacingMultiplier = 1.25f,
            IncludePageHeader = true,
        };

        var altered = baseline with { FontSize = 18 };

        PromptRasterCacheKey.Create("text", baseline, null)
            .Should().NotBe(PromptRasterCacheKey.Create("text", altered, null));
    }
}
