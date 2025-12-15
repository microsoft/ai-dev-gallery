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
    private const int DefaultMaxTokens = 1024;

    private readonly Microsoft.AI.Foundry.Local.OpenAIChatClient _chatClient;
    private readonly string _modelId;

    public FoundryLocalChatClientAdapter(Microsoft.AI.Foundry.Local.OpenAIChatClient chatClient, string modelId)
    {
        _modelId = modelId;
        _chatClient = chatClient;
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
        // Map ChatOptions to FoundryLocal ChatSettings
        // CRITICAL: MaxTokens must be set, otherwise some model won't generate any output
        _chatClient.Settings.MaxTokens = options?.MaxOutputTokens ?? DefaultMaxTokens;
        
        if (options?.Temperature != null)
        {
            _chatClient.Settings.Temperature = (float)options.Temperature;
        }

        if (options?.TopP != null)
        {
            _chatClient.Settings.TopP = (float)options.TopP;
        }

        if (options?.TopK != null)
        {
            _chatClient.Settings.TopK = options.TopK;
        }

        if (options?.FrequencyPenalty != null)
        {
            _chatClient.Settings.FrequencyPenalty = (float)options.FrequencyPenalty;
        }

        if (options?.PresencePenalty != null)
        {
            _chatClient.Settings.PresencePenalty = (float)options.PresencePenalty;
        }

        if (options?.Seed != null)
        {
            _chatClient.Settings.RandomSeed = (int)options.Seed;
        }

        var messageList = chatMessages.ToList();
        var openAIMessages = ConvertToFoundryMessages(messageList);
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