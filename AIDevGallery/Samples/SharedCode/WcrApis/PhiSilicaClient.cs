// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.ContentModeration;
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

    // Search Options
    private const int DefaultTopK = 50;
    private const float DefaultTopP = 0.9f;
    private const float DefaultTemperature = 1;
    private const LanguageModelSkill DefaultLanguageModelSkill = LanguageModelSkill.General;
    private const SeverityLevel DefaultInputModeration = SeverityLevel.None;
    private const SeverityLevel DefaultOutputModeration = SeverityLevel.None;

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

    public static ChatOptions GetDefaultChatOptions()
    {
        return new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "skill", DefaultLanguageModelSkill },
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

    public Task<ChatResponse> GetResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(chatMessages, options, cancellationToken).ToChatResponseAsync(cancellationToken: cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_languageModel == null)
        {
            throw new InvalidOperationException("Language model is not loaded.");
        }

        var prompt = GetPrompt(chatMessages);

        await foreach (var part in GenerateStreamResponseAsync(prompt, options, cancellationToken))
        {
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Text = part,
            };
        }
    }

    private (LanguageModelOptions? ModelOptions, ContentFilterOptions? FilterOptions) GetModelOptions(ChatOptions options)
    {
        if (options == null)
        {
            return (null, null);
        }

        var languageModelOptions = new LanguageModelOptions
        {
            Skill = options.AdditionalProperties?.TryGetValue("skill", out LanguageModelSkill skill) == true ? skill : DefaultLanguageModelSkill,
            Temp = options.Temperature ?? DefaultTemperature,
            Top_k = (uint)(options.TopK ?? DefaultTopK),
            Top_p = (uint)(options.TopP ?? DefaultTopP),
        };

        var contentFilterOptions = new ContentFilterOptions();

        if (options?.AdditionalProperties?.TryGetValue("input_moderation", out SeverityLevel inputModeration) == true && inputModeration != SeverityLevel.None)
        {
            contentFilterOptions.PromptMinSeverityLevelToBlock = new TextContentFilterSeverity
            {
                HateContentSeverity = inputModeration,
                SexualContentSeverity = inputModeration,
                ViolentContentSeverity = inputModeration,
                SelfHarmContentSeverity = inputModeration
            };
        }

        if (options?.AdditionalProperties?.TryGetValue("output_moderation", out SeverityLevel outputModeration) == true && outputModeration != SeverityLevel.None)
        {
            contentFilterOptions.ResponseMinSeverityLevelToBlock = new TextContentFilterSeverity
            {
                HateContentSeverity = outputModeration,
                SexualContentSeverity = outputModeration,
                ViolentContentSeverity = outputModeration,
                SelfHarmContentSeverity = outputModeration
            };
        }

        return (languageModelOptions, contentFilterOptions);
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
        try
        {
            return LanguageModel.IsAvailable();
        }
        catch
        {
            return false;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable())
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
            IAsyncOperationWithProgress<LanguageModelResponse, string>? progress;
            if (options == null)
            {
                progress = _languageModel.GenerateResponseWithProgressAsync(prompt);
            }
            else
            {
                var (modelOptions, filterOptions) = GetModelOptions(options);
                progress = _languageModel.GenerateResponseWithProgressAsync(modelOptions, prompt, filterOptions);
            }

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
                LanguageModelResponseStatus.PromptBlockedByPolicy => "\nPrompt blocked by policy",
                LanguageModelResponseStatus.ResponseBlockedByPolicy => "\nResponse blocked by policy",
                _ => string.Empty,
            };
        }
        else
        {
            yield return "Prompt is too large for this model. Please submit a smaller prompt";
        }
    }
}