namespace PromptRaster;

/// <summary>
/// Conservative heuristic checks for content that should remain as exact text.
/// These detectors are extension points, not a complete security control.
/// </summary>
public interface IExactContentDetector
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/> appears to contain
    /// secrets, long hashes, GUID-heavy content, exact identifiers, paths, or dense
    /// numeric data that should not be rasterised automatically.
    /// </summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="reason">A short machine-readable category when a match is found.</param>
    bool LooksExactOrSensitive(string text, out string? reason);
}
