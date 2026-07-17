using Microsoft.Extensions.Logging;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Structured log messages for the chat-client middleware. Source text and image
/// bytes are never written.
/// </summary>
internal static partial class PromptRasterChatClientLog
{
    [LoggerMessage(
        EventId = 2000,
        EventName = "PromptRasterChatClientRasterised",
        Level = LogLevel.Debug,
        Message = "PromptRaster chat middleware rasterised marked content. Reason: {Reason}. Characters: {CharacterCount}, pages: {PageCount}.")]
    public static partial void Rasterised(
        ILogger logger,
        PromptRasterDecisionReason reason,
        int characterCount,
        int pageCount);

    [LoggerMessage(
        EventId = 2001,
        EventName = "PromptRasterChatClientFallback",
        Level = LogLevel.Information,
        Message = "PromptRaster chat middleware fell back to text. Reason: {Reason}. Characters: {CharacterCount}.")]
    public static partial void Fallback(
        ILogger logger,
        PromptRasterDecisionReason reason,
        int characterCount);
}
