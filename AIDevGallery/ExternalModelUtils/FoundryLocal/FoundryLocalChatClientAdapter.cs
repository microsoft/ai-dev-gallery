// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

/// <summary>
/// Adapter that wraps OpenAI ChatClient to work with Microsoft.Extensions.AI.IChatClient.
/// Uses synchronous streaming API (CompleteChatStreaming) to avoid SSE compatibility issues
/// with FoundryLocal's web service.
/// </summary>
internal class FoundryLocalChatClientAdapter : IChatClient
{
    private readonly OpenAI.Chat.ChatClient _chatClient;
    private readonly string _modelId;

    public FoundryLocalChatClientAdapter(string serviceUrl, string modelId)
    {
        Debug.WriteLine($"[FoundryLocalAdapter] Creating adapter for model {modelId} at {serviceUrl}");
        _modelId = modelId;
        
        var client = new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri($"{serviceUrl}/v1")
        });
        _chatClient = client.GetChatClient(modelId);
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
        Debug.WriteLine($"[FoundryLocalAdapter] GetStreamingResponseAsync called with {messageList.Count} messages");
        
        var openAIMessages = ConvertToOpenAIMessages(messageList);
        var chatOptions = ConvertToChatCompletionOptions(options);

        // Use synchronous streaming API which works reliably with FoundryLocal
        var completionUpdates = _chatClient.CompleteChatStreaming(openAIMessages, chatOptions, cancellationToken);
        
        Debug.WriteLine($"[FoundryLocalAdapter] Starting to enumerate streaming updates");
        
        // Yield to make this properly async
        await Task.Yield();
        
        string responseId = Guid.NewGuid().ToString("N");
        int updateCount = 0;
        foreach (var update in completionUpdates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            updateCount++;
            if (updateCount == 1)
            {
                Debug.WriteLine($"[FoundryLocalAdapter] Received first streaming update");
            }
            
            yield return ConvertToResponseUpdate(update, responseId);
        }
        
        Debug.WriteLine($"[FoundryLocalAdapter] Streaming completed. Total updates: {updateCount}");
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public void Dispose()
    {
        // ChatClient doesn't need disposal
    }

    private static List<OpenAI.Chat.ChatMessage> ConvertToOpenAIMessages(IList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        return messages.Select(m =>
        {
            OpenAI.Chat.ChatMessage chatMessage = m.Role.Value switch
            {
                "system" => new OpenAI.Chat.SystemChatMessage(m.Text ?? string.Empty),
                "user" => new OpenAI.Chat.UserChatMessage(m.Text ?? string.Empty),
                "assistant" => new OpenAI.Chat.AssistantChatMessage(m.Text ?? string.Empty),
                _ => new OpenAI.Chat.UserChatMessage(m.Text ?? string.Empty)
            };
            return chatMessage;
        }).ToList();
    }

    private static OpenAI.Chat.ChatCompletionOptions? ConvertToChatCompletionOptions(ChatOptions? options)
    {
        if (options == null)
        {
            return null;
        }

        var chatOptions = new OpenAI.Chat.ChatCompletionOptions
        {
            MaxOutputTokenCount = options.MaxOutputTokens,
            Temperature = options.Temperature,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty
        };

        if (options.StopSequences != null)
        {
            foreach (var stopSeq in options.StopSequences)
            {
                chatOptions.StopSequences.Add(stopSeq);
            }
        }

        return chatOptions;
    }

    private static ChatResponseUpdate ConvertToResponseUpdate(OpenAI.Chat.StreamingChatCompletionUpdate update, string responseId)
    {
        var text = update.ContentUpdate.Select(c => c.Text).FirstOrDefault();
        
        return new ChatResponseUpdate(ChatRole.Assistant, text ?? string.Empty)
        {
            ResponseId = responseId
        };
    }

    private static Microsoft.Extensions.AI.ChatFinishReason? ConvertFinishReason(OpenAI.Chat.ChatFinishReason? finishReason)
    {
        if (!finishReason.HasValue)
        {
            return null;
        }

        return finishReason.Value switch
        {
            OpenAI.Chat.ChatFinishReason.Stop => Microsoft.Extensions.AI.ChatFinishReason.Stop,
            OpenAI.Chat.ChatFinishReason.Length => Microsoft.Extensions.AI.ChatFinishReason.Length,
            OpenAI.Chat.ChatFinishReason.ContentFilter => Microsoft.Extensions.AI.ChatFinishReason.ContentFilter,
            OpenAI.Chat.ChatFinishReason.ToolCalls => Microsoft.Extensions.AI.ChatFinishReason.ToolCalls,
            _ => Microsoft.Extensions.AI.ChatFinishReason.Stop
        };
    }
}
