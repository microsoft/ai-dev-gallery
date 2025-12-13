// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

/// <summary>
/// Adapter that wraps FoundryLocal SDK's native OpenAIChatClient to work with Microsoft.Extensions.AI.IChatClient.
/// Uses the SDK's direct model API (no web service) to avoid SSE compatibility issues.
/// </summary>
internal class FoundryLocalChatClientAdapter : IChatClient
{
    private readonly Microsoft.AI.Foundry.Local.OpenAIChatClient _chatClient;
    private readonly string _modelId;

    public FoundryLocalChatClientAdapter(Microsoft.AI.Foundry.Local.OpenAIChatClient chatClient, string modelId)
    {
        _modelId = modelId;
        _chatClient = chatClient;

        // CRITICAL: MaxTokens must be set, otherwise the model won't generate any output
        if (_chatClient.Settings.MaxTokens == null)
        {
            _chatClient.Settings.MaxTokens = 512;
        }

        if (_chatClient.Settings.Temperature == null)
        {
            _chatClient.Settings.Temperature = 0.7f;
        }
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
        var messageList = chatMessages.ToList();
        var openAIMessages = ConvertToFoundryMessages(messageList);

        // Use FoundryLocal SDK's native streaming API - direct in-memory communication, no HTTP/SSE
        var streamingResponse = _chatClient.CompleteChatStreamingAsync(openAIMessages, cancellationToken);

        string responseId = Guid.NewGuid().ToString("N");
        await foreach (var chunk in streamingResponse)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunk.Choices != null && chunk.Choices.Count > 0)
            {
                var content = chunk.Choices[0].Message?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, content)
                    {
                        ResponseId = responseId
                    };
                }
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public void Dispose()
    {
        // ChatClient doesn't need disposal
    }

    private static List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> ConvertToFoundryMessages(IList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        return messages.Select(m => new Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage
        {
            Role = m.Role.Value,
            Content = m.Text ?? string.Empty
        }).ToList();
    }
}