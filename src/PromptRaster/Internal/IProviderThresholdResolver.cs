namespace PromptRaster.Internal;

/// <summary>
/// Resolves the minimum characters-per-page density required before
/// rasterisation is considered worthwhile for a provider.
/// </summary>
internal interface IProviderThresholdResolver
{
    /// <summary>
    /// Returns the effective threshold, honouring the request-level override
    /// when present.
    /// </summary>
    int Resolve(AiProvider provider, PromptRasterRequest request);
}
