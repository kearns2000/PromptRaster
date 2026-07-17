namespace PromptRaster.Internal;

/// <summary>
/// Resolves model profiles from <see cref="PromptRasterOptions.ModelProfiles"/>.
/// Lookup is case-insensitive. Unknown model identifiers have no profile.
/// </summary>
internal sealed class DefaultModelProfileProvider(Microsoft.Extensions.Options.IOptions<PromptRasterOptions> options)
    : IModelProfileProvider
{
    public bool TryGetProfile(string? modelId, out ModelProfile? profile)
    {
        profile = null;

        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var profiles = options.Value.ModelProfiles;

        if (profiles.Count == 0)
        {
            return false;
        }

        foreach (var candidate in profiles)
        {
            if (string.Equals(candidate.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }
}
