namespace PromptRaster;

/// <summary>
/// A single rendered PNG page together with the exact range of source
/// characters it represents.
/// </summary>
public sealed record PromptRasterPage
{
    /// <summary>The one-based page number.</summary>
    public required int PageNumber { get; init; }

    /// <summary>The total number of pages produced for the source text.</summary>
    public required int TotalPages { get; init; }

    /// <summary>The encoded PNG bytes.</summary>
    public required byte[] Data { get; init; }

    /// <summary>The IANA media type of <see cref="Data"/> (always <c>image/png</c>).</summary>
    public required string MediaType { get; init; }

    /// <summary>The index into the original text where this page's content starts.</summary>
    public required int SourceStartIndex { get; init; }

    /// <summary>The number of source characters represented by this page.</summary>
    public required int SourceLength { get; init; }

    /// <summary>The number of source characters represented by this page (equal to <see cref="SourceLength"/>).</summary>
    public required int CharacterCount { get; init; }
}
