namespace PromptRaster;

/// <summary>
/// Rendering and eligibility settings for a specific multimodal model.
/// Unknown models should fall back to text unless a profile enables them.
/// Transient provider prices are intentionally not part of this model.
/// </summary>
public sealed class ModelProfile
{
    /// <summary>The provider-specific model identifier (for example <c>gpt-4.1</c>).</summary>
    public required string ModelId { get; init; }

    /// <summary>Whether automatic rasterisation is enabled for this model.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The AI provider family associated with this model.</summary>
    public AiProvider Provider { get; init; } = AiProvider.Unknown;

    /// <summary>Preferred rendered page width in pixels.</summary>
    public int ImageWidth { get; init; } = 1_024;

    /// <summary>Preferred rendered page height in pixels.</summary>
    public int ImageHeight { get; init; } = 1_536;

    /// <summary>Preferred body font size in pixels.</summary>
    public float FontSize { get; init; } = 17;

    /// <summary>Preferred left and right padding in pixels.</summary>
    public int HorizontalPadding { get; init; } = 48;

    /// <summary>Preferred top and bottom padding in pixels.</summary>
    public int VerticalPadding { get; init; } = 48;

    /// <summary>Maximum pages allowed for automatic rasterisation with this model.</summary>
    public int MaximumPages { get; init; } = 8;

    /// <summary>Minimum average characters per page required to rasterise.</summary>
    public int MinimumCharactersPerPage { get; init; } = 6_000;

    /// <summary>
    /// Optional notes from evaluation work describing OCR quality or density limits
    /// for this model. Not used by the decision algorithm.
    /// </summary>
    public string? EvaluationNotes { get; init; }
}
