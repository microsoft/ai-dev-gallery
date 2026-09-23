// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

/// <summary>
/// Adapter that wraps Foundry Local's ChatSession API as a Microsoft.Extensions.AI.IChatClient.
/// </summary>
internal class FoundryLocalChatClientAdapter : IChatClient
{
    private const int DefaultMaxTokens = 1024;

    private readonly IModel _model;
    private readonly string _modelId;
    private readonly int? _modelMaxOutputTokens;

    public FoundryLocalChatClientAdapter(IModel model, string modelId, int? modelMaxOutputTokens = null)
    {
        _modelId = modelId;
        _model = model;
        _modelMaxOutputTokens = modelMaxOutputTokens;
    }

    public ChatClientMetadata Metadata => new("FoundryLocal", new Uri($"foundrylocal:///{_modelId}"), _modelId);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(chatMessages, options, cancellationToken).ToChatResponseAsync(cancellationToken: cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        using var session = new ChatSession(_model);
        session.SetStreaming(true);

        using var request = new Request();
        foreach (var message in chatMessages)
        {
            if (ConvertToFoundryMessageData(message) is { } messageData)
            {
                using var messageItem = new MessageItem(messageData.Role, messageData.Content);
                request.AddItem(messageItem);
            }
        }

        if (request.ItemCount == 0)
        {
            throw new ArgumentException("At least one non-empty text chat message is required.", nameof(chatMessages));
        }

        request.SetOptions(CreateRequestOptions(options));

        // Key Perf Log
        System.Diagnostics.Debug.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Starting inference");
        await using var streamingResponse = session.ProcessStreamingRequestAsync(request, cancellationToken);

        string responseId = Guid.NewGuid().ToString("N");
        int chunkCount = 0;
        await foreach (var chunk in streamingResponse)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? content = null;
            using (chunk)
            {
                if (chunk is TextItem textItem)
                {
                    content = textItem.Text;
                }
            }

            if (!string.IsNullOrEmpty(content))
            {
                if (chunkCount == 0)
                {
                    // Key Perf Log
                    System.Diagnostics.Debug.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] First token received");
                }

                chunkCount++;
                yield return new ChatResponseUpdate(ChatRole.Assistant, content)
                {
                    ResponseId = responseId
                };
            }
        }

        using var finalResponse = await streamingResponse.FinalResponse.ConfigureAwait(false);

        if (chunkCount == 0)
        {
            var errorMessage = $"The model '{_modelId}' did not generate any output. " +
                             "Please verify you have selected an appropriate language model.";
            Telemetry.Events.FoundryLocalErrorEvent.Log("ChatStreaming", "NoOutput", _modelId, errorMessage); // <exclude-line>
            throw new InvalidOperationException(errorMessage);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public void Dispose()
    {
        // ChatSession instances are scoped and disposed per request.
    }

    private RequestOptions CreateRequestOptions(ChatOptions? options)
    {
        return new RequestOptions
        {
            Search = new SearchOptions
            {
                MaxOutputTokens = options?.MaxOutputTokens ?? _modelMaxOutputTokens ?? DefaultMaxTokens,
                Temperature = options?.Temperature,
                TopP = options?.TopP,
                TopK = options?.TopK,
                FrequencyPenalty = options?.FrequencyPenalty,
                PresencePenalty = options?.PresencePenalty,
                Seed = options?.Seed is long seed ? checked((int)seed) : null
            }
        };
    }

    private static (MessageRole Role, string Content)? ConvertToFoundryMessageData(Microsoft.Extensions.AI.ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            if (content is not TextContent)
            {
                throw new NotSupportedException(
                    $"Foundry Local text chat does not support content of type '{content.GetType().Name}'.");
            }
        }

        if (string.IsNullOrEmpty(message.Text))
        {
            return null;
        }

        var role = message.Role.Value.ToLowerInvariant() switch
        {
            "system" => MessageRole.System,
            "user" => MessageRole.User,
            "assistant" => MessageRole.Assistant,
            "developer" => MessageRole.Developer,
            "tool" => MessageRole.Tool,
            _ => throw new NotSupportedException($"Foundry Local does not support the chat role '{message.Role.Value}'.")
        };

        return (role, message.Text);
    }
}