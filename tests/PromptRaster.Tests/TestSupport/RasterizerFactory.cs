using Microsoft.Extensions.DependencyInjection;

namespace PromptRaster.Tests.TestSupport;

/// <summary>
/// Builds a fully wired <see cref="IPromptRasterizer"/> through the public
/// dependency injection entry point.
/// </summary>
internal static class RasterizerFactory
{
    public static IPromptRasterizer Create(Action<PromptRasterOptions>? configure = null)
    {
        var services = new ServiceCollection();

        if (configure is null)
        {
            services.AddPromptRaster();
        }
        else
        {
            services.AddPromptRaster(configure);
        }

        return services.BuildServiceProvider().GetRequiredService<IPromptRasterizer>();
    }
}
