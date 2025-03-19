// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models;

internal abstract class BaseSampleNavigationParameters(TaskCompletionSource sampleLoadedCompletionSource, CancellationToken loadingCanceledToken)
{
    public CancellationToken CancellationToken { get; private set; } = loadingCanceledToken;
    public TaskCompletionSource SampleLoadedCompletionSource { get; set; } = sampleLoadedCompletionSource;

    protected abstract string ChatClientModelPath { get; }
    protected abstract LlmPromptTemplate? ChatClientPromptTemplate { get; }

    public void NotifyCompletion()
    {
        SampleLoadedCompletionSource.SetResult();
    }

    public async Task<IChatClient?> GetIChatClientAsync()
    {
        if (ChatClientModelPath == $"file://{ModelType.PhiSilica}")
        {
            return await PhiSilicaClient.CreateAsync(CancellationToken).ConfigureAwait(false);
        }
        else if (ChatClientModelPath.StartsWith("ollama", System.StringComparison.InvariantCultureIgnoreCase))
        {
            var modelId = ChatClientModelPath.Split('/').LastOrDefault();
            return new OllamaChatClient(Ollama.GetOllamaUrl(), modelId);
        }

        return await GenAIModel.CreateAsync(ChatClientModelPath, ChatClientPromptTemplate, CancellationToken).ConfigureAwait(false);
    }

    internal abstract void SendSampleInteractionEvent(string? customInfo = null);
}