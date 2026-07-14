using FluentAssertions;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class PromptRasterizerDecisionTests
{
    // Dense prose lays out at roughly 7,000-7,400 characters per page with the
    // default render settings, which sits between the Gemini/OpenAI thresholds
    // (5,500/6,000) and the Anthropic threshold (8,000).
    private static string DenseProse(int length = 40_000) => ProseGenerator.Generate(length);

    [Fact]
    public async Task NullText_ThrowsArgumentNullException()
    {
        var rasterizer = RasterizerFactory.Create();

        var act = async () => await rasterizer.RasterizeAsync(null!, AiProvider.OpenAI);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public async Task EmptyOrWhitespaceText_RemainsText(string text)
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.EmptyInput);
        result.Pages.Should().BeEmpty();
        result.PageCount.Should().Be(0);
    }

    [Fact]
    public async Task WhitespaceOnlyText_RemainsText_EvenWhenForced()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            "   \n   ",
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.EmptyInput);
    }

    [Fact]
    public async Task UnknownProvider_RemainsText()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.Unknown);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.UnknownProvider);
        result.OriginalText.Should().Be(DenseProse());
    }

    [Fact]
    public async Task ShortText_RemainsText()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            "A short note that is clearly under the minimum length.",
            AiProvider.OpenAI);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.TextTooShort);
    }

    [Fact]
    public async Task LongDenseProse_AboveOpenAIThreshold_BecomesImages()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.OpenAI);

        result.Encoding.Should().Be(PromptRasterEncoding.Images);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.Rasterised);
        result.AverageCharactersPerPage.Should().BeGreaterThanOrEqualTo(6_000);
        result.Pages.Should().NotBeEmpty();
        result.Pages.Should().AllSatisfy(static p => p.MediaType.Should().Be("image/png"));
    }

    [Fact]
    public async Task LongDenseProse_BelowAnthropicThreshold_RemainsText()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.Anthropic);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.InsufficientCharacterDensity);
        result.RequiredCharactersPerPage.Should().Be(8_000);
        result.AverageCharactersPerPage.Should().BeLessThan(8_000);
        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task Gemini_UsesItsDefaultThreshold_WhereAnthropicDoesNot()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = DenseProse();

        var geminiResult = await rasterizer.RasterizeAsync(text, AiProvider.Gemini);
        var anthropicResult = await rasterizer.RasterizeAsync(text, AiProvider.Anthropic);

        geminiResult.Encoding.Should().Be(PromptRasterEncoding.Images);
        geminiResult.RequiredCharactersPerPage.Should().Be(5_500);
        anthropicResult.Encoding.Should().Be(PromptRasterEncoding.Text);
    }

    [Fact]
    public async Task Gemini_UsesConfiguredThreshold()
    {
        var rasterizer = RasterizerFactory.Create(static options =>
            options.GeminiMinimumCharactersPerPage = 20_000);

        var result = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.Gemini);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.InsufficientCharacterDensity);
        result.RequiredCharactersPerPage.Should().Be(20_000);
    }

    [Fact]
    public async Task RequestThreshold_OverridesProviderDefault()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = DenseProse();

        var withDefault = await rasterizer.RasterizeAsync(text, AiProvider.Anthropic);
        var withOverride = await rasterizer.RasterizeAsync(
            text,
            AiProvider.Anthropic,
            new PromptRasterRequest { MinimumCharactersPerPage = 6_000 });

        withDefault.Encoding.Should().Be(PromptRasterEncoding.Text);
        withOverride.Encoding.Should().Be(PromptRasterEncoding.Images);
        withOverride.RequiredCharactersPerPage.Should().Be(6_000);
    }

    [Fact]
    public async Task ModeNever_AlwaysReturnsText_WithoutLayout()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            DenseProse(),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Never });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.RasterisationDisabled);
        result.PageCount.Should().Be(0, "Never mode must not perform layout");
        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task ModeAlways_ProducesImages_EvenForShortText()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            "Short but forced.",
            AiProvider.Unknown,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Encoding.Should().Be(PromptRasterEncoding.Images);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.ForcedRasterisation);
        result.PageCount.Should().Be(1);
        result.Pages.Should().HaveCount(1);
    }

    [Fact]
    public async Task ModeAlways_SkipsContentSuitabilityDetection()
    {
        var rasterizer = RasterizerFactory.Create();
        var json = "{\"items\": [" + string.Join(",", Enumerable.Range(0, 900).Select(static i => $"{{\"id\": {i}}}")) + "]}";

        var result = await rasterizer.RasterizeAsync(
            json,
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Encoding.Should().Be(PromptRasterEncoding.Images);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.ForcedRasterisation);
    }

    [Fact]
    public async Task MaximumPageCount_PreventsAutomaticRasterisation()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            DenseProse(),
            AiProvider.OpenAI,
            new PromptRasterRequest { MaximumPages = 2 });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.MaximumPageCountExceeded);
        result.PageCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task AbsoluteMaximumPageCount_PreventsForcedRasterisation()
    {
        var rasterizer = RasterizerFactory.Create(static options =>
        {
            options.MaximumPages = 2;
            options.AbsoluteMaximumPages = 2;
        });

        var result = await rasterizer.RasterizeAsync(
            DenseProse(),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.MaximumPageCountExceeded);
        result.Decision.Description.Should().Contain("absolute");
    }

    [Fact]
    public async Task StructuredContent_RemainsText()
    {
        var rasterizer = RasterizerFactory.Create();
        var json = "{\"items\": [" + string.Join(",", Enumerable.Range(0, 900).Select(static i => $"{{\"id\": {i}}}")) + "]}";

        var result = await rasterizer.RasterizeAsync(json, AiProvider.OpenAI);

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.UnsuitableContent);
    }

    [Fact]
    public async Task TreatAsProse_BypassesContentDetection_ButNotDensityCheck()
    {
        var rasterizer = RasterizerFactory.Create();
        var json = "{\"items\": [" + string.Join(",", Enumerable.Range(0, 900).Select(static i => $"{{\"id\": {i}}}")) + "]}";

        var result = await rasterizer.RasterizeAsync(
            json,
            AiProvider.OpenAI,
            new PromptRasterRequest { TreatAsProse = true });

        result.Decision.Reason.Should().NotBe(
            PromptRasterDecisionReason.UnsuitableContent,
            "TreatAsProse must skip the classifier");
        result.Decision.Reason.Should().NotBe(
            PromptRasterDecisionReason.ForcedRasterisation,
            "TreatAsProse must not force rasterisation");
    }

    [Fact]
    public async Task DecisionDescriptions_ContainUsefulValues()
    {
        var rasterizer = RasterizerFactory.Create();

        var tooShort = await rasterizer.RasterizeAsync("Too short.", AiProvider.OpenAI);
        tooShort.Decision.Description.Should().Contain("5,000").And.Contain("10");

        var belowThreshold = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.Anthropic);
        belowThreshold.Decision.Description.Should().Contain("Anthropic").And.Contain("8,000");

        var rasterised = await rasterizer.RasterizeAsync(DenseProse(), AiProvider.OpenAI);
        rasterised.Decision.Description.Should().Contain("OpenAI").And.Contain("6,000");
    }

    [Fact]
    public async Task Result_ContainsUppercaseSha256OfUtf8Source()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync("abc", AiProvider.OpenAI);

        result.SourceSha256.Should().Be("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
    }

    [Fact]
    public async Task Result_AlwaysPreservesOriginalText()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = DenseProse();

        var imageResult = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI);
        var textResult = await rasterizer.RasterizeAsync(text, AiProvider.Unknown);

        imageResult.OriginalText.Should().BeSameAs(text);
        textResult.OriginalText.Should().BeSameAs(text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task InvalidRequestMaximumPages_Throws(int maximumPages)
    {
        var rasterizer = RasterizerFactory.Create();

        var act = async () => await rasterizer.RasterizeAsync(
            "text",
            AiProvider.OpenAI,
            new PromptRasterRequest { MaximumPages = maximumPages });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task InvalidRequestMinimumCharactersPerPage_Throws()
    {
        var rasterizer = RasterizerFactory.Create();

        var act = async () => await rasterizer.RasterizeAsync(
            "text",
            AiProvider.OpenAI,
            new PromptRasterRequest { MinimumCharactersPerPage = 0 });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
