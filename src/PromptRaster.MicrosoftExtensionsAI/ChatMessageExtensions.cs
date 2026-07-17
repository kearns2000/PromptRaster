using Microsoft.Extensions.AI;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Convenience helpers for marking chat content as rasterisation-eligible.
/// </summary>
public static class ChatMessageExtensions
{
    /// <summary>
    /// Appends a <see cref="RasterTextContent"/> item to <paramref name="message"/>.
    /// </summary>
    public static ChatMessage AddRasterText(this ChatMessage message, string text)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(text);

        message.Contents.Add(new RasterTextContent(text));
        return message;
    }
}
