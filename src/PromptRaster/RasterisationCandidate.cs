namespace PromptRaster;

/// <summary>
/// A unit of text considered for rasterisation, together with optional layout
/// measurements and model identity.
/// </summary>
/// <param name="Text">The source text. Never logged by default PromptRaster components.</param>
/// <param name="Provider">The AI provider family that will receive the content.</param>
/// <param name="ModelId">
/// An optional provider-specific model identifier used for model-profile lookup.
/// When supplied and no profile is known, automatic rasterisation is rejected.
/// </param>
/// <param name="Request">Optional per-request overrides.</param>
/// <param name="PageCount">The measured page count after layout, when available.</param>
/// <param name="AverageCharactersPerPage">Measured average density after layout, when available.</param>
/// <param name="RequiredCharactersPerPage">The density threshold in force for this candidate.</param>
public sealed record RasterisationCandidate(
    string Text,
    AiProvider Provider,
    string? ModelId = null,
    PromptRasterRequest? Request = null,
    int? PageCount = null,
    double? AverageCharactersPerPage = null,
    int? RequiredCharactersPerPage = null)
{
    /// <summary>The character length of <see cref="Text"/>.</summary>
    public int CharacterCount => Text.Length;
}
