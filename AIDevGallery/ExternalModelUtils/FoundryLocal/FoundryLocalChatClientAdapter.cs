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
        
        // Set default settings if not already set
        if (_chatClient.Settings.MaxTokens == null)
        {
            Debug.WriteLine($"[FoundryLocalAdapter] Setting default MaxTokens = 512");
            _chatClient.Settings.MaxTokens = 512;
        }
        if (_chatClient.Settings.Temperature == null)
        {
            Debug.WriteLine($"[FoundryLocalAdapter] Setting default Temperature = 0.7");
            _chatClient.Settings.Temperature = 0.7f;
        }
        
        Debug.WriteLine($"[FoundryLocalAdapter] Final Settings: MaxTokens={_chatClient.Settings.MaxTokens}, Temperature={_chatClient.Settings.Temperature}");
    }

    /// <summary>
    /// Direct test method to verify CompleteChatStreamingAsync works
    /// Exactly mimics the official sample code
    /// </summary>
    public async Task<string> TestDirectStreamingAsync(CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TEST] ===== DIRECT STREAMING TEST =====");
        Debug.WriteLine($"[TEST] ChatClient type: {_chatClient.GetType().FullName}");
        Debug.WriteLine($"[TEST] ChatClient.Settings: {_chatClient.Settings != null}");
        if (_chatClient.Settings != null)
        {
            Debug.WriteLine($"[TEST]   Settings.MaxTokens: {_chatClient.Settings.MaxTokens}");
            Debug.WriteLine($"[TEST]   Settings.Temperature: {_chatClient.Settings.Temperature}");
        }
        
        // EXACTLY like official sample
        List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> messages = new()
        {
            new Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage { Role = "user", Content = "Why is the sky blue?" }
        };

        Debug.WriteLine($"[TEST] Created message list with {messages.Count} messages");
        Debug.WriteLine($"[TEST] Message[0]: Role='{messages[0].Role}', Content='{messages[0].Content}'");
        Debug.WriteLine($"[TEST] Calling CompleteChatStreamingAsync...");
        
        var streamingResponse = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken);
        Debug.WriteLine($"[TEST] StreamingResponse type: {streamingResponse.GetType().FullName}");
        
        int chunkCount = 0;
        var fullResponse = new System.Text.StringBuilder();
        
        try
        {
            Debug.WriteLine($"[TEST] Starting await foreach loop...");
            await foreach (var chunk in streamingResponse)
            {
                chunkCount++;
                Debug.WriteLine($"[TEST] >>> Chunk #{chunkCount} <<<");
                
                // Try to access exactly like official sample
                try
                {
                    var content = chunk.Choices[0].Message.Content;
                    Debug.WriteLine($"[TEST]   Official way works! Content: '{content}'");
                    fullResponse.Append(content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TEST]   Official way failed: {ex.Message}");
                    Debug.WriteLine($"[TEST]   Chunk.Choices null? {chunk.Choices == null}");
                    Debug.WriteLine($"[TEST]   Chunk.Choices.Count: {chunk.Choices?.Count ?? -1}");
                    if (chunk.Choices != null && chunk.Choices.Count > 0)
                    {
                        var choice = chunk.Choices[0];
                        Debug.WriteLine($"[TEST]   Choice[0].Message null? {choice.Message == null}");
                        Debug.WriteLine($"[TEST]   Choice[0].Delta null? {choice.Delta == null}");
                        var altContent = choice.Message?.Content ?? choice.Delta?.Content;
                        if (altContent != null)
                        {
                            Debug.WriteLine($"[TEST]   Alternative content: '{altContent}'");
                            fullResponse.Append(altContent);
                        }
                    }
                }
            }
            Debug.WriteLine($"[TEST] Exited await foreach loop");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TEST] EXCEPTION in foreach: {ex.GetType().FullName}");
            Debug.WriteLine($"[TEST]   Message: {ex.Message}");
            Debug.WriteLine($"[TEST]   Stack: {ex.StackTrace}");
            throw;
        }
        
        Debug.WriteLine($"[TEST] ===== TEST COMPLETE =====");
        Debug.WriteLine($"[TEST] Total chunks: {chunkCount}");
        Debug.WriteLine($"[TEST] Response length: {fullResponse.Length}");
        Debug.WriteLine($"[TEST] Response: '{fullResponse}'");
        return fullResponse.ToString();
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
