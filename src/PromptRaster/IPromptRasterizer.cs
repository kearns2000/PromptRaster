namespace PromptRaster;

/// <summary>
/// Decides whether long plain text is likely to cost fewer AI input tokens
/// as one or more dense PNG pages, and renders those pages when it is.
/// </summary>
public interface IPromptRasterizer
{
    /// <summary>
    /// Evaluates <paramref name="text"/> for the given <paramref name="provider"/> and
    /// returns either the original text or rendered PNG pages, together with the
    /// decision that was made and why.
    /// </summary>
    /// <param name="text">The source text. It is never modified or truncated.</param>
    /// <param name="provider">The AI provider that will receive the content.</param>
    /// <param name="request">Optional per-request overrides.</param>
    /// <param name="cancellationToken">A token to cancel layout and rendering.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    ValueTask<PromptRasterResult> RasterizeAsync(
        string text,
        AiProvider provider,
        PromptRasterRequest? request = null,
        CancellationToken cancellationToken = default);
}
