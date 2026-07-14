using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace PromptRaster.Internal;

/// <summary>
/// The default <see cref="IPromptRasterizer"/>. Decision-making is kept separate
/// from rendering: the text is first laid out into in-memory pages, and PNG data
/// is only encoded after the decision to return images has been made.
/// </summary>
internal sealed class PromptRasterizer(
    ITextContentClassifier classifier,
    ITextPageLayoutEngine layoutEngine,
    ITextImageRenderer renderer,
    IProviderThresholdResolver thresholdResolver,
    IOptions<PromptRasterOptions> options,
    ILogger<PromptRasterizer>? logger = null) : IPromptRasterizer
{
    private static readonly PromptRasterRequest DefaultRequest = new();

    // Logging is optional: hosts that have not called AddLogging still work.
    private readonly ILogger _logger = logger ?? NullLogger<PromptRasterizer>.Instance;

    public ValueTask<PromptRasterResult> RasterizeAsync(
        string text,
        AiProvider provider,
        PromptRasterRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        request ??= DefaultRequest;
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var result = Rasterize(text, provider, request, cancellationToken);

        PromptRasterizerLog.Decision(
            _logger,
            provider,
            result.Decision.Reason,
            result.CharacterCount,
            result.PageCount,
            result.AverageCharactersPerPage,
            result.RequiredCharactersPerPage);

        return new ValueTask<PromptRasterResult>(result);
    }

    private PromptRasterResult Rasterize(
        string text,
        AiProvider provider,
        PromptRasterRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var threshold = thresholdResolver.Resolve(provider, request);
        var context = new DecisionContext(text, provider, request, threshold);

        if (string.IsNullOrWhiteSpace(text))
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.EmptyInput,
                "The content was kept as text because the input is empty or whitespace-only.");
        }

        return request.Mode switch
        {
            PromptRasterMode.Never => TextResult(
                context,
                PromptRasterDecisionReason.RasterisationDisabled,
                "The content was kept as text because rasterisation was disabled by PromptRasterMode.Never."),
            PromptRasterMode.Always => RasterizeForced(context, settings, cancellationToken),
            _ => RasterizeAuto(context, settings, cancellationToken),
        };
    }

    private PromptRasterResult RasterizeAuto(
        DecisionContext context,
        PromptRasterOptions settings,
        CancellationToken cancellationToken)
    {
        var (text, provider, request, threshold) = context;

        if (provider == AiProvider.Unknown)
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.UnknownProvider,
                "The content was kept as text because the provider is Unknown; " +
                "automatic rasterisation requires a known provider.");
        }

        if (text.Length < settings.MinimumTextLength)
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.TextTooShort,
                $"The content was kept as text because it is {Format(text.Length)} characters long, " +
                $"below the configured minimum of {Format(settings.MinimumTextLength)}.");
        }

        if (settings.DetectStructuredContent && !request.TreatAsProse)
        {
            var classification = classifier.Classify(text);

            if (classification != TextContentClassification.Prose)
            {
                return TextResult(
                    context,
                    PromptRasterDecisionReason.UnsuitableContent,
                    $"The content was kept as text because it appears to be {classification} content, " +
                    "which is treated as accuracy-sensitive and unsuitable for automatic visual encoding.");
            }
        }

        var layout = layoutEngine.Layout(text, PromptRasterRenderSettings.Create(settings, request), cancellationToken);
        var pageCount = layout.Pages.Count;
        var maximumPages = Math.Min(request.MaximumPages ?? settings.MaximumPages, settings.AbsoluteMaximumPages);

        if (pageCount > maximumPages)
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.MaximumPageCountExceeded,
                $"The content was kept as text because it would require {Format(pageCount)} pages, " +
                $"exceeding the maximum of {Format(maximumPages)}.",
                layout);
        }

        var averageDensity = text.Length / (double)pageCount;

        if (averageDensity < threshold)
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.InsufficientCharacterDensity,
                $"The content was kept as text because its average density was " +
                $"{Format(averageDensity)} characters per page, below the {provider} threshold " +
                $"of {Format(threshold)}.",
                layout);
        }

        var decision = new PromptRasterDecision
        {
            Reason = PromptRasterDecisionReason.Rasterised,
            Description =
                $"The content was rasterised into {Format(pageCount)} page(s) because its average density " +
                $"was {Format(averageDensity)} characters per page, at or above the {provider} threshold " +
                $"of {Format(threshold)}.",
        };

        return ImageResult(context, decision, layout, cancellationToken);
    }

    private PromptRasterResult RasterizeForced(
        DecisionContext context,
        PromptRasterOptions settings,
        CancellationToken cancellationToken)
    {
        var (text, _, request, _) = context;

        var layout = layoutEngine.Layout(text, PromptRasterRenderSettings.Create(settings, request), cancellationToken);
        var pageCount = layout.Pages.Count;

        if (pageCount > settings.AbsoluteMaximumPages)
        {
            return TextResult(
                context,
                PromptRasterDecisionReason.MaximumPageCountExceeded,
                $"The content was kept as text because it would require {Format(pageCount)} pages, " +
                $"exceeding the absolute maximum of {Format(settings.AbsoluteMaximumPages)} " +
                "that even forced rasterisation must respect.",
                layout);
        }

        var averageDensity = text.Length / (double)pageCount;

        var decision = new PromptRasterDecision
        {
            Reason = PromptRasterDecisionReason.ForcedRasterisation,
            Description =
                $"Rasterisation was forced by PromptRasterMode.Always: {Format(pageCount)} page(s) " +
                $"at an average density of {Format(averageDensity)} characters per page.",
        };

        return ImageResult(context, decision, layout, cancellationToken);
    }

    private PromptRasterResult ImageResult(
        DecisionContext context,
        PromptRasterDecision decision,
        TextPageLayoutResult layout,
        CancellationToken cancellationToken)
    {
        var (text, provider, request, threshold) = context;
        var renderSettings = PromptRasterRenderSettings.Create(options.Value, request);

        var stopwatch = Stopwatch.StartNew();
        var pages = new List<PromptRasterPage>(layout.Pages.Count);
        long totalBytes = 0;

        foreach (var pageLayout in layout.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var data = renderer.Render(pageLayout, renderSettings, cancellationToken);
            totalBytes += data.Length;

            pages.Add(new PromptRasterPage
            {
                PageNumber = pageLayout.PageNumber,
                TotalPages = pageLayout.TotalPages,
                Data = data,
                MediaType = "image/png",
                SourceStartIndex = pageLayout.SourceStartIndex,
                SourceLength = pageLayout.SourceLength,
                CharacterCount = pageLayout.SourceLength,
            });
        }

        stopwatch.Stop();
        PromptRasterizerLog.PagesRendered(_logger, pages.Count, totalBytes, stopwatch.ElapsedMilliseconds, provider);

        return new PromptRasterResult
        {
            Encoding = PromptRasterEncoding.Images,
            Decision = decision,
            OriginalText = text,
            SourceSha256 = ComputeSha256(text),
            CharacterCount = text.Length,
            PageCount = pages.Count,
            AverageCharactersPerPage = pages.Count > 0 ? text.Length / (double)pages.Count : 0,
            RequiredCharactersPerPage = threshold,
            Pages = pages,
        };
    }

    private static PromptRasterResult TextResult(
        DecisionContext context,
        PromptRasterDecisionReason reason,
        string description,
        TextPageLayoutResult? layout = null)
    {
        var (text, _, _, threshold) = context;
        var pageCount = layout?.Pages.Count ?? 0;

        return new PromptRasterResult
        {
            Encoding = PromptRasterEncoding.Text,
            Decision = new PromptRasterDecision { Reason = reason, Description = description },
            OriginalText = text,
            SourceSha256 = ComputeSha256(text),
            CharacterCount = text.Length,
            PageCount = pageCount,
            AverageCharactersPerPage = pageCount > 0 ? text.Length / (double)pageCount : 0,
            RequiredCharactersPerPage = threshold,
            Pages = [],
        };
    }

    private static void ValidateRequest(PromptRasterRequest request)
    {
        if (request.MaximumPages is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaximumPages,
                $"{nameof(request.MaximumPages)} must be at least 1 when specified.");
        }

        if (request.MinimumCharactersPerPage is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MinimumCharactersPerPage,
                $"{nameof(request.MinimumCharactersPerPage)} must be at least 1 when specified.");
        }
    }

    private static string ComputeSha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private sealed record DecisionContext(
        string Text,
        AiProvider Provider,
        PromptRasterRequest Request,
        int Threshold);
}
