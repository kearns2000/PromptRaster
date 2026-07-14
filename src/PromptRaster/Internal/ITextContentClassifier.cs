namespace PromptRaster.Internal;

/// <summary>
/// Classifies text into coarse content categories using cheap, deterministic
/// heuristics. Never throws for malformed input.
/// </summary>
internal interface ITextContentClassifier
{
    /// <summary>Classifies <paramref name="text"/>.</summary>
    TextContentClassification Classify(string text);
}
