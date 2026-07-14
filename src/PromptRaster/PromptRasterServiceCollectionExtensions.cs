using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptRaster.Internal;

namespace PromptRaster;

/// <summary>
/// Dependency injection registration for PromptRaster.
/// </summary>
public static class PromptRasterServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPromptRasterizer"/> and its supporting services
    /// with default options. All services are stateless and registered as singletons.
    /// </summary>
    public static IServiceCollection AddPromptRaster(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<PromptRasterOptions>().ValidateOnStart();
        services.TryAddSingleton<Microsoft.Extensions.Options.IValidateOptions<PromptRasterOptions>, PromptRasterOptionsValidator>();

        services.TryAddSingleton<ITextContentClassifier, TextContentClassifier>();
        services.TryAddSingleton<ITextPageLayoutEngine, SkiaTextPageLayoutEngine>();
        services.TryAddSingleton<ITextImageRenderer, SkiaTextImageRenderer>();
        services.TryAddSingleton<IProviderThresholdResolver, ProviderThresholdResolver>();
        services.TryAddSingleton<IPromptRasterizer, PromptRasterizer>();

        return services;
    }

    /// <summary>
    /// Registers PromptRaster and applies <paramref name="configure"/> to the options.
    /// </summary>
    public static IServiceCollection AddPromptRaster(
        this IServiceCollection services,
        Action<PromptRasterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddPromptRaster();
        services.Configure(configure);

        return services;
    }
}
