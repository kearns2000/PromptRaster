using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Dependency injection registration for the Microsoft.Extensions.AI integration.
/// </summary>
public static class PromptRasterMicrosoftExtensionsAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPromptRasterContentFactory"/>. The core PromptRaster
    /// services are also registered if they have not been registered already.
    /// </summary>
    public static IServiceCollection AddPromptRasterMicrosoftExtensionsAI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPromptRaster();
        services.TryAddSingleton<IPromptRasterContentFactory, PromptRasterContentFactory>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IPromptRasterContentFactory"/> and applies
    /// <paramref name="configure"/> to the PromptRaster options.
    /// </summary>
    public static IServiceCollection AddPromptRasterMicrosoftExtensionsAI(
        this IServiceCollection services,
        Action<PromptRasterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddPromptRasterMicrosoftExtensionsAI();
        services.Configure(configure);

        return services;
    }
}
