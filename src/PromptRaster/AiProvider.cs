namespace PromptRaster;

/// <summary>
/// Identifies the AI provider that will ultimately receive the content.
/// The provider determines the conservative character-density threshold
/// used when deciding whether rasterisation is likely to save input tokens.
/// </summary>
public enum AiProvider
{
    /// <summary>The provider is not known. Content is never automatically rasterised.</summary>
    Unknown,

    /// <summary>OpenAI (for example GPT-4o and later multimodal models).</summary>
    OpenAI,

    /// <summary>Azure OpenAI Service.</summary>
    AzureOpenAI,

    /// <summary>Anthropic (Claude models).</summary>
    Anthropic,

    /// <summary>Google Gemini.</summary>
    Gemini
}
