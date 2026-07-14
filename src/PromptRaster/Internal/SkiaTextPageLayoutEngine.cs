using SkiaSharp;

namespace PromptRaster.Internal;

/// <summary>
/// Wraps text by measured pixel width using SkiaSharp font metrics and
/// paginates the resulting lines. No pixels are rendered here.
/// </summary>
/// <remarks>
/// Invariants:
/// <list type="bullet">
/// <item>every source character belongs to exactly one line's source range;</item>
/// <item>concatenating all page ranges in order reconstructs the input exactly;</item>
/// <item>words are only split when a single token is wider than a whole line.</item>
/// </list>
/// The engine is stateless and safe for concurrent use.
/// </remarks>
internal sealed class SkiaTextPageLayoutEngine : ITextPageLayoutEngine
{
    public TextPageLayoutResult Layout(
        string text,
        PromptRasterRenderSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        using var font = SkiaTextMetrics.CreateBodyFont(settings);

        var maxLineWidth = settings.ImageWidth - (2f * settings.HorizontalPadding);
        var lines = WrapText(text, font, maxLineWidth, cancellationToken);
        var linesPerPage = SkiaTextMetrics.GetLinesPerPage(settings, font);

        return Paginate(lines, linesPerPage);
    }

    private static List<TextLine> WrapText(
        string text,
        SKFont font,
        float maxLineWidth,
        CancellationToken cancellationToken)
    {
        var lines = new List<TextLine>();
        var position = 0;

        while (position < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (contentLength, terminatorLength) = FindSourceLine(text, position);

            if (contentLength == 0)
            {
                // Blank line: preserves paragraph breaks. The terminator (or the
                // trailing empty segment) is still accounted for in the range.
                lines.Add(new TextLine
                {
                    Text = string.Empty,
                    SourceStartIndex = position,
                    SourceLength = terminatorLength,
                });
                position += terminatorLength;
                continue;
            }

            var remainingStart = position;
            var remainingLength = contentLength;

            while (remainingLength > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = text.AsSpan(remainingStart, remainingLength);
                var fit = font.BreakText(remaining, maxLineWidth);

                if (fit <= 0)
                {
                    // Guarantee forward progress even in degenerate geometry.
                    fit = 1;
                }

                if (fit < remaining.Length)
                {
                    fit = AdjustToWordBoundary(remaining, fit);
                }

                var isLastSegment = fit == remainingLength;

                lines.Add(new TextLine
                {
                    Text = text.Substring(remainingStart, fit),
                    SourceStartIndex = remainingStart,
                    SourceLength = isLastSegment ? fit + terminatorLength : fit,
                });

                remainingStart += fit;
                remainingLength -= fit;
            }

            position += contentLength + terminatorLength;
        }

        return lines;
    }

    /// <summary>
    /// Returns the length of the line content starting at <paramref name="start"/>
    /// and the length of its terminator (0 at end of input, 1 for \n or \r, 2 for \r\n).
    /// </summary>
    private static (int ContentLength, int TerminatorLength) FindSourceLine(string text, int start)
    {
        var index = start;

        while (index < text.Length && text[index] != '\n' && text[index] != '\r')
        {
            index++;
        }

        var contentLength = index - start;

        if (index >= text.Length)
        {
            return (contentLength, 0);
        }

        var terminatorLength = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
        return (contentLength, terminatorLength);
    }

    /// <summary>
    /// Moves a mid-word break point back to the last whitespace character so
    /// words are not split. Falls back to the measured break for tokens wider
    /// than a whole line.
    /// </summary>
    private static int AdjustToWordBoundary(ReadOnlySpan<char> remaining, int fit)
    {
        // If the break already falls on whitespace, keep it: consume any run of
        // whitespace so the next line starts with a visible character.
        if (char.IsWhiteSpace(remaining[fit]) || char.IsWhiteSpace(remaining[fit - 1]))
        {
            var end = fit;
            while (end < remaining.Length && remaining[end] != '\n' && remaining[end] != '\r' && char.IsWhiteSpace(remaining[end]))
            {
                end++;
            }

            return end;
        }

        for (var i = fit - 1; i > 0; i--)
        {
            if (char.IsWhiteSpace(remaining[i]))
            {
                return i + 1;
            }
        }

        // A single unbroken token wider than the line: hard split (unavoidable).
        return fit;
    }

    private static TextPageLayoutResult Paginate(List<TextLine> lines, int linesPerPage)
    {
        if (lines.Count == 0)
        {
            return new TextPageLayoutResult { Pages = [] };
        }

        var totalPages = (lines.Count + linesPerPage - 1) / linesPerPage;
        var pages = new List<TextPageLayout>(totalPages);

        for (var pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var pageLines = lines
                .Skip(pageIndex * linesPerPage)
                .Take(linesPerPage)
                .ToArray();

            var start = pageLines[0].SourceStartIndex;
            var length = pageLines.Sum(static l => l.SourceLength);

            pages.Add(new TextPageLayout
            {
                PageNumber = pageIndex + 1,
                TotalPages = totalPages,
                Lines = pageLines,
                SourceStartIndex = start,
                SourceLength = length,
            });
        }

        return new TextPageLayoutResult { Pages = pages };
    }
}
