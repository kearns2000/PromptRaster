using System.Globalization;
using Microsoft.Extensions.AI;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Shared helpers for converting rasterised pages into Microsoft.Extensions.AI content.
/// </summary>
internal static class PromptRasterPageInstructions
{
    public static string CreateReadingInstruction(int pageCount) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The document is attached as {pageCount} PNG image page(s). " +
            $"Read every page completely, in numerical page order, starting at page 1.");

    public static List<AIContent> ToImageContents(PromptRasterResult result)
    {
        var contents = new List<AIContent>(result.Pages.Count + 1)
        {
            new TextContent(CreateReadingInstruction(result.PageCount)),
        };

        foreach (var page in result.Pages.OrderBy(static p => p.PageNumber))
        {
            contents.Add(new DataContent(page.Data, page.MediaType));
        }

        return contents;
    }
}
