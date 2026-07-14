namespace PromptRaster;

/// <summary>
/// The outcome of a rasterisation request. The original text is always
/// preserved so callers can fall back to it regardless of the decision.
/// </summary>
public sealed record PromptRasterResult
{
    /// <summary>Whether the content should be sent as text or as images.</summary>
    public required PromptRasterEncoding Encoding { get; init; }

    /// <summary>The decision that was made and why.</summary>
    public required PromptRasterDecision Decision { get; init; }

    /// <summary>The original, unmodified source text.</summary>
    public required string OriginalText { get; init; }

    /// <summary>The SHA-256 hash of the UTF-8 source text as uppercase hexadecimal, for traceability.</summary>
    public required string SourceSha256 { get; init; }

    /// <summary>The number of characters in the source text.</summary>
    public required int CharacterCount { get; init; }

    /// <summary>The number of pages that were (or would be) produced. Zero when no layout was performed.</summary>
    public required int PageCount { get; init; }

    /// <summary>The average number of source characters per laid-out page. Zero when no layout was performed.</summary>
    public required double AverageCharactersPerPage { get; init; }

    /// <summary>The minimum characters-per-page density required for rasterisation with the selected provider.</summary>
    public required int RequiredCharactersPerPage { get; init; }

    /// <summary>The rendered pages, in page-number order. Empty when <see cref="Encoding"/> is <see cref="PromptRasterEncoding.Text"/>.</summary>
    public required IReadOnlyList<PromptRasterPage> Pages { get; init; }
}
