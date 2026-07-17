namespace PromptRaster;

/// <summary>
/// Per-request overrides for a single rasterisation call.
/// </summary>
public sealed record PromptRasterRequest
{
    /// <summary>How the rasterisation decision should be made. Defaults to <see cref="PromptRasterMode.Auto"/>.</summary>
    public PromptRasterMode Mode { get; init; } = PromptRasterMode.Auto;

    /// <summary>
    /// Skips automatic content classification and treats the input as prose.
    /// Rasterisation is still subject to the density threshold, so this does
    /// not force images the way <see cref="PromptRasterMode.Always"/> does.
    /// </summary>
    public bool TreatAsProse { get; init; }

    /// <summary>Whether to draw a small "Page 1 of 3" header on each page. Defaults to <see langword="true"/>.</summary>
    public bool IncludePageHeader { get; init; } = true;

    /// <summary>Overrides the configured maximum automatic page count for this request.</summary>
    public int? MaximumPages { get; init; }

    /// <summary>Overrides the provider's default minimum characters-per-page threshold for this request.</summary>
    public int? MinimumCharactersPerPage { get; init; }

    /// <summary>
    /// Optional provider-specific model identifier used for <see cref="ModelProfile"/> lookup.
    /// When supplied, automatic rasterisation requires a registered and enabled profile.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the content is treated as explicitly excluded from rasterisation
    /// and remains ordinary text.
    /// </summary>
    public bool ExcludeFromRasterisation { get; init; }
}
