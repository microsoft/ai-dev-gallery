// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal static class ChatClientTextOutput
{
    // This is the provider, not the model/alias selected within ONNX, Foundry Local, etc.
    private static bool NeedsWatermark(IChatClient client) =>
        client.GetService<ChatClientMetadata>()?.ProviderName is not ("AionInstruct" or "PhiSilica");

    public static WatermarkedText? CreateDisplayWatermarker(this IChatClient client) =>
        NeedsWatermark(client) ? new() : null;

    public static string GetDisplayText(this IChatClient client, string rawText) =>
        NeedsWatermark(client) ? TextWatermarker.Apply(rawText) : rawText;

    /// <summary>Publishes display deltas without modifying messages, options, or provider updates.</summary>
    /// <returns>A task that completes after the final display delta has been published.</returns>
    public static async Task StreamDisplayTextAsync(
        this IChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Action<string> publish,
        Action? onModelUpdate = null,
        CancellationToken cancellationToken = default)
    {
        // A caller asking for structured output needs the literal provider response.
        var marker = options?.ResponseFormat is not ChatResponseFormatJson && NeedsWatermark(client) ? new TextWatermarker() : null;
        try
        {
            await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                onModelUpdate?.Invoke();
                var text = update.Text;
                publish(marker?.Append(text) ?? text);
            }
        }
        finally
        {
            if (marker?.Finish() is { Length: > 0 } tail)
            {
                publish(tail);
            }
        }
    }
}