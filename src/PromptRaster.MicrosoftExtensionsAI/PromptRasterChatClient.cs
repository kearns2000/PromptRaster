using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// <see cref="DelegatingChatClient"/> middleware that rasterises explicitly marked
/// <see cref="RasterTextContent"/> into PNG <see cref="DataContent"/> items.
/// Caller-owned message and content collections are never mutated.
/// </summary>
public sealed class PromptRasterChatClient : DelegatingChatClient
{
    private readonly IPromptRasterizer _rasterizer;
    private readonly PromptRasterChatClientOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates middleware around <paramref name="innerClient"/>.
    /// </summary>
    public PromptRasterChatClient(
        IChatClient innerClient,
        IPromptRasterizer rasterizer,
        PromptRasterChatClientOptions? options = null,
        ILogger<PromptRasterChatClient>? logger = null)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);
        _rasterizer = rasterizer;
        _options = options ?? new PromptRasterChatClientOptions();
        _logger = logger ?? NullLogger<PromptRasterChatClient>.Instance;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var transformed = await TransformMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
        return await base.GetResponseAsync(transformed, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var transformed = await TransformMessagesAsync(messages, cancellationToken).ConfigureAwait(false);

        await foreach (var update in base.GetStreamingResponseAsync(transformed, options, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private async Task<IList<ChatMessage>> TransformMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken stopToken)
    {
        var source = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var result = new List<ChatMessage>(source.Count);

        foreach (var message in source)
        {
            stopToken.ThrowIfCancellationRequested();
            result.Add(await TransformMessageAsync(message, stopToken).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<ChatMessage> TransformMessageAsync(ChatMessage message, CancellationToken stopToken)
    {
        if (message.Contents.Count == 0 || message.Contents.All(static c => c is not RasterTextContent))
        {
            return CloneMessage(message, message.Contents.ToList());
        }

        var contents = new List<AIContent>(message.Contents.Count);

        foreach (var content in message.Contents)
        {
            stopToken.ThrowIfCancellationRequested();

            if (content is RasterTextContent rasterText)
            {
                contents.AddRange(await RasteriseOrFallbackAsync(rasterText, stopToken).ConfigureAwait(false));
            }
            else
            {
                contents.Add(content);
            }
        }

        return CloneMessage(message, contents);
    }

    private async Task<IReadOnlyList<AIContent>> RasteriseOrFallbackAsync(
        RasterTextContent rasterText,
        CancellationToken stopToken)
    {
        if (rasterText.Text.Length < _options.MinimumCharacterCount)
        {
            return HandleRejection(
                rasterText,
                PromptRasterDecisionReason.TextTooShort,
                $"Marked content is {rasterText.Text.Length} characters, below MinimumCharacterCount {_options.MinimumCharacterCount}.");
        }

        var request = CreateRequest();

        try
        {
            var result = await _rasterizer.RasterizeAsync(
                    rasterText.Text,
                    _options.Provider,
                    request,
                    stopToken)
                .ConfigureAwait(false);

            if (result.Encoding == PromptRasterEncoding.Images && result.Pages.Count > 0)
            {
                PromptRasterChatClientLog.Rasterised(
                    _logger,
                    result.Decision.Reason,
                    result.CharacterCount,
                    result.PageCount);

                return PromptRasterPageInstructions.ToImageContents(result);
            }

            return HandleRejection(
                rasterText,
                result.Decision.Reason,
                result.Decision.Description);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PromptRasterException)
        {
            throw;
        }
        catch (Exception) when (!_options.StrictMode && _options.FallbackToText)
        {
            PromptRasterChatClientLog.Fallback(
                _logger,
                PromptRasterDecisionReason.RenderingFailed,
                rasterText.Text.Length);

            return [new TextContent(rasterText.Text)];
        }
        catch (Exception ex)
        {
            throw new PromptRasterException(
                _options.StrictMode
                    ? "PromptRaster rendering failed while strict mode was enabled."
                    : "PromptRaster rendering failed and FallbackToText is disabled.",
                PromptRasterDecisionReason.RenderingFailed,
                ex);
        }
    }

    private IReadOnlyList<AIContent> HandleRejection(
        RasterTextContent rasterText,
        PromptRasterDecisionReason reason,
        string? description)
    {
        if (_options.StrictMode)
        {
            throw new PromptRasterException(
                description ?? $"PromptRaster rejected rasterisation ({reason}).",
                reason);
        }

        if (!_options.FallbackToText)
        {
            throw new PromptRasterException(
                description ?? $"PromptRaster rejected rasterisation ({reason}) and FallbackToText is disabled.",
                reason);
        }

        PromptRasterChatClientLog.Fallback(_logger, reason, rasterText.Text.Length);
        return [new TextContent(rasterText.Text)];
    }

    private PromptRasterRequest CreateRequest()
    {
        var request = _options.Request ?? new PromptRasterRequest();

        if (_options.ModelId is not null && request.ModelId is null)
        {
            request = request with { ModelId = _options.ModelId };
        }

        return request;
    }

    private static ChatMessage CloneMessage(ChatMessage source, IList<AIContent> contents) =>
        new(source.Role, contents)
        {
            AuthorName = source.AuthorName,
            CreatedAt = source.CreatedAt,
            MessageId = source.MessageId,
            RawRepresentation = source.RawRepresentation,
            AdditionalProperties = source.AdditionalProperties is null
                ? null
                : new AdditionalPropertiesDictionary(source.AdditionalProperties),
        };
}
