// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal static class OnnxRuntimeGenAIChatClientFactory
{
    private const string TEMPLATE_PLACEHOLDER = "{{CONTENT}}";

    private const int DefaultMaxLength = 1024;

    private static readonly SemaphoreSlim _createSemaphore = new(1, 1);
    private static OgaHandle? _ogaHandle;

    public static async Task<IChatClient?> CreateAsync(string modelDir, LlmPromptTemplate? template = null, CancellationToken cancellationToken = default)
    {
        var options = new OnnxRuntimeGenAIChatClientOptions
        {
            StopSequences = template?.Stop ?? Array.Empty<string>(),
            PromptFormatter = (chatMessages, chatOptions) => GetPrompt(template, chatMessages, chatOptions)
        };

        var lockAcquired = false;
        OnnxRuntimeGenAIChatClient? model = null;
        try
        {
            // ensure we call CreateAsync one at a time to avoid fun issues
            await _createSemaphore.WaitAsync(cancellationToken);
            lockAcquired = true;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    model = new OnnxRuntimeGenAIChatClient(modelDir, options);
                    cancellationToken.ThrowIfCancellationRequested();
                },
                cancellationToken);
        }
        catch
        {
            model?.Dispose();
            return null;
        }
        finally
        {
            if (lockAcquired)
            {
                _createSemaphore.Release();
            }
        }

        return (model
            ?.AsBuilder())
            ?.ConfigureOptions(o =>
            {
                o.AdditionalProperties ??= [];
                o.AdditionalProperties["max_length"] = DefaultMaxLength;
            })
            ?.Build();
    }

    public static void InitializeGenAI()
    {
        _ogaHandle = new OgaHandle();
    }

    private static string GetPrompt(LlmPromptTemplate? template, IEnumerable<ChatMessage> history, ChatOptions? chatOptions)
    {
        if (!history.Any())
        {
            return string.Empty;
        }

        if (template == null)
        {
            return string.Join(". ", history);
        }

        StringBuilder prompt = new();

        string systemMsgWithoutSystemTemplate = string.Empty;

        for (var i = 0; i < history.Count(); i++)
        {
            var message = history.ElementAt(i);
            if (message.Role == ChatRole.System)
            {
                // ignore system prompts that aren't at the beginning
                if (i == 0)
                {
                    if (string.IsNullOrWhiteSpace(template.System))
                    {
                        systemMsgWithoutSystemTemplate = message.Text ?? string.Empty;
                    }
                    else
                    {
                        prompt.Append(template.System.Replace(TEMPLATE_PLACEHOLDER, message.Text));
                    }
                }
            }
            else if (message.Role == ChatRole.User)
            {
                string msgText = message.Text ?? string.Empty;
                if (i == 1 && !string.IsNullOrWhiteSpace(systemMsgWithoutSystemTemplate))
                {
                    msgText = $"{systemMsgWithoutSystemTemplate} {msgText}";
                }

                prompt.Append(string.IsNullOrWhiteSpace(template.User) ?
                    msgText :
                    template.User.Replace(TEMPLATE_PLACEHOLDER, msgText));
            }
            else if (message.Role == ChatRole.Assistant)
            {
                prompt.Append(string.IsNullOrWhiteSpace(template.Assistant) ?
                    message.Text :
                    template.Assistant.Replace(TEMPLATE_PLACEHOLDER, message.Text));
            }
        }

        if (!string.IsNullOrWhiteSpace(template.Assistant))
        {
            var substringIndex = template.Assistant.IndexOf(TEMPLATE_PLACEHOLDER, StringComparison.InvariantCulture);
            prompt.Append(template.Assistant[..substringIndex]);
        }

        return prompt.ToString();
    }
}