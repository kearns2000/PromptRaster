namespace PromptRaster;

/// <summary>
/// Controls how the rasterisation decision is made for a single request.
/// </summary>
public enum PromptRasterMode
{
    /// <summary>
    /// Decide automatically using content classification and the provider's
    /// character-density threshold. This is the default.
    /// </summary>
    Auto,

    /// <summary>
    /// Force rasterisation, skipping content suitability detection and the
    /// provider threshold. Empty input and the absolute maximum page count
    /// are still enforced.
    /// </summary>
    Always,

    /// <summary>Never rasterise; always return the original text.</summary>
    Never
}
