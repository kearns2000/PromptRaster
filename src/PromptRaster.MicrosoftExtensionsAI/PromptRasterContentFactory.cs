using Microsoft.Extensions.AI;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// The default <see cref="IPromptRasterContentFactory"/>, backed by
/// <see cref="IPromptRasterizer"/>.
/// </summary>
internal sealed class PromptRasterContentFactory(IPromptRasterizer rasterizer) : IPromptRasterContentFactory
{
    public async ValueTask<IReadOnlyList<AIContent>> CreateAsync(
        string instruction,
        string text,
        AiProvider provider,
        PromptRasterRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(text);

        var result = await rasterizer.RasterizeAsync(text, provider, request, cancellationToken)
            .ConfigureAwait(false);

        var contents = new List<AIContent> { new TextContent(instruction) };

        if (result.Encoding == PromptRasterEncoding.Text)
        {
            contents.Add(new TextContent(result.OriginalText));
            return contents;
        }

        contents.AddRange(PromptRasterPageInstructions.ToImageContents(result));
        return contents;
    }
}
