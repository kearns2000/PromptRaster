namespace PromptRaster;

/// <summary>
/// Indicates how the source content should be sent to the AI provider.
/// </summary>
public enum PromptRasterEncoding
{
    /// <summary>Send the original text unchanged.</summary>
    Text,

    /// <summary>Send the rendered PNG pages instead of the text.</summary>
    Images
}
