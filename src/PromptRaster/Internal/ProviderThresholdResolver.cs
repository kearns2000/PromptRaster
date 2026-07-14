using Microsoft.Extensions.Options;

namespace PromptRaster.Internal;

/// <summary>
/// Resolves provider thresholds from <see cref="PromptRasterOptions"/>, letting
/// a request-level override take precedence.
/// </summary>
internal sealed class ProviderThresholdResolver(IOptions<PromptRasterOptions> options) : IProviderThresholdResolver
{
    public int Resolve(AiProvider provider, PromptRasterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MinimumCharactersPerPage is { } requested)
        {
            return requested;
        }

        var value = options.Value;

        return provider switch
        {
            AiProvider.OpenAI => value.OpenAIMinimumCharactersPerPage,
            AiProvider.AzureOpenAI => value.AzureOpenAIMinimumCharactersPerPage,
            AiProvider.Gemini => value.GeminiMinimumCharactersPerPage,
            AiProvider.Anthropic => value.AnthropicMinimumCharactersPerPage,
            // Unknown providers never meet the density requirement.
            _ => int.MaxValue,
        };
    }
}
