using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PromptRaster.MicrosoftExtensionsAI;
using Xunit;

namespace PromptRaster.MicrosoftExtensionsAI.Tests;

public class PromptRasterContentFactoryTests
{
    private static IPromptRasterContentFactory CreateFactory(IPromptRasterizer? rasterizer = null)
    {
        var services = new ServiceCollection();

        if (rasterizer is not null)
        {
            services.AddSingleton(rasterizer);
        }

        services.AddPromptRasterMicrosoftExtensionsAI();

        return services.BuildServiceProvider().GetRequiredService<IPromptRasterContentFactory>();
    }

    [Fact]
    public async Task Instruction_IsAlwaysTheFirstTextContent()
    {
        var factory = CreateFactory();

        var contents = await factory.CreateAsync(
            "Summarise the following document.",
            "A short document.",
            AiProvider.OpenAI);

        contents[0].Should().BeOfType<TextContent>()
            .Which.Text.Should().Be("Summarise the following document.");
    }

    [Fact]
    public async Task TextDecision_ProducesInstructionAndDocumentAsText()
    {
        var factory = CreateFactory();

        var contents = await factory.CreateAsync(
            "Summarise the following document.",
            "A short document that stays as text.",
            AiProvider.OpenAI);

        contents.Should().HaveCount(2);
        contents.Should().AllBeOfType<TextContent>();
        contents[1].Should().BeOfType<TextContent>()
            .Which.Text.Should().Be("A short document that stays as text.");
    }

    [Fact]
    public async Task ImageDecision_ProducesOneDataContentPerPage_InPageOrder()
    {
        var rasterizer = new FakeRasterizer(pageCount: 3);
        var factory = CreateFactory(rasterizer);

        var contents = await factory.CreateAsync(
            "Summarise.",
            "irrelevant, the fake decides",
            AiProvider.OpenAI);

        var images = contents.OfType<DataContent>().ToArray();
        images.Should().HaveCount(3);

        for (var i = 0; i < images.Length; i++)
        {
            // The fake tags each page's first byte with its page number.
            images[i].Data.Span[0].Should().Be((byte)(i + 1));
        }
    }

    [Fact]
    public async Task ImageDecision_UsesPngMediaType()
    {
        var factory = CreateFactory(new FakeRasterizer(pageCount: 2));

        var contents = await factory.CreateAsync("Summarise.", "text", AiProvider.OpenAI);

        contents.OfType<DataContent>().Should().AllSatisfy(static image =>
            image.MediaType.Should().Be("image/png"));
    }

    [Fact]
    public async Task ImageDecision_IncludesReadingInstructionBeforeImages()
    {
        var factory = CreateFactory(new FakeRasterizer(pageCount: 2));

        var contents = await factory.CreateAsync("Summarise.", "text", AiProvider.OpenAI);

        var textContents = contents.OfType<TextContent>().ToArray();
        textContents.Should().HaveCount(2, "the instruction plus the page-order note");
        textContents[1].Text.Should().Contain("order");

        // All text comes before any image.
        var firstImageIndex = contents.ToList().FindIndex(static c => c is DataContent);
        contents.Take(firstImageIndex).Should().AllBeOfType<TextContent>();
    }

    [Fact]
    public async Task ImageDecision_WorksEndToEnd_WithRealRasterizer()
    {
        var factory = CreateFactory();
        var prose = string.Concat(Enumerable.Repeat(
            "The committee reviewed the quarterly findings and noted that regional supply " +
            "patterns had shifted meaningfully during recent months. ", 300));

        var contents = await factory.CreateAsync(
            "Summarise.",
            prose,
            AiProvider.OpenAI,
            new PromptRasterRequest { Mode = PromptRasterMode.Always });

        contents.OfType<DataContent>().Should().NotBeEmpty();
        contents.OfType<DataContent>().Should().AllSatisfy(static image =>
        {
            image.MediaType.Should().Be("image/png");
            image.Data.Length.Should().BePositive();
        });
    }

    [Fact]
    public async Task Cancellation_IsPassedThrough()
    {
        var rasterizer = new FakeRasterizer(pageCount: 1);
        var factory = CreateFactory(rasterizer);
        using var cts = new CancellationTokenSource();

        await factory.CreateAsync("Summarise.", "text", AiProvider.OpenAI, cancellationToken: cts.Token);

        rasterizer.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task NullArguments_Throw()
    {
        var factory = CreateFactory();

        var nullInstruction = async () => await factory.CreateAsync(null!, "text", AiProvider.OpenAI);
        var nullText = async () => await factory.CreateAsync("instruction", null!, AiProvider.OpenAI);

        await nullInstruction.Should().ThrowAsync<ArgumentNullException>();
        await nullText.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void AddPromptRasterMicrosoftExtensionsAI_RegistersCoreServices()
    {
        var provider = new ServiceCollection()
            .AddPromptRasterMicrosoftExtensionsAI()
            .BuildServiceProvider();

        provider.GetService<IPromptRasterizer>().Should().NotBeNull();
        provider.GetService<IPromptRasterContentFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddPromptRasterMicrosoftExtensionsAI_WithConfiguration_AppliesOptions()
    {
        var provider = new ServiceCollection()
            .AddPromptRasterMicrosoftExtensionsAI(static options => options.MaximumPages = 12)
            .BuildServiceProvider();

        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<PromptRasterOptions>>().Value;

        options.MaximumPages.Should().Be(12);
    }

    /// <summary>
    /// Returns a deterministic image decision whose page data starts with the
    /// page number, so ordering can be asserted without decoding pixels.
    /// </summary>
    private sealed class FakeRasterizer(int pageCount) : IPromptRasterizer
    {
        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<PromptRasterResult> RasterizeAsync(
            string text,
            AiProvider provider,
            PromptRasterRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;

            var pages = Enumerable.Range(1, pageCount)
                .Select(number => new PromptRasterPage
                {
                    PageNumber = number,
                    TotalPages = pageCount,
                    Data = [(byte)number, 0xAA, 0xBB],
                    MediaType = "image/png",
                    SourceStartIndex = 0,
                    SourceLength = text.Length,
                    CharacterCount = text.Length,
                })
                .ToArray();

            var result = new PromptRasterResult
            {
                Encoding = PromptRasterEncoding.Images,
                Decision = new PromptRasterDecision
                {
                    Reason = PromptRasterDecisionReason.Rasterised,
                    Description = "Fake decision for testing.",
                },
                OriginalText = text,
                SourceSha256 = "00",
                CharacterCount = text.Length,
                PageCount = pageCount,
                AverageCharactersPerPage = text.Length,
                RequiredCharactersPerPage = 6_000,
                Pages = pages,
            };

            return new ValueTask<PromptRasterResult>(result);
        }
    }
}
