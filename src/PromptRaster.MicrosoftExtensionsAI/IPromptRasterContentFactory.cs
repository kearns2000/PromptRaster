using Microsoft.Extensions.AI;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Builds <see cref="AIContent"/> items for an <see cref="IChatClient"/> message,
/// letting PromptRaster decide whether a document travels as text or as PNG pages.
/// Rasterisation is always explicit: only the supplied document text is ever
/// converted, never arbitrary user messages.
/// Prefer <see cref="PromptRasterChatClient"/> with <see cref="RasterTextContent"/>
/// when integrating through an <see cref="IChatClient"/> pipeline.
/// </summary>
public interface IPromptRasterContentFactory
{
    /// <summary>
    /// Creates content items consisting of <paramref name="instruction"/> as text,
    /// followed by the document either as text or as one PNG image per page.
    /// </summary>
    /// <param name="instruction">The instruction for the model. Always included as text.</param>
    /// <param name="text">The document text to evaluate for rasterisation.</param>
    /// <param name="provider">The AI provider that will receive the message.</param>
    /// <param name="request">Optional per-request PromptRaster overrides.</param>
    /// <param name="cancellationToken">A token to cancel layout and rendering.</param>
    ValueTask<IReadOnlyList<AIContent>> CreateAsync(
        string instruction,
        string text,
        AiProvider provider,
        PromptRasterRequest? request = null,
        CancellationToken cancellationToken = default);
}
