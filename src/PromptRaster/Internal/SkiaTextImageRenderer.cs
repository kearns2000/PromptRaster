using SkiaSharp;

namespace PromptRaster.Internal;

/// <summary>
/// Renders a laid-out page as a white-background, black-text, anti-aliased PNG.
/// The renderer is stateless and safe for concurrent use; every SkiaSharp
/// resource it creates is disposed before returning.
/// </summary>
internal sealed class SkiaTextImageRenderer : ITextImageRenderer
{
    public byte[] Render(
        TextPageLayout page,
        PromptRasterRenderSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        // Grayscale keeps the PNG payload small without affecting OCR accuracy:
        // the output is always black anti-aliased text on a white background.
        var info = new SKImageInfo(settings.ImageWidth, settings.ImageHeight, SKColorType.Gray8, SKAlphaType.Opaque);

        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create a SkiaSharp surface for rendering.");

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var bodyFont = SkiaTextMetrics.CreateBodyFont(settings);

        if (settings.IncludePageHeader)
        {
            using var headerFont = SkiaTextMetrics.CreateHeaderFont(settings);
            var headerBaseline = settings.VerticalPadding - headerFont.Metrics.Ascent;
            canvas.DrawText(
                $"Page {page.PageNumber} of {page.TotalPages}",
                settings.HorizontalPadding,
                headerBaseline,
                SKTextAlign.Left,
                headerFont,
                paint);
        }

        var lineHeight = SkiaTextMetrics.GetLineHeight(bodyFont, settings.LineSpacingMultiplier);
        var contentTop = SkiaTextMetrics.GetContentTop(settings);
        var baseline = contentTop - bodyFont.Metrics.Ascent;

        foreach (var line in page.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (line.Text.Length > 0)
            {
                canvas.DrawText(line.Text, settings.HorizontalPadding, baseline, SKTextAlign.Left, bodyFont, paint);
            }

            baseline += lineHeight;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to encode the rendered page as PNG.");

        return data.ToArray();
    }
}
