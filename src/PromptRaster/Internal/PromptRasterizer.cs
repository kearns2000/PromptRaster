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
/// from rendering: the text is first evaluated by policy, then laid out into
/// in-memory pages, and PNG data is only encoded after the decision to return
/// images has been made.
/// </summary>
internal sealed class PromptRasterizer(
    IRasterisationPolicy policy,
    ITextPageLayoutEngine layoutEngine,
    ITextImageRenderer renderer,
    IProviderThresholdResolver thresholdResolver,
    IModelProfileProvider modelProfileProvider,
    IPromptRasterCache cache,
    IOptions<PromptRasterOptions> options,
    ILogger<PromptRasterizer>? logger = null) : IPromptRasterizer
{
    private static readonly PromptRasterRequest DefaultRequest = new();

    private readonly ILogger _logger = logger ?? NullLogger<PromptRasterizer>.Instance;

    public async ValueTask<PromptRasterResult> RasterizeAsync(
        string text,
        AiProvider provider,
        PromptRasterRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        request ??= DefaultRequest;
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = PromptRasterActivity.StartRasterizeActivity();

        var result = await RasterizeAsyncCore(text, provider, request, cancellationToken).ConfigureAwait(false);

        PromptRasterizerLog.Decision(
            _logger,
            provider,
            result.Decision.Reason,
            result.CharacterCount,
            result.PageCount,
            result.AverageCharactersPerPage,
            result.RequiredCharactersPerPage);

        PromptRasterActivity.RecordDecision(
            activity,
            result.Decision.Reason,
            provider,
            result.CharacterCount,
            result.PageCount,
            fellBack: result.Decision.Reason == PromptRasterDecisionReason.RenderingFailed);

        return result;
    }

    private async ValueTask<PromptRasterResult> RasterizeAsyncCore(
        string text,
        AiProvider provider,
        PromptRasterRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var threshold = ResolveThreshold(provider, request);
        var profile = ResolveProfile(request.ModelId);

        if (request.ExcludeFromRasterisation)
        {
            return TextResult(
                text,
                threshold,
                PromptRasterDecisionReason.ExplicitlyExcluded,
                "The content was kept as text because it was explicitly excluded from rasterisation.");
        }

        var preLayoutCandidate = new RasterisationCandidate(
            text,
            provider,
            request.ModelId,
            request,
            RequiredCharactersPerPage: threshold);

        var preLayoutDecision = await policy.EvaluateAsync(preLayoutCandidate, cancellationToken)
            .ConfigureAwait(false);

        if (!preLayoutDecision.ShouldRasterise)
        {
            return TextResult(
                text,
                threshold,
                preLayoutDecision.Reason,
                preLayoutDecision.Description ?? preLayoutDecision.Reason.ToString());
        }

        if (request.Mode == PromptRasterMode.Always)
        {
            return await RasterizeForcedAsync(text, provider, request, threshold, profile, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var renderSettings = CreateRenderSettings(settings, request, profile);
            var layout = layoutEngine.Layout(text, renderSettings, cancellationToken);
            var pageCount = layout.Pages.Count;
            var averageDensity = pageCount > 0 ? text.Length / (double)pageCount : 0;

            var postLayoutCandidate = preLayoutCandidate with
            {
                PageCount = pageCount,
                AverageCharactersPerPage = averageDensity,
                RequiredCharactersPerPage = threshold,
            };

            var postLayoutDecision = await policy.EvaluateAsync(postLayoutCandidate, cancellationToken)
                .ConfigureAwait(false);

            if (!postLayoutDecision.ShouldRasterise)
            {
                return TextResult(
                    text,
                    threshold,
                    postLayoutDecision.Reason,
                    postLayoutDecision.Description ?? postLayoutDecision.Reason.ToString(),
                    layout);
            }

            var decision = new PromptRasterDecision
            {
                Reason = PromptRasterDecisionReason.Rasterised,
                Description = postLayoutDecision.Description ??
                    $"The content was rasterised into {Format(pageCount)} page(s).",
            };

            return await ImageResultAsync(text, provider, request, threshold, profile, decision, layout, renderSettings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (settings.FallbackToText)
        {
            PromptRasterActivity.RenderFailures.Add(1);
            return TextResult(
                text,
                threshold,
                PromptRasterDecisionReason.RenderingFailed,
                "The content was kept as text because layout or rendering failed and fallback to text is enabled.");
        }
    }

    private async ValueTask<PromptRasterResult> RasterizeForcedAsync(
        string text,
        AiProvider provider,
        PromptRasterRequest request,
        int threshold,
        ModelProfile? profile,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            var renderSettings = CreateRenderSettings(settings, request, profile);
            var layout = layoutEngine.Layout(text, renderSettings, cancellationToken);
            var pageCount = layout.Pages.Count;

            var forcedCandidate = new RasterisationCandidate(
                text,
                provider,
                request.ModelId,
                request,
                PageCount: pageCount,
                AverageCharactersPerPage: pageCount > 0 ? text.Length / (double)pageCount : 0,
                RequiredCharactersPerPage: threshold);

            var forcedDecision = await policy.EvaluateAsync(forcedCandidate, cancellationToken)
                .ConfigureAwait(false);

            if (!forcedDecision.ShouldRasterise)
            {
                return TextResult(
                    text,
                    threshold,
                    forcedDecision.Reason,
                    forcedDecision.Description ?? forcedDecision.Reason.ToString(),
                    layout);
            }

            var averageDensity = pageCount > 0 ? text.Length / (double)pageCount : 0;
            var decision = new PromptRasterDecision
            {
                Reason = PromptRasterDecisionReason.ForcedRasterisation,
                Description =
                    $"Rasterisation was forced by PromptRasterMode.Always: {Format(pageCount)} page(s) " +
                    $"at an average density of {Format(averageDensity)} characters per page.",
            };

            return await ImageResultAsync(text, provider, request, threshold, profile, decision, layout, renderSettings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (settings.FallbackToText)
        {
            PromptRasterActivity.RenderFailures.Add(1);
            return TextResult(
                text,
                threshold,
                PromptRasterDecisionReason.RenderingFailed,
                "The content was kept as text because layout or rendering failed and fallback to text is enabled.");
        }
    }

    private async ValueTask<PromptRasterResult> ImageResultAsync(
        string text,
        AiProvider provider,
        PromptRasterRequest request,
        int threshold,
        ModelProfile? profile,
        PromptRasterDecision decision,
        TextPageLayoutResult layout,
        PromptRasterRenderSettings renderSettings,
        CancellationToken cancellationToken)
    {
        var cacheKey = PromptRasterCacheKey.Create(text, renderSettings, profile);

        if (cache.TryGet(cacheKey, out var cachedPages) && cachedPages is { Count: > 0 })
        {
            var clonedCachedPages = ClonePages(cachedPages);

            return new PromptRasterResult
            {
                Encoding = PromptRasterEncoding.Images,
                Decision = decision,
                OriginalText = text,
                SourceSha256 = ComputeSha256(text),
                CharacterCount = text.Length,
                PageCount = clonedCachedPages.Count,
                AverageCharactersPerPage = text.Length / (double)clonedCachedPages.Count,
                RequiredCharactersPerPage = threshold,
                Pages = clonedCachedPages,
            };
        }

        try
        {
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
            PromptRasterActivity.RenderDurationMilliseconds.Record(stopwatch.Elapsed.TotalMilliseconds);
            PromptRasterizerLog.PagesRendered(_logger, pages.Count, totalBytes, stopwatch.ElapsedMilliseconds, provider);

            cache.Set(cacheKey, ClonePages(pages));

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (options.Value.FallbackToText)
        {
            PromptRasterActivity.RenderFailures.Add(1);
            return TextResult(
                text,
                threshold,
                PromptRasterDecisionReason.RenderingFailed,
                "The content was kept as text because layout or rendering failed and fallback to text is enabled.",
                layout);
        }
    }

    private static IReadOnlyList<PromptRasterPage> ClonePages(IReadOnlyList<PromptRasterPage> pages)
    {
        var clones = new List<PromptRasterPage>(pages.Count);

        foreach (var page in pages)
        {
            clones.Add(page with { Data = (byte[])page.Data.Clone() });
        }

        return clones;
    }

    private int ResolveThreshold(AiProvider provider, PromptRasterRequest request)
    {
        if (request.MinimumCharactersPerPage is int overrideThreshold)
        {
            return overrideThreshold;
        }

        if (!string.IsNullOrWhiteSpace(request.ModelId) &&
            modelProfileProvider.TryGetProfile(request.ModelId, out var profile) &&
            profile is not null)
        {
            return profile.MinimumCharactersPerPage;
        }

        return thresholdResolver.Resolve(provider, request);
    }

    private ModelProfile? ResolveProfile(string? modelId) =>
        !string.IsNullOrWhiteSpace(modelId) &&
        modelProfileProvider.TryGetProfile(modelId, out var profile)
            ? profile
            : null;

    private static PromptRasterRenderSettings CreateRenderSettings(
        PromptRasterOptions settings,
        PromptRasterRequest request,
        ModelProfile? profile)
    {
        if (profile is null)
        {
            return PromptRasterRenderSettings.Create(settings, request);
        }

        return new PromptRasterRenderSettings
        {
            ImageWidth = profile.ImageWidth > 0 ? profile.ImageWidth : settings.ImageWidth,
            ImageHeight = profile.ImageHeight > 0 ? profile.ImageHeight : settings.ImageHeight,
            HorizontalPadding = profile.HorizontalPadding >= 0 ? profile.HorizontalPadding : settings.HorizontalPadding,
            VerticalPadding = profile.VerticalPadding >= 0 ? profile.VerticalPadding : settings.VerticalPadding,
            FontSize = profile.FontSize > 0 ? profile.FontSize : settings.FontSize,
            LineSpacingMultiplier = settings.LineSpacingMultiplier,
            IncludePageHeader = request.IncludePageHeader,
        };
    }

    private static PromptRasterResult TextResult(
        string text,
        int threshold,
        PromptRasterDecisionReason reason,
        string description,
        TextPageLayoutResult? layout = null)
    {
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
}
