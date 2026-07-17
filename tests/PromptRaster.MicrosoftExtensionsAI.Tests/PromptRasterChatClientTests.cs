using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PromptRaster.MicrosoftExtensionsAI;
using PromptRaster.MicrosoftExtensionsAI.Tests.TestSupport;
using Xunit;

namespace PromptRaster.MicrosoftExtensionsAI.Tests;

public class PromptRasterChatClientTests
{
    [Fact]
    public async Task ExplicitRasterText_IsRasterised()
    {
        var prose = Prose.Generate(12_000);
        var inner = new CapturingChatClient();
        var client = CreateClient(inner, options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Always };
        });

        var message = new ChatMessage(ChatRole.User, "Summarise the document.");
        message.AddRasterText(prose);

        await client.GetResponseAsync([message]);

        inner.LastMessages.Should().NotBeNull();
        var contents = inner.LastMessages!.Single().Contents;
        contents.OfType<DataContent>().Should().NotBeEmpty();
        contents.OfType<DataContent>().Should().AllSatisfy(static c => c.MediaType.Should().Be("image/png"));
        contents.OfType<RasterTextContent>().Should().BeEmpty();
    }

    [Fact]
    public async Task NormalTextContent_IsUntouched()
    {
        var inner = new CapturingChatClient();
        var client = CreateClient(inner);
        var message = new ChatMessage(ChatRole.User, "Keep this as ordinary text.");

        await client.GetResponseAsync([message]);

        var contents = inner.LastMessages!.Single().Contents;
        contents.Should().ContainSingle()
            .Which.Should().BeOfType<TextContent>()
            .Which.Text.Should().Be("Keep this as ordinary text.");
    }

    [Fact]
    public async Task UnsupportedModel_FallsBackToText()
    {
        var prose = Prose.Generate(12_000);
        var inner = new CapturingChatClient();
        var client = CreateClient(inner, options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.ModelId = "totally-unknown-model";
            options.FallbackToText = true;
        });

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(prose);

        await client.GetResponseAsync([message]);

        inner.LastMessages!.Single().Contents.Should().ContainSingle()
            .Which.Should().BeOfType<TextContent>()
            .Which.Text.Should().Be(prose);
    }

    [Fact]
    public async Task PolicyRejection_LeavesContentAsText()
    {
        var inner = new CapturingChatClient();
        var client = CreateClient(inner, options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Never };
        });

        const string text = "This marked content stays as text because Never was requested.";
        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(text);

        await client.GetResponseAsync([message]);

        inner.LastMessages!.Single().Contents.Should().ContainSingle()
            .Which.Should().BeOfType<TextContent>()
            .Which.Text.Should().Be(text);
    }

    [Fact]
    public async Task StrictMode_PropagatesFailure()
    {
        var client = CreateClient(new CapturingChatClient(), options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.StrictMode = true;
            options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Never };
        });

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(Prose.Generate(2_000));

        var act = async () => await client.GetResponseAsync([message]);

        await act.Should().ThrowAsync<PromptRasterException>();
    }

    [Fact]
    public async Task FallbackDisabled_WrapsRenderExceptions()
    {
        var failingRasterizer = new ThrowingRasterizer();
        var client = new PromptRasterChatClient(
            new CapturingChatClient(),
            failingRasterizer,
            new PromptRasterChatClientOptions
            {
                MinimumCharacterCount = 1,
                FallbackToText = false,
                StrictMode = false,
            });

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText("marked content long enough");

        var act = async () => await client.GetResponseAsync([message]);

        var exception = await act.Should().ThrowAsync<PromptRasterException>();
        exception.Which.Reason.Should().Be(PromptRasterDecisionReason.RenderingFailed);
        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancellation_IsHonoured()
    {
        var client = CreateClient(new CapturingChatClient(), options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Always };
        });

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(Prose.Generate(12_000));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await client.GetResponseAsync([message], cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CallerCollections_AreNotMutated()
    {
        var prose = Prose.Generate(12_000);
        var inner = new CapturingChatClient();
        var client = CreateClient(inner, options =>
        {
            options.MinimumCharacterCount = 1_000;
            options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Always };
        });

        var originalContents = new List<AIContent>
        {
            new TextContent("Summarise."),
            new RasterTextContent(prose),
        };
        var message = new ChatMessage(ChatRole.User, originalContents);
        var messages = new List<ChatMessage> { message };

        await client.GetResponseAsync(messages);

        messages.Should().HaveCount(1);
        messages[0].Should().BeSameAs(message);
        message.Contents.Should().BeSameAs(originalContents);
        originalContents[1].Should().BeOfType<RasterTextContent>();
        inner.LastMessages.Should().NotBeSameAs(messages);
        inner.LastMessages![0].Should().NotBeSameAs(message);
    }

    [Fact]
    public async Task StreamingAndNonStreaming_UseSameTransformationRules()
    {
        var prose = Prose.Generate(12_000);
        var nonStreamingInner = new CapturingChatClient();
        var streamingInner = new CapturingChatClient();

        var nonStreaming = CreateClient(nonStreamingInner, ConfigureAlways);
        var streaming = CreateClient(streamingInner, ConfigureAlways);

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(prose);

        await nonStreaming.GetResponseAsync([message]);

        await foreach (var _ in streaming.GetStreamingResponseAsync([message]))
        {
        }

        var nonStreamingImages = nonStreamingInner.LastMessages!.Single().Contents.OfType<DataContent>().Count();
        var streamingImages = streamingInner.LastMessages!.Single().Contents.OfType<DataContent>().Count();

        nonStreamingImages.Should().BePositive();
        streamingImages.Should().Be(nonStreamingImages);
    }

    [Fact]
    public async Task UsePromptRaster_RegistersViaBuilder()
    {
        var services = new ServiceCollection().AddPromptRasterMicrosoftExtensionsAI().BuildServiceProvider();
        var inner = new CapturingChatClient();
        var prose = Prose.Generate(12_000);

        var client = inner
            .AsBuilder()
            .UsePromptRaster(services.GetRequiredService<IPromptRasterizer>(), options =>
            {
                options.MinimumCharacterCount = 1_000;
                options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Always };
            })
            .Build();

        var message = new ChatMessage(ChatRole.User, []);
        message.AddRasterText(prose);

        await client.GetResponseAsync([message]);

        inner.LastMessages!.Single().Contents.OfType<DataContent>().Should().NotBeEmpty();
    }

    private static void ConfigureAlways(PromptRasterChatClientOptions options)
    {
        options.MinimumCharacterCount = 1_000;
        options.Request = new PromptRasterRequest { Mode = PromptRasterMode.Always };
    }

    private static PromptRasterChatClient CreateClient(
        IChatClient inner,
        Action<PromptRasterChatClientOptions>? configure = null)
    {
        var services = new ServiceCollection().AddPromptRasterMicrosoftExtensionsAI();
        var provider = services.BuildServiceProvider();
        var options = new PromptRasterChatClientOptions();
        configure?.Invoke(options);

        return new PromptRasterChatClient(
            inner,
            provider.GetRequiredService<IPromptRasterizer>(),
            options);
    }

    private sealed class ThrowingRasterizer : IPromptRasterizer
    {
        public ValueTask<PromptRasterResult> RasterizeAsync(
            string text,
            AiProvider provider,
            PromptRasterRequest? request = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("renderer exploded");
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public IList<ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToList();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
