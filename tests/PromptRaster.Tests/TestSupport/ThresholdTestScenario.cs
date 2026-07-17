namespace PromptRaster.Tests.TestSupport;

/// <summary>
/// Builds a rasteriser/text pair whose measured characters-per-page lands in the
/// 6,000-7,999 band on the current platform. Linux and macOS use different
/// <see cref="SkiaSharp.SKTypeface.Default"/> metrics, so a fixed prose length
/// is not reliable.
/// </summary>
internal static class ThresholdTestScenario
{
    public const int BandMinimum = 6_000;
    public const int BandMaximum = 7_999;

    private static readonly Action<PromptRasterOptions>[] RenderProfiles =
    [
        static _ => { },
        static options => options.FontSize = 14f,
        static options => options.FontSize = 13f,
        static options =>
        {
            options.ImageWidth = 1_152;
            options.ImageHeight = 1_728;
        },
        static options =>
        {
            options.ImageWidth = 1_152;
            options.ImageHeight = 1_728;
            options.FontSize = 13f;
        },
        static options =>
        {
            options.ImageWidth = 1_280;
            options.ImageHeight = 1_920;
            options.FontSize = 12f;
            options.HorizontalPadding = 40;
            options.VerticalPadding = 40;
        },
    ];

    public static async Task<(IPromptRasterizer Rasterizer, string Text, double AverageCharactersPerPage)> CreateAsync()
    {
        var text = ProseGenerator.GenerateForThresholdTests(45_000);
        var probeRequest = new PromptRasterRequest
        {
            TreatAsProse = true,
            MaximumPages = 50,
        };

        foreach (var profile in RenderProfiles)
        {
            var rasterizer = RasterizerFactory.Create(profile);
            var probe = await rasterizer.RasterizeAsync(
                text,
                AiProvider.Anthropic,
                probeRequest);

            if (probe.Decision.Reason == PromptRasterDecisionReason.UnsuitableContent ||
                probe.PageCount == 0)
            {
                continue;
            }

            var density = probe.AverageCharactersPerPage;

            if (density < BandMinimum || density >= 8_000)
            {
                continue;
            }

            var realistic = await rasterizer.RasterizeAsync(
                text,
                AiProvider.Anthropic,
                new PromptRasterRequest { TreatAsProse = true });

            if (realistic.Decision.Reason == PromptRasterDecisionReason.MaximumPageCountExceeded)
            {
                continue;
            }

            return (rasterizer, text, density);
        }

        throw new InvalidOperationException(
            $"Could not find render settings that lay out between {BandMinimum:N0} and {BandMaximum:N0} " +
            "characters per page on this platform. Adjust ThresholdTestScenario render profiles.");
    }
}
