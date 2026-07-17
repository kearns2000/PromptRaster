using Microsoft.Extensions.AI;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Marks text as eligible for PromptRaster visual encoding when a
/// <see cref="PromptRasterChatClient"/> is present in the <see cref="IChatClient"/> pipeline.
/// Ordinary <see cref="TextContent"/> is never rasterised automatically.
/// </summary>
public sealed class RasterTextContent : AIContent
{
    /// <summary>
    /// Creates a new marker for <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text that may be rendered as PNG pages.</param>
    public RasterTextContent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>The source text eligible for rasterisation.</summary>
    public string Text { get; }
}
