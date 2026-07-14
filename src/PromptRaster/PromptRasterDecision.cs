namespace PromptRaster;

/// <summary>
/// Explains why content was rasterised or kept as text.
/// </summary>
public sealed record PromptRasterDecision
{
    /// <summary>The machine-readable reason for the decision.</summary>
    public required PromptRasterDecisionReason Reason { get; init; }

    /// <summary>A human-readable description including the relevant measured values.</summary>
    public required string Description { get; init; }
}
