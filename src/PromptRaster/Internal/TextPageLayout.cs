namespace PromptRaster.Internal;

/// <summary>
/// A single visual line on a page. <see cref="Text"/> is what is drawn;
/// the source range additionally covers any line terminator characters that
/// follow it, so that concatenating all line ranges reconstructs the source.
/// </summary>
internal sealed record TextLine
{
    public required string Text { get; init; }

    public required int SourceStartIndex { get; init; }

    public required int SourceLength { get; init; }
}

/// <summary>
/// The laid-out content of a single page before any pixels are rendered.
/// </summary>
internal sealed record TextPageLayout
{
    public required int PageNumber { get; init; }

    public required int TotalPages { get; init; }

    public required IReadOnlyList<TextLine> Lines { get; init; }

    public required int SourceStartIndex { get; init; }

    public required int SourceLength { get; init; }
}

/// <summary>
/// The result of laying out the full source text into pages.
/// </summary>
internal sealed record TextPageLayoutResult
{
    public required IReadOnlyList<TextPageLayout> Pages { get; init; }
}
