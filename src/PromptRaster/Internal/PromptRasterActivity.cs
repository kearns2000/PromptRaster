using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PromptRaster.Internal;

/// <summary>
/// OpenTelemetry-compatible activity and metric instrumentation.
/// Tags never include source text or image bytes.
/// </summary>
internal static class PromptRasterActivity
{
    public const string SourceName = "PromptRaster";

    public static ActivitySource Source { get; } = new(SourceName, "0.1.0");

    public static Meter Meter { get; } = new(SourceName, "0.1.0");

    public static Counter<long> Decisions { get; } =
        Meter.CreateCounter<long>("promptraster.decisions", description: "Rasterisation decisions by reason.");

    public static Counter<long> Fallbacks { get; } =
        Meter.CreateCounter<long>(
            "promptraster.fallbacks",
            description: "Count of layout/render failures that fell back to ordinary text.");

    public static Counter<long> RenderFailures { get; } =
        Meter.CreateCounter<long>("promptraster.render_failures", description: "Count of rendering failures.");

    public static Histogram<double> RenderDurationMilliseconds { get; } =
        Meter.CreateHistogram<double>("promptraster.render.duration", unit: "ms", description: "PNG rendering duration.");

    public static Activity? StartRasterizeActivity() =>
        Source.StartActivity("PromptRaster.Rasterize", ActivityKind.Internal);

    public static void RecordDecision(
        Activity? activity,
        PromptRasterDecisionReason reason,
        AiProvider provider,
        int characterCount,
        int pageCount,
        bool fellBack)
    {
        var tags = new TagList
        {
            { "reason", reason.ToString() },
            { "provider", provider.ToString() },
        };

        Decisions.Add(1, tags);

        if (fellBack)
        {
            Fallbacks.Add(1, tags);
        }

        var encoding = reason is PromptRasterDecisionReason.Rasterised
            or PromptRasterDecisionReason.ForcedRasterisation
            ? "images"
            : "text";

        activity?.SetTag("promptraster.reason", reason.ToString());
        activity?.SetTag("promptraster.provider", provider.ToString());
        activity?.SetTag("promptraster.character_count", characterCount);
        activity?.SetTag("promptraster.page_count", pageCount);
        activity?.SetTag("promptraster.fallback", fellBack);
        activity?.SetTag("promptraster.encoding", encoding);
    }
}
