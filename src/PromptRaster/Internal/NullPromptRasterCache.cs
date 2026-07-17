namespace PromptRaster.Internal;

/// <summary>A no-op cache used when no application cache is registered.</summary>
internal sealed class NullPromptRasterCache : IPromptRasterCache
{
    public static NullPromptRasterCache Instance { get; } = new();

    public bool TryGet(string cacheKey, out IReadOnlyList<PromptRasterPage>? pages)
    {
        pages = null;
        return false;
    }

    public void Set(string cacheKey, IReadOnlyList<PromptRasterPage> pages)
    {
    }
}
