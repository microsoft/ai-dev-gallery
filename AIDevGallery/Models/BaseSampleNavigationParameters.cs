// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models;

internal abstract class BaseSampleNavigationParameters(TaskCompletionSource sampleLoadedCompletionSource, CancellationToken loadingCanceledToken)
{
    public CancellationToken CancellationToken { get; private set; } = loadingCanceledToken;
    public TaskCompletionSource SampleLoadedCompletionSource { get; set; } = sampleLoadedCompletionSource;

    protected abstract string ChatClientModelPath { get; }
    protected abstract HardwareAccelerator ChatClientHardwareAccelerator { get; }
    protected abstract LlmPromptTemplate? ChatClientPromptTemplate { get; }

    public abstract string PreferedEP { get; }

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
        else if (ExternalModelHelper.IsUrlFromExternalProvider(ChatClientModelPath))
        {
            return ExternalModelHelper.GetIChatClient(ChatClientModelPath);
        }

        return await OnnxRuntimeGenAIChatClientFactory.CreateAsync(
            ChatClientModelPath,
            ChatClientPromptTemplate,
            ChatClientHardwareAccelerator == HardwareAccelerator.QNN ? "qnn" : null,
            CancellationToken).ConfigureAwait(false);
    }

    internal abstract void SendSampleInteractionEvent(string? customInfo = null);
}