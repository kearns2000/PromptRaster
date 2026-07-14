using Microsoft.Extensions.Logging;

namespace PromptRaster.Internal;

/// <summary>
/// Structured log messages for the rasteriser. Only metadata is logged;
/// source text and image bytes are never written to the log.
/// </summary>
internal static partial class PromptRasterizerLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "PromptRasterDecision",
        Level = LogLevel.Debug,
        Message = "PromptRaster decision for {Provider}: {Reason}. Characters: {CharacterCount}, pages: {PageCount}, average characters per page: {AverageCharactersPerPage:F0}, required: {RequiredCharactersPerPage}.")]
    public static partial void Decision(
        ILogger logger,
        AiProvider provider,
        PromptRasterDecisionReason reason,
        int characterCount,
        int pageCount,
        double averageCharactersPerPage,
        int requiredCharactersPerPage);

    [LoggerMessage(
        EventId = 1001,
        EventName = "PromptRasterPagesRendered",
        Level = LogLevel.Information,
        Message = "PromptRaster rendered {PageCount} PNG page(s) totalling {TotalBytes} bytes in {ElapsedMilliseconds} ms for {Provider}.")]
    public static partial void PagesRendered(
        ILogger logger,
        int pageCount,
        long totalBytes,
        long elapsedMilliseconds,
        AiProvider provider);
}
