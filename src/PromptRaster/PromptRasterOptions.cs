namespace PromptRaster;

/// <summary>
/// Configuration for PromptRaster. All values are validated at startup;
/// see <c>AddPromptRaster</c> for registration.
/// </summary>
public sealed class PromptRasterOptions
{
    /// <summary>The configuration section name used when binding from <c>IConfiguration</c>.</summary>
    public const string SectionName = "PromptRaster";

    /// <summary>Text shorter than this is never automatically rasterised.</summary>
    public int MinimumTextLength { get; set; } = 5_000;

    /// <summary>The maximum number of pages allowed for automatic rasterisation.</summary>
    public int MaximumPages { get; set; } = 8;

    /// <summary>
    /// The hard page limit that even <see cref="PromptRasterMode.Always"/> cannot exceed.
    /// </summary>
    public int AbsoluteMaximumPages { get; set; } = 50;

    /// <summary>The rendered image width in pixels.</summary>
    public int ImageWidth { get; set; } = 1_024;

    /// <summary>The rendered image height in pixels.</summary>
    public int ImageHeight { get; set; } = 1_536;

    /// <summary>The left and right padding in pixels.</summary>
    public int HorizontalPadding { get; set; } = 48;

    /// <summary>The top and bottom padding in pixels.</summary>
    public int VerticalPadding { get; set; } = 48;

    /// <summary>The body font size in pixels.</summary>
    public float FontSize { get; set; } = 17;

    /// <summary>The line height as a multiple of the font's natural line height.</summary>
    public float LineSpacingMultiplier { get; set; } = 1.25f;

    /// <summary>The minimum average characters per page required to rasterise for OpenAI.</summary>
    public int OpenAIMinimumCharactersPerPage { get; set; } = 6_000;

    /// <summary>The minimum average characters per page required to rasterise for Azure OpenAI.</summary>
    public int AzureOpenAIMinimumCharactersPerPage { get; set; } = 6_000;

    /// <summary>The minimum average characters per page required to rasterise for Gemini.</summary>
    public int GeminiMinimumCharactersPerPage { get; set; } = 5_500;

    /// <summary>The minimum average characters per page required to rasterise for Anthropic.</summary>
    public int AnthropicMinimumCharactersPerPage { get; set; } = 8_000;

    /// <summary>
    /// Whether to automatically detect structured or accuracy-sensitive content
    /// (JSON, code, tables, and so on) and keep it as text.
    /// </summary>
    public bool DetectStructuredContent { get; set; } = true;
}
