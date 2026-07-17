namespace PromptRaster;

/// <summary>
/// Evaluates whether a text candidate is eligible for rasterisation.
/// Applications may supply their own policy or compose the default with
/// additional classifiers. Heuristic detectors are not a complete security control.
/// </summary>
public interface IRasterisationPolicy
{
    /// <summary>
    /// Evaluates <paramref name="candidate"/> and returns a decision that explains
    /// whether rasterisation should proceed and why.
    /// </summary>
    /// <param name="candidate">The text and context under consideration.</param>
    /// <param name="stopToken">A token to cancel the evaluation.</param>
    ValueTask<RasterisationDecision> EvaluateAsync(
        RasterisationCandidate candidate,
        CancellationToken stopToken = default);
}
