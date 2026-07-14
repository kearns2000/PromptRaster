using SkiaSharp;

namespace PromptRaster.Internal;

/// <summary>
/// Shared font construction and vertical metrics used by both the layout
/// engine and the renderer, so that pagination and drawing always agree.
/// </summary>
internal static class SkiaTextMetrics
{
    private const float HeaderFontScale = 0.75f;
    private const float HeaderGapScale = 0.5f;

    /// <summary>Creates the body font. The caller owns and must dispose it.</summary>
    public static SKFont CreateBodyFont(PromptRasterRenderSettings settings) => new(SKTypeface.Default, settings.FontSize)
    {
        Subpixel = true,
        Edging = SKFontEdging.Antialias,
    };

    /// <summary>Creates the page-header font. The caller owns and must dispose it.</summary>
    public static SKFont CreateHeaderFont(PromptRasterRenderSettings settings) => new(SKTypeface.Default, settings.FontSize * HeaderFontScale)
    {
        Subpixel = true,
        Edging = SKFontEdging.Antialias,
    };

    /// <summary>The vertical distance between consecutive baselines.</summary>
    public static float GetLineHeight(SKFont font, float multiplier)
    {
        var metrics = font.Metrics;
        return (metrics.Descent - metrics.Ascent + metrics.Leading) * multiplier;
    }

    /// <summary>
    /// The y coordinate where body content starts, accounting for the page
    /// header when enabled.
    /// </summary>
    public static float GetContentTop(PromptRasterRenderSettings settings)
    {
        if (!settings.IncludePageHeader)
        {
            return settings.VerticalPadding;
        }

        using var headerFont = CreateHeaderFont(settings);
        var headerHeight = GetLineHeight(headerFont, 1f);
        return settings.VerticalPadding + headerHeight + (headerHeight * HeaderGapScale);
    }

    /// <summary>The number of body lines that fit on a single page. Always at least 1.</summary>
    public static int GetLinesPerPage(PromptRasterRenderSettings settings, SKFont bodyFont)
    {
        var contentTop = GetContentTop(settings);
        var contentHeight = settings.ImageHeight - contentTop - settings.VerticalPadding;
        var lineHeight = GetLineHeight(bodyFont, settings.LineSpacingMultiplier);
        return Math.Max(1, (int)(contentHeight / lineHeight));
    }
}
