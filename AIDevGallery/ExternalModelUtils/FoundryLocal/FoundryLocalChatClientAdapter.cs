// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

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
        Debug.WriteLine($"[FoundryLocalAdapter] Creating adapter for model {modelId}");
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
        var messageList = chatMessages.ToList();
        Debug.WriteLine($"[FoundryLocalAdapter] GetStreamingResponseAsync called with {messageList.Count} messages");
        
        var openAIMessages = ConvertToFoundryMessages(messageList);
        
        Debug.WriteLine($"[FoundryLocalAdapter] Converted {openAIMessages.Count} messages:");
        foreach (var msg in openAIMessages)
        {
            var contentPreview = msg.Content != null ? msg.Content.Substring(0, Math.Min(50, msg.Content.Length)) : "(null)";
            Debug.WriteLine($"  Role: {msg.Role}, Content: {contentPreview}...");
        }

        // Use FoundryLocal SDK's native streaming API - no HTTP/SSE issues!
        var streamingResponse = _chatClient.CompleteChatStreamingAsync(openAIMessages, cancellationToken);
        
        Debug.WriteLine($"[FoundryLocalAdapter] Starting to enumerate streaming updates");
        
        string responseId = Guid.NewGuid().ToString("N");
        int updateCount = 0;
        await foreach (var chunk in streamingResponse)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            updateCount++;
            
            Debug.WriteLine($"[FoundryLocalAdapter] Chunk #{updateCount}: Choices count = {chunk.Choices?.Count ?? 0}");
            
            // Access choices directly like official sample does
            if (chunk.Choices != null && chunk.Choices.Count > 0)
            {
                var choice = chunk.Choices[0];
                var content = choice.Message?.Content;
                
                Debug.WriteLine($"  Choice[0]: Message={choice.Message != null}, Content length={content?.Length ?? 0}");
                
                if (!string.IsNullOrEmpty(content))
                {
                    Debug.WriteLine($"  Yielding content: {content}");
                    yield return new ChatResponseUpdate(ChatRole.Assistant, content)
                    {
                        ResponseId = responseId
                    };
                }
            }
            else
            {
                Debug.WriteLine($"  No choices in chunk");
            }
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

    private static List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> ConvertToFoundryMessages(IList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        return messages.Select(m => new Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage
        {
            Role = m.Role.Value,
            Content = m.Text ?? string.Empty
        }).ToList();
    }
}
