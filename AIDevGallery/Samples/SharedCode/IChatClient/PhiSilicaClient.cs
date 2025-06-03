// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AIDevGallery.Samples.SharedCode;

internal class WCRException : Exception
{
    public WCRException(string message)
        : base(message)
    {
    }
}

internal class PhiSilicaClient : IChatClient
{
    // Search Options
    private const SeverityLevel DefaultInputModeration = SeverityLevel.Minimum;
    private const SeverityLevel DefaultOutputModeration = SeverityLevel.Minimum;
    private const int DefaultTopK = 50;
    private const float DefaultTopP = 0.9f;
    private const float DefaultTemperature = 1;

    private LanguageModel _languageModel;
    private LanguageModelContext? _languageModelContext;

    public ChatClientMetadata Metadata { get; }

    private PhiSilicaClient(LanguageModel languageModel)
    {
        _languageModel = languageModel;
        Metadata = new ChatClientMetadata("PhiSilica", new Uri($"file:///PhiSilica"));
    }

    private static ChatOptions GetDefaultChatOptions()
    {
        return new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "input_moderation", DefaultInputModeration },
                { "output_moderation", DefaultOutputModeration },
            },
            Temperature = DefaultTemperature,
            TopP = DefaultTopP,
            TopK = DefaultTopK,
        };
    }

    public static async Task<PhiSilicaClient?> CreateAsync(CancellationToken cancellationToken = default)
    {
        var readyState = LanguageModel.GetReadyState();

        if (readyState is AIFeatureReadyState.DisabledByUser or AIFeatureReadyState.NotSupportedOnCurrentSystem)
        {
            throw new WCRException("PhiSilica is not available: " +
                readyState switch
                {
                    AIFeatureReadyState.NotSupportedOnCurrentSystem => "Not supported",
                    AIFeatureReadyState.DisabledByUser => "Disabled by user",
                    _ => "Unknown reason"
                });
        }

        if (readyState is AIFeatureReadyState.NotReady)
        {
            var operation = await LanguageModel.EnsureReadyAsync();
            if (operation.Status != AIFeatureReadyResultState.Success)
            {
                throw new WCRException($"PhiSilica is not available");
            }
        }

        if (LanguageModel.GetReadyState() is not AIFeatureReadyState.Ready)
        {
            throw new WCRException("PhiSilica is not available");
        }

        var languageModel = await LanguageModel.CreateAsync();

        cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CA2000 // Dispose objects before losing scope
        var phiSilicaClient = new PhiSilicaClient(languageModel);
#pragma warning restore CA2000 // Dispose objects before losing scope

        return phiSilicaClient;
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(chatMessages, options, cancellationToken).ToChatResponseAsync(cancellationToken: cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = GetPrompt(chatMessages);

        string responseId = Guid.NewGuid().ToString("N");
        await foreach (var part in GenerateStreamResponseAsync(prompt, options, cancellationToken))
        {
            yield return new(ChatRole.Assistant, part)
            {
                ResponseId = responseId
            };
        }
    }

    private LanguageModelOptions GetModelOptions(ChatOptions? options)
    {
        if (options == null)
        {
            return new LanguageModelOptions();
        }

        var contentFilterOptions = new ContentFilterOptions();

        if (options?.AdditionalProperties?.TryGetValue("input_moderation", out SeverityLevel inputModeration) == true && inputModeration != SeverityLevel.Minimum)
        {
            contentFilterOptions.PromptMaxAllowedSeverityLevel = new TextContentFilterSeverity
            {
                Hate = inputModeration,
                Sexual = inputModeration,
                Violent = inputModeration,
                SelfHarm = inputModeration
            };
        }

        if (options?.AdditionalProperties?.TryGetValue("output_moderation", out SeverityLevel outputModeration) == true && outputModeration != SeverityLevel.Minimum)
        {
            contentFilterOptions.ResponseMaxAllowedSeverityLevel = new TextContentFilterSeverity
            {
                Hate = outputModeration,
                Sexual = outputModeration,
                Violent = outputModeration,
                SelfHarm = outputModeration
            };
        }

        var languageModelOptions = new LanguageModelOptions
        {
            Temperature = options?.Temperature ?? DefaultTemperature,
            TopK = (uint)(options?.TopK ?? DefaultTopK),
            TopP = (uint)(options?.TopP ?? DefaultTopP),
            ContentFilterOptions = contentFilterOptions
        };
        return languageModelOptions;
    }

    private string GetPrompt(IEnumerable<ChatMessage> history)
    {
        if (!history.Any())
        {
            return string.Empty;
        }

        string prompt = string.Empty;

        var firstMessage = history.FirstOrDefault();

        _languageModelContext = firstMessage?.Role == ChatRole.System ?
            _languageModel?.CreateContext(firstMessage.Text, new ContentFilterOptions()) :
            _languageModel?.CreateContext();

        for (var i = 0; i < history.Count(); i++)
        {
            var message = history.ElementAt(i);
            if (message.Role == ChatRole.System)
            {
                if (i > 0)
                {
                    throw new ArgumentException("Only first message can be a system message");
                }
            }
            else if (message.Role == ChatRole.User)
            {
                string msgText = message.Text ?? string.Empty;
                prompt += msgText;
            }
            else if (message.Role == ChatRole.Assistant)
            {
                prompt += message.Text;
            }
        }

        return prompt;
    }

    public void Dispose()
    {
        _languageModel.Dispose();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is not null ? null :
            serviceType == typeof(LanguageModel) ? _languageModel :
            serviceType == typeof(PhiSilicaClient) ? this :
            serviceType == typeof(IChatClient) ? this :
            serviceType == typeof(ChatClientMetadata) ? Metadata :
            serviceType == typeof(ChatOptions) ? GetDefaultChatOptions() :
            null;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        string currentResponse = string.Empty;
        using var newPartEvent = new ManualResetEventSlim(false);

        IAsyncOperationWithProgress<LanguageModelResponseResult, string>? progress;

        var modelOptions = GetModelOptions(options);
        if ((ulong)prompt.Length > _languageModel.GetUsablePromptLength(_languageModelContext, prompt))
        {
            yield return "\nPrompt larger than context";
            yield break;
        }

        progress = _languageModel.GenerateResponseAsync(_languageModelContext, prompt, modelOptions);

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
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

            if (newPartEvent.Wait(10, cancellationToken))
            {
                yield return currentResponse;
                newPartEvent.Reset();
            }
        }

        var response = await progress;

        yield return response?.Status switch
        {
            LanguageModelResponseStatus.BlockedByPolicy => "\nBlocked by policy",
            LanguageModelResponseStatus.PromptBlockedByContentModeration => "\nPrompt blocked by content moderation",
            LanguageModelResponseStatus.ResponseBlockedByContentModeration => "\nResponse blocked by content moderation",
            LanguageModelResponseStatus.PromptLargerThanContext => "\nPrompt larger than context",
            LanguageModelResponseStatus.Error => "\nError",
            _ => string.Empty,
        };
    }
}