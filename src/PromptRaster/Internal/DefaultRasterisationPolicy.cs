using System.Globalization;
using Microsoft.Extensions.Options;

namespace PromptRaster.Internal;

/// <summary>
/// The default policy used by <see cref="PromptRasterizer"/>. It applies length,
/// model-profile, structured-content, exact-content, page-limit and density checks.
/// </summary>
internal sealed class DefaultRasterisationPolicy(
    ITextContentClassifier classifier,
    IExactContentDetector exactContentDetector,
    IModelProfileProvider modelProfileProvider,
    IOptions<PromptRasterOptions> options) : IRasterisationPolicy
{
    public ValueTask<RasterisationDecision> EvaluateAsync(
        RasterisationCandidate candidate,
        CancellationToken stopToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidate.Text);
        stopToken.ThrowIfCancellationRequested();

        var settings = options.Value;
        var request = candidate.Request ?? new PromptRasterRequest();

        if (string.IsNullOrWhiteSpace(candidate.Text))
        {
            return Decision(
                false,
                PromptRasterDecisionReason.EmptyInput,
                "The content was kept as text because the input is empty or whitespace-only.");
        }

        if (request.ExcludeFromRasterisation)
        {
            return Decision(
                false,
                PromptRasterDecisionReason.ExplicitlyExcluded,
                "The content was kept as text because it was explicitly excluded from rasterisation.");
        }

        if (request.Mode == PromptRasterMode.Never)
        {
            return Decision(
                false,
                PromptRasterDecisionReason.RasterisationDisabled,
                "The content was kept as text because rasterisation was disabled by PromptRasterMode.Never.");
        }

        if (request.Mode == PromptRasterMode.Always)
        {
            if (candidate.PageCount is int forcedPages && forcedPages > settings.AbsoluteMaximumPages)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.MaximumPageCountExceeded,
                    $"The content was kept as text because it would require {Format(forcedPages)} pages, " +
                    $"exceeding the absolute maximum of {Format(settings.AbsoluteMaximumPages)} " +
                    "that even forced rasterisation must respect.");
            }

            return Decision(
                true,
                PromptRasterDecisionReason.ForcedRasterisation,
                "Rasterisation was forced by PromptRasterMode.Always.");
        }

        if (candidate.Provider == AiProvider.Unknown)
        {
            return Decision(
                false,
                PromptRasterDecisionReason.UnknownProvider,
                "The content was kept as text because the provider is Unknown; " +
                "automatic rasterisation requires a known provider.");
        }

        ModelProfile? profile = null;

        if (!string.IsNullOrWhiteSpace(candidate.ModelId))
        {
            if (!modelProfileProvider.TryGetProfile(candidate.ModelId, out profile) || profile is null)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.UnsupportedModel,
                    $"The content was kept as text because no model profile is registered for '{candidate.ModelId}'.");
            }

            if (!profile.Enabled)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.UnsupportedModel,
                    $"The content was kept as text because the model profile for '{candidate.ModelId}' is disabled.");
            }

            if (profile.Provider != AiProvider.Unknown && profile.Provider != candidate.Provider)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.UnsupportedModel,
                    $"The content was kept as text because the model profile for '{candidate.ModelId}' " +
                    $"belongs to {profile.Provider}, not {candidate.Provider}.");
            }
        }

        if (candidate.CharacterCount < settings.MinimumTextLength)
        {
            return Decision(
                false,
                PromptRasterDecisionReason.TextTooShort,
                $"The content was kept as text because it is {Format(candidate.CharacterCount)} characters long, " +
                $"below the configured minimum of {Format(settings.MinimumTextLength)}.");
        }

        if (settings.DetectExactContent &&
            !request.TreatAsProse &&
            exactContentDetector.LooksExactOrSensitive(candidate.Text, out var exactReason))
        {
            return Decision(
                false,
                PromptRasterDecisionReason.ExactContentDetected,
                $"The content was kept as text because exact or sensitive content was detected ({exactReason}). " +
                "Heuristic detection is not a complete security control.");
        }

        if (settings.DetectStructuredContent && !request.TreatAsProse)
        {
            var classification = classifier.Classify(candidate.Text);

            if (classification != TextContentClassification.Prose)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.UnsuitableContent,
                    $"The content was kept as text because it appears to be {classification} content, " +
                    "which is treated as accuracy-sensitive and unsuitable for automatic visual encoding.");
            }
        }

        if (candidate.PageCount is int pageCount)
        {
            var maximumPages = ResolveMaximumPages(settings, request, profile);

            if (pageCount > maximumPages)
            {
                return Decision(
                    false,
                    PromptRasterDecisionReason.MaximumPageCountExceeded,
                    $"The content was kept as text because it would require {Format(pageCount)} pages, " +
                    $"exceeding the maximum of {Format(maximumPages)}.");
            }
        }

        if (candidate.AverageCharactersPerPage is double average &&
            candidate.RequiredCharactersPerPage is int required &&
            average < required)
        {
            return Decision(
                false,
                PromptRasterDecisionReason.InsufficientCharacterDensity,
                $"The content was kept as text because its average density was " +
                $"{Format(average)} characters per page, below the {candidate.Provider} threshold " +
                $"of {Format(required)}.");
        }

        if (candidate.PageCount is int approvedPages &&
            candidate.AverageCharactersPerPage is double approvedAverage &&
            candidate.RequiredCharactersPerPage is int approvedRequired)
        {
            return Decision(
                true,
                PromptRasterDecisionReason.Rasterised,
                $"The content was rasterised into {Format(approvedPages)} page(s) because its average density " +
                $"was {Format(approvedAverage)} characters per page, at or above the {candidate.Provider} threshold " +
                $"of {Format(approvedRequired)}.");
        }

        // Pre-layout approval: layout and density still need to be measured.
        return Decision(
            true,
            PromptRasterDecisionReason.Rasterised,
            "The content passed pre-layout policy checks and may proceed to layout.");
    }

    private static int ResolveMaximumPages(
        PromptRasterOptions settings,
        PromptRasterRequest request,
        ModelProfile? profile)
    {
        var maximumPages = Math.Min(request.MaximumPages ?? settings.MaximumPages, settings.AbsoluteMaximumPages);

        if (profile is not null && profile.MaximumPages > 0)
        {
            maximumPages = Math.Min(maximumPages, profile.MaximumPages);
        }

        return maximumPages;
    }

    private static ValueTask<RasterisationDecision> Decision(
        bool shouldRasterise,
        PromptRasterDecisionReason reason,
        string description) =>
        new(new RasterisationDecision(shouldRasterise, reason, description));

    private static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
