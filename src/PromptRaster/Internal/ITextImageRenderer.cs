namespace PromptRaster.Internal;

/// <summary>
/// Renders a laid-out page to encoded PNG bytes.
/// </summary>
internal interface ITextImageRenderer
{
    /// <summary>Renders <paramref name="page"/> as a PNG image.</summary>
    byte[] Render(
        TextPageLayout page,
        PromptRasterRenderSettings settings,
        CancellationToken cancellationToken);
}
