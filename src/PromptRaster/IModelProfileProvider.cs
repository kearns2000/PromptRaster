namespace PromptRaster;

/// <summary>
/// Resolves <see cref="ModelProfile"/> values for provider-specific model identifiers.
/// </summary>
public interface IModelProfileProvider
{
    /// <summary>
    /// Attempts to resolve a profile for <paramref name="modelId"/>.
    /// </summary>
    /// <param name="modelId">The provider-specific model identifier.</param>
    /// <param name="profile">The resolved profile when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when a profile is known; otherwise <see langword="false"/>.
    /// Unknown models should fall back to text by default.
    /// </returns>
    bool TryGetProfile(string? modelId, out ModelProfile? profile);
}
