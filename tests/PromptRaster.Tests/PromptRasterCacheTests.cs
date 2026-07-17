using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class PromptRasterCacheTests
{
    [Fact]
    public async Task CachedPages_AreCloned_SoCallersCannotMutateSharedEntries()
    {
        var cache = new InMemoryPromptRasterCache();
        var services = new ServiceCollection();
        services.AddPromptRaster();
        services.RemoveAll<IPromptRasterCache>();
        services.AddSingleton<IPromptRasterCache>(cache);

        var rasterizer = services.BuildServiceProvider().GetRequiredService<IPromptRasterizer>();
        var text = ProseGenerator.Generate(8_000);
        var request = new PromptRasterRequest { Mode = PromptRasterMode.Always };

        var first = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI, request);
        first.Encoding.Should().Be(PromptRasterEncoding.Images);
        first.Pages.Should().NotBeEmpty();

        var originalByte = first.Pages[0].Data[0];
        first.Pages[0].Data[0] = unchecked((byte)(originalByte + 1));

        var second = await rasterizer.RasterizeAsync(text, AiProvider.OpenAI, request);

        second.Pages[0].Data[0].Should().Be(originalByte);
        cache.HitCount.Should().BeGreaterThan(0);
    }

    private sealed class InMemoryPromptRasterCache : IPromptRasterCache
    {
        private readonly Dictionary<string, IReadOnlyList<PromptRasterPage>> _entries = new(StringComparer.Ordinal);

        public int HitCount { get; private set; }

        public bool TryGet(string cacheKey, out IReadOnlyList<PromptRasterPage>? pages)
        {
            if (_entries.TryGetValue(cacheKey, out pages))
            {
                HitCount++;
                return true;
            }

            pages = null;
            return false;
        }

        public void Set(string cacheKey, IReadOnlyList<PromptRasterPage> pages) =>
            _entries[cacheKey] = pages;
    }
}
