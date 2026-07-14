namespace PromptRaster.Internal;

/// <summary>
/// The effective render settings for one rasterisation call, combining
/// <see cref="PromptRasterOptions"/> with per-request overrides.
/// </summary>
internal sealed record PromptRasterRenderSettings
{
    public required int ImageWidth { get; init; }

    public required int ImageHeight { get; init; }

    public required int HorizontalPadding { get; init; }

    public required int VerticalPadding { get; init; }

    public required float FontSize { get; init; }

    public required float LineSpacingMultiplier { get; init; }

    public required bool IncludePageHeader { get; init; }

    public static PromptRasterRenderSettings Create(PromptRasterOptions options, PromptRasterRequest request) => new()
    {
        ImageWidth = options.ImageWidth,
        ImageHeight = options.ImageHeight,
        HorizontalPadding = options.HorizontalPadding,
        VerticalPadding = options.VerticalPadding,
        FontSize = options.FontSize,
        LineSpacingMultiplier = options.LineSpacingMultiplier,
        IncludePageHeader = request.IncludePageHeader,
    };
}
