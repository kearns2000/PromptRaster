using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Fluent registration for PromptRaster on a <see cref="ChatClientBuilder"/> pipeline.
/// </summary>
public static class PromptRasterChatClientBuilderExtensions
{
    /// <summary>
    /// Adds PromptRaster middleware that rasterises <see cref="RasterTextContent"/> only.
    /// Resolves <see cref="IPromptRasterizer"/> from the pipeline service provider.
    /// </summary>
    public static ChatClientBuilder UsePromptRaster(
        this ChatClientBuilder builder,
        Action<PromptRasterChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use((innerClient, services) =>
        {
            var options = CreateOptions(configure);
            var rasterizer = services.GetService<IPromptRasterizer>()
                ?? throw new InvalidOperationException(
                    "IPromptRasterizer is not registered. Call services.AddPromptRaster() " +
                    "or services.AddPromptRasterMicrosoftExtensionsAI(), then Build(serviceProvider).");

            var logger = services.GetService<ILogger<PromptRasterChatClient>>();
            return new PromptRasterChatClient(innerClient, rasterizer, options, logger);
        });
    }

    /// <summary>
    /// Adds PromptRaster middleware using an explicitly supplied <paramref name="rasterizer"/>.
    /// </summary>
    public static ChatClientBuilder UsePromptRaster(
        this ChatClientBuilder builder,
        IPromptRasterizer rasterizer,
        Action<PromptRasterChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rasterizer);

        var options = CreateOptions(configure);
        return builder.Use(innerClient => new PromptRasterChatClient(innerClient, rasterizer, options));
    }

    private static PromptRasterChatClientOptions CreateOptions(Action<PromptRasterChatClientOptions>? configure)
    {
        var options = new PromptRasterChatClientOptions();
        configure?.Invoke(options);
        return options;
    }
}
