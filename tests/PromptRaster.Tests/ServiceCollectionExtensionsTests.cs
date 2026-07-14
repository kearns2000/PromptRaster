using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace PromptRaster.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPromptRaster_RegistersRasterizerAsSingleton()
    {
        var provider = new ServiceCollection().AddPromptRaster().BuildServiceProvider();

        var first = provider.GetRequiredService<IPromptRasterizer>();
        var second = provider.GetRequiredService<IPromptRasterizer>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddPromptRaster_WithConfiguration_AppliesOptions()
    {
        var provider = new ServiceCollection()
            .AddPromptRaster(static options =>
            {
                options.MinimumTextLength = 6_000;
                options.MaximumPages = 10;
            })
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PromptRasterOptions>>().Value;

        options.MinimumTextLength.Should().Be(6_000);
        options.MaximumPages.Should().Be(10);
    }

    [Fact]
    public void AddPromptRaster_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection().AddPromptRaster().AddPromptRaster();

        services.Count(static d => d.ServiceType == typeof(IPromptRasterizer)).Should().Be(1);
    }
}
