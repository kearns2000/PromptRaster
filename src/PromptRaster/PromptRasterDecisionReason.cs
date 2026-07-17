namespace PromptRaster;

/// <summary>
/// The reason behind a rasterisation decision.
/// </summary>
public enum PromptRasterDecisionReason
{
    /// <summary>The content was rasterised because its character density met the provider threshold.</summary>
    Rasterised,

    /// <summary>The content was rasterised because <see cref="PromptRasterMode.Always"/> was requested.</summary>
    ForcedRasterisation,

    /// <summary>The content was kept as text because <see cref="PromptRasterMode.Never"/> was requested.</summary>
    RasterisationDisabled,

    /// <summary>The content was kept as text because the provider is <see cref="AiProvider.Unknown"/>.</summary>
    UnknownProvider,

    /// <summary>
    /// The content was kept as text because the model is unknown, disabled, or has no registered profile.
    /// </summary>
    UnsupportedModel,

    /// <summary>The content was kept as text because it is shorter than the configured minimum length.</summary>
    TextTooShort,

    /// <summary>The content was kept as text because its average character density per page is below the provider threshold.</summary>
    InsufficientCharacterDensity,

    /// <summary>The content was kept as text because it appears structured or accuracy-sensitive.</summary>
    UnsuitableContent,

    /// <summary>
    /// The content was kept as text because exact or sensitive material was detected
    /// (secrets, hashes, identifiers, paths, or dense numeric data).
    /// </summary>
    ExactContentDetected,

    /// <summary>The content was kept as text because the caller explicitly excluded it from rasterisation.</summary>
    ExplicitlyExcluded,

    /// <summary>The content was kept as text because the required page count exceeds the configured maximum.</summary>
    MaximumPageCountExceeded,

    /// <summary>The content was kept as text because the input is empty or whitespace-only.</summary>
    EmptyInput,

    /// <summary>The content was kept as text because rendering failed and fallback to text was enabled.</summary>
    RenderingFailed,
}
