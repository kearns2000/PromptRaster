using System.Globalization;
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

        contents.Add(new TextContent(string.Create(
            CultureInfo.InvariantCulture,
            $"The document is attached as {result.PageCount} PNG image page(s). " +
            $"Read every page completely, in numerical page order, starting at page 1.")));

        foreach (var page in result.Pages.OrderBy(static p => p.PageNumber))
        {
            contents.Add(new DataContent(page.Data, page.MediaType));
        }

        return contents;
    }
}
