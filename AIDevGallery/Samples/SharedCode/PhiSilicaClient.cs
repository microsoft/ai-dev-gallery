// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.Generative;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AIDevGallery.Samples.SharedCode;

internal class PhiSilicaClient : IChatClient
{
    private const string TEMPLATE_PLACEHOLDER = "{{CONTENT}}";

    private readonly LlmPromptTemplate _promptTemplate;

    private LanguageModel? _languageModel;

    public ChatClientMetadata Metadata { get; }

    private PhiSilicaClient()
    {
        Metadata = new ChatClientMetadata("PhiSilica", new Uri($"file:///PhiSilica"));
        _promptTemplate = new LlmPromptTemplate
        {
            System = "<|system|>\n{{CONTENT}}<|end|>\n",
            User = "<|user|>\n{{CONTENT}}<|end|>\n",
            Assistant = "<|assistant|>\n{{CONTENT}}<|end|>\n",
            Stop = ["<|system|>", "<|user|>", "<|assistant|>", "<|end|>"]
        };
    }

    public static async Task<PhiSilicaClient?> CreateAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var phiSilicaClient = new PhiSilicaClient();
#pragma warning restore CA2000 // Dispose objects before losing scope

        try
        {
            await phiSilicaClient.InitializeAsync(cancellationToken);
        }
        catch
        {
            return null;
        }

        return phiSilicaClient;
    }

    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_languageModel == null)
        {
            throw new InvalidOperationException("Language model is not loaded.");
        }

        var prompt = GetPrompt(chatMessages);

        var response = await _languageModel.GenerateResponseWithProgressAsync(prompt).AsTask(cancellationToken);
        if (response.Status == LanguageModelResponseStatus.Complete)
        {
            return new ChatCompletion(new ChatMessage(ChatRole.Assistant, response.Response));
        }

        return new ChatCompletion(new ChatMessage(ChatRole.Assistant, string.Empty));
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_languageModel == null)
        {
            throw new InvalidOperationException("Language model is not loaded.");
        }

        var prompt = GetPrompt(chatMessages);

        await foreach (var part in GenerateStreamResponseAsync(prompt, options, cancellationToken))
        {
            yield return new StreamingChatCompletionUpdate
            {
                Role = ChatRole.Assistant,
                Text = part,
            };
        }
    }

    private string GetPrompt(IEnumerable<ChatMessage> history)
    {
        if (!history.Any())
        {
            return string.Empty;
        }

        string prompt = string.Empty;

        string systemMsgWithoutSystemTemplate = string.Empty;

        for (var i = 0; i < history.Count(); i++)
        {
            var message = history.ElementAt(i);
            if (message.Role == ChatRole.System)
            {
                if (i > 0)
                {
                    throw new ArgumentException("Only first message can be a system message");
                }

                if (string.IsNullOrWhiteSpace(_promptTemplate.System))
                {
                    systemMsgWithoutSystemTemplate = message.Text ?? string.Empty;
                }
                else
                {
                    prompt += _promptTemplate.System.Replace(TEMPLATE_PLACEHOLDER, message.Text);
                }
            }
            else if (message.Role == ChatRole.User)
            {
                string msgText = message.Text ?? string.Empty;
                if (i == 1 && !string.IsNullOrWhiteSpace(systemMsgWithoutSystemTemplate))
                {
                    msgText = $"{systemMsgWithoutSystemTemplate} {msgText}";
                }

                if (string.IsNullOrWhiteSpace(_promptTemplate.User))
                {
                    prompt += msgText;
                }
                else
                {
                    prompt += _promptTemplate.User.Replace(TEMPLATE_PLACEHOLDER, msgText);
                }
            }
            else if (message.Role == ChatRole.Assistant)
            {
                if (string.IsNullOrWhiteSpace(_promptTemplate.Assistant))
                {
                    prompt += message.Text;
                }
                else
                {
                    prompt += _promptTemplate.Assistant.Replace(TEMPLATE_PLACEHOLDER, message.Text);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_promptTemplate.Assistant))
        {
            var substringIndex = _promptTemplate.Assistant.IndexOf(TEMPLATE_PLACEHOLDER, StringComparison.InvariantCulture);
            prompt += _promptTemplate.Assistant[..substringIndex];
        }

        return prompt;
    }

    public void Dispose()
    {
        _languageModel?.Dispose();
        _languageModel = null;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return
            serviceKey is not null ? null :
            _languageModel is not null && serviceType?.IsInstanceOfType(_languageModel) is true ? _languageModel :
            serviceType?.IsInstanceOfType(this) is true ? this :
            null;
    }

    public static bool IsAvailable()
    {
        return LanguageModel.IsAvailable();
    }

    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LanguageModel.IsAvailable())
        {
            await LanguageModel.MakeAvailableAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();

        _languageModel = await LanguageModel.CreateAsync();
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (_languageModel == null)
        {
            throw new InvalidOperationException("Language model is not loaded.");
        }

        string currentResponse = string.Empty;
        using var newPartEvent = new ManualResetEventSlim(false);

        if (!_languageModel.IsPromptLargerThanContext(prompt))
        {
            var progress = _languageModel.GenerateResponseWithProgressAsync(prompt);
            progress.Progress = (result, value) =>
            {
                currentResponse = value;
                newPartEvent.Set();
                if (cancellationToken.IsCancellationRequested)
                {
                    progress.Cancel();
                }
            };

            while (progress.Status != AsyncStatus.Completed)
            {
                await Task.Delay(0, cancellationToken).ConfigureAwait(false);

                if (newPartEvent.Wait(10, cancellationToken))
                {
                    yield return currentResponse;
                    newPartEvent.Reset();
                }
            }

            await progress;
        }
        else
        {
            yield return "Prompt is too large for this model. Please submit a smaller prompt";
        }
    }
}