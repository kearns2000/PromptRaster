using System.Text;
using FluentAssertions;
using PromptRaster.Internal;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class RenderingTests
{
    private static readonly PromptRasterRenderSettings DefaultSettings = new()
    {
        ImageWidth = 1_024,
        ImageHeight = 1_536,
        HorizontalPadding = 48,
        VerticalPadding = 48,
        FontSize = 17,
        LineSpacingMultiplier = 1.25f,
        IncludePageHeader = true,
    };

    private readonly SkiaTextPageLayoutEngine _layoutEngine = new();
    private readonly SkiaTextImageRenderer _renderer = new();

    [Fact]
    public async Task EveryImage_IsAValidPng_WithConfiguredDimensions()
    {
        var rasterizer = RasterizerFactory.Create(static options =>
        {
            options.ImageWidth = 800;
            options.ImageHeight = 1_200;
        });

        var result = await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(20_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Pages.Should().NotBeEmpty();

        foreach (var page in result.Pages)
        {
            Png.HasValidSignature(page.Data).Should().BeTrue();
            Png.ReadDimensions(page.Data).Should().Be((800, 1_200));
        }
    }

    [Fact]
    public async Task Pages_AreReturnedInNumericalOrder()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(40_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        result.Pages.Select(static p => p.PageNumber)
            .Should().BeEquivalentTo(
                Enumerable.Range(1, result.PageCount),
                static options => options.WithStrictOrdering());
        result.Pages.Should().AllSatisfy(p => p.TotalPages.Should().Be(result.PageCount));
    }

    [Fact]
    public async Task SourceRanges_ReconstructOriginalTextExactly()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = ProseGenerator.Generate(40_000);

        var result = await rasterizer.RasterizeAsync(
            text,
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        var reconstructed = new StringBuilder(text.Length);

        foreach (var page in result.Pages.OrderBy(static p => p.PageNumber))
        {
            reconstructed.Append(text.AsSpan(page.SourceStartIndex, page.SourceLength));
        }

        reconstructed.ToString().Should().Be(text);
    }

    [Fact]
    public async Task SourceRanges_AreContiguous_NoSkipsOrDuplicates()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = ProseGenerator.Generate(40_000);

        var result = await rasterizer.RasterizeAsync(
            text,
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        var expectedStart = 0;

        foreach (var page in result.Pages.OrderBy(static p => p.PageNumber))
        {
            page.SourceStartIndex.Should().Be(expectedStart);
            page.SourceLength.Should().BePositive();
            page.CharacterCount.Should().Be(page.SourceLength);
            expectedStart += page.SourceLength;
        }

        expectedStart.Should().Be(text.Length);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Layout_PreservesAllLineTerminatorStyles(string newline)
    {
        var text = $"First paragraph line one.{newline}{newline}Second paragraph.{newline}Third line.";

        var layout = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);

        Reconstruct(text, layout).Should().Be(text);
    }

    [Fact]
    public void Layout_PreservesParagraphBreaks_AsBlankLines()
    {
        const string text = "First paragraph.\n\nSecond paragraph.";

        var layout = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);

        var lines = layout.Pages.Single().Lines;
        lines.Select(static l => l.Text).Should().ContainInOrder("First paragraph.", "", "Second paragraph.");
        Reconstruct(text, layout).Should().Be(text);
    }

    [Fact]
    public void Layout_WrapsByMeasuredWidth_WithoutSplittingWords()
    {
        var text = string.Join(" ", Enumerable.Repeat("wrapping", 400));

        var layout = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);

        var lines = layout.Pages.SelectMany(static p => p.Lines).ToArray();
        lines.Length.Should().BeGreaterThan(1);
        lines.Should().AllSatisfy(static line =>
            line.Text.Trim().Split(' ').Should().AllSatisfy(static word => word.Should().Be("wrapping")));
        Reconstruct(text, layout).Should().Be(text);
    }

    [Fact]
    public void Layout_SplitsVeryLongUnbrokenTokens_WithoutInfiniteLoops()
    {
        var text = new string('x', 50_000);

        var layout = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);

        layout.Pages.Should().NotBeEmpty();
        Reconstruct(text, layout).Should().Be(text);
    }

    [Fact]
    public void Layout_IsDeterministic_ForTheSameSettings()
    {
        var text = ProseGenerator.Generate(30_000);

        var first = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);
        var second = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);

        second.Pages.Count.Should().Be(first.Pages.Count);
        second.Pages.Select(static p => (p.SourceStartIndex, p.SourceLength))
            .Should().Equal(first.Pages.Select(static p => (p.SourceStartIndex, p.SourceLength)));
    }

    [Fact]
    public void Layout_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _layoutEngine.Layout(ProseGenerator.Generate(30_000), DefaultSettings, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Render_RespectsCancellation()
    {
        var layout = _layoutEngine.Layout("Some text.", DefaultSettings, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _renderer.Render(layout.Pages[0], DefaultSettings, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task Rasterization_RespectsCancellation()
    {
        var rasterizer = RasterizerFactory.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(40_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Rendering_IsThreadSafe()
    {
        var rasterizer = RasterizerFactory.Create();
        var text = ProseGenerator.Generate(25_000);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(async () => await rasterizer.RasterizeAsync(
                text,
                AiProvider.OpenAI,
                new PromptRasterRequest { Mode = PromptRasterMode.Always }))));

        results.Should().AllSatisfy(static result =>
        {
            result.Encoding.Should().Be(PromptRasterEncoding.Images);
            result.Pages.Should().NotBeEmpty();
        });

        // Deterministic layout means every concurrent run produces the same pagination.
        results.Select(static r => r.PageCount).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public void Render_CanBeCalledRepeatedly_WithoutResourceExhaustion()
    {
        var layout = _layoutEngine.Layout(ProseGenerator.Generate(8_000), DefaultSettings, CancellationToken.None);
        var page = layout.Pages[0];

        // All SkiaSharp resources are disposed per call; repeated rendering must
        // neither throw nor accumulate native handles.
        for (var i = 0; i < 100; i++)
        {
            var data = _renderer.Render(page, DefaultSettings, CancellationToken.None);
            Png.HasValidSignature(data).Should().BeTrue();
        }
    }

    [Fact]
    public async Task WhitespaceOnlyInput_DoesNotRender()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync("  \n\n  \t ", AiProvider.OpenAI);

        result.Pages.Should().BeEmpty();
        result.PageCount.Should().Be(0);
    }

    [Fact]
    public void PageHeader_CanBeDisabled_AndAffectsCapacity()
    {
        var text = ProseGenerator.Generate(30_000);
        var withHeader = _layoutEngine.Layout(text, DefaultSettings, CancellationToken.None);
        var withoutHeader = _layoutEngine.Layout(
            text,
            DefaultSettings with { IncludePageHeader = false },
            CancellationToken.None);

        // Disabling the header frees vertical space, so capacity per page never shrinks.
        withoutHeader.Pages.Count.Should().BeLessThanOrEqualTo(withHeader.Pages.Count);
        Reconstruct(text, withoutHeader).Should().Be(text);
    }

    private static string Reconstruct(string source, TextPageLayoutResult layout)
    {
        var builder = new StringBuilder(source.Length);

        foreach (var page in layout.Pages.OrderBy(static p => p.PageNumber))
        {
            builder.Append(source.AsSpan(page.SourceStartIndex, page.SourceLength));
        }

        return builder.ToString();
    }
}
