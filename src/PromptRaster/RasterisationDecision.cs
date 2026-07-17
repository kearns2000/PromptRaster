namespace PromptRaster;

/// <summary>
/// The outcome of an <see cref="IRasterisationPolicy"/> evaluation.
/// </summary>
/// <param name="ShouldRasterise">
/// <see langword="true"/> when the candidate may proceed to PNG rendering;
/// otherwise the content should remain ordinary text.
/// </param>
/// <param name="Reason">The structured reason for the decision.</param>
/// <param name="Description">A human-readable explanation suitable for diagnostics.</param>
public sealed record RasterisationDecision(
    bool ShouldRasterise,
    PromptRasterDecisionReason Reason,
    string? Description = null);
