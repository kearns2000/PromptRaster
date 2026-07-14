namespace PromptRaster.Internal;

/// <summary>
/// Lays text out into fixed-size pages without rendering any pixels.
/// </summary>
internal interface ITextPageLayoutEngine
{
    /// <summary>
    /// Wraps and paginates <paramref name="text"/> using measured pixel widths.
    /// The concatenated source ranges of the returned pages reconstruct the
    /// input exactly.
    /// </summary>
    TextPageLayoutResult Layout(
        string text,
        PromptRasterRenderSettings settings,
        CancellationToken cancellationToken);
}
