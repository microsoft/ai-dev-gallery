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
    private readonly int? _modelMaxOutputTokens;

    public FoundryLocalChatClientAdapter(Microsoft.AI.Foundry.Local.OpenAIChatClient chatClient, string modelId, int? modelMaxOutputTokens = null)
    {
        _modelId = modelId;
        _chatClient = chatClient;
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
        ApplyChatOptions(options);

        var openAIMessages = ConvertToOpenAIMessages(chatMessages);
        var streamingResponse = _chatClient.CompleteChatStreamingAsync(openAIMessages, cancellationToken);

        string responseId = Guid.NewGuid().ToString("N");
        int chunkCount = 0;
        await foreach (var chunk in streamingResponse)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chunkCount++;

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

        if (chunkCount == 0)
        {
            var errorMessage = $"The model '{_modelId}' did not generate any output. " +
                             "Please verify you have selected an appropriate language model.";
            Telemetry.Events.FoundryLocalErrorEvent.Log("ChatStreaming", "NoOutput", _modelId, errorMessage);
            throw new InvalidOperationException(errorMessage);
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

    private void ApplyChatOptions(ChatOptions? options)
    {
        // CRITICAL: MaxTokens must be set, otherwise some models won't generate any output
        _chatClient.Settings.MaxTokens = options?.MaxOutputTokens ?? _modelMaxOutputTokens ?? DefaultMaxTokens;

        if (options?.Temperature is float temperature)
        {
            _chatClient.Settings.Temperature = temperature;
        }

        if (options?.TopP is float topP)
        {
            _chatClient.Settings.TopP = topP;
        }

        if (options?.TopK is int topK)
        {
            _chatClient.Settings.TopK = topK;
        }

        if (options?.FrequencyPenalty is float frequencyPenalty)
        {
            _chatClient.Settings.FrequencyPenalty = frequencyPenalty;
        }

        if (options?.PresencePenalty is float presencePenalty)
        {
            _chatClient.Settings.PresencePenalty = presencePenalty;
        }

        if (options?.Seed is long seed)
        {
            _chatClient.Settings.RandomSeed = (int)seed;
        }
    }

    /// <summary>
    /// Converts Microsoft.Extensions.AI chat messages to OpenAI-compatible format.
    /// </summary>
    private static List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> ConvertToOpenAIMessages(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        return messages.Select(m => new Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage
        {
            Role = m.Role.Value,
            Content = m.Text ?? string.Empty // NOTE: Only supports text content; multi-modal content (images, etc.) is not handled
        }).ToList();
    }
}