namespace PromptRaster;

/// <summary>
/// Optional cache for rendered PNG pages. Callers that cache sensitive content must
/// supply an implementation and retention policy appropriate to that content.
/// PromptRaster does not cache secrets by default.
/// </summary>
public interface IPromptRasterCache
{
    /// <summary>
    /// Attempts to retrieve previously rendered pages for <paramref name="cacheKey"/>.
    /// </summary>
    bool TryGet(string cacheKey, out IReadOnlyList<PromptRasterPage>? pages);

    /// <summary>
    /// Stores rendered pages under <paramref name="cacheKey"/>.
    /// </summary>
    void Set(string cacheKey, IReadOnlyList<PromptRasterPage> pages);
}
