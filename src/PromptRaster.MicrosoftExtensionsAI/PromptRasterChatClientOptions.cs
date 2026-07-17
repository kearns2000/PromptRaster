namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Options for the <see cref="PromptRasterChatClient"/> middleware.
/// Rasterisation is opt-in: only <see cref="RasterTextContent"/> is considered.
/// </summary>
public sealed class PromptRasterChatClientOptions
{
    /// <summary>
    /// Minimum character count for a marked content item before rasterisation is attempted.
    /// Shorter marked content falls back to ordinary text unless strict mode is enabled.
    /// </summary>
    public int MinimumCharacterCount { get; set; } = 4_000;

    /// <summary>
    /// When <see langword="true"/> (the default), policy rejection and rendering failure
    /// leave the original text as <see cref="Microsoft.Extensions.AI.TextContent"/>.
    /// </summary>
    public bool FallbackToText { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, policy rejection or rendering failure throws instead
    /// of falling back to text.
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>The AI provider family used for threshold resolution.</summary>
    public AiProvider Provider { get; set; } = AiProvider.OpenAI;

    /// <summary>
    /// Optional model identifier for <see cref="ModelProfile"/> lookup.
    /// Unknown models fall back to text unless strict mode is enabled.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>Per-request overrides forwarded to <see cref="IPromptRasterizer"/>.</summary>
    public PromptRasterRequest? Request { get; set; }
}
