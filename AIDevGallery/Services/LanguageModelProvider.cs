// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Services;

/// <summary>
/// Provides a lazily-created singleton of LanguageModel for local HTTP usage.
/// </summary>
public sealed class LanguageModelProvider : IAsyncDisposable
{
    private LanguageModel? _instance;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private bool _disposed;

    public async Task<LanguageModel> GetAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LanguageModelProvider));

        if (_instance != null) return _instance;

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_instance != null) return _instance;

            // Unlock Limited Access Feature (LAF) and ensure readiness
            const string featureId = "com.microsoft.windows.ai.languagemodel";
            var token = LimitedAccessFeaturesHelper.GetAiLanguageModelToken();
            var publisher = LimitedAccessFeaturesHelper.GetAiLanguageModelPublisherId();

            var unlock = LimitedAccessFeatures.TryUnlockFeature(
                featureId,
                token,
                $"{publisher} has registered their use of {featureId} with Microsoft and agrees to the terms of use.");

            var ready = LanguageModel.GetReadyState();
            if (ready == AIFeatureReadyState.NotReady)
            {
                var ensure = await LanguageModel.EnsureReadyAsync();
                if (ensure.Status != AIFeatureReadyResultState.Success)
                {
                    throw new InvalidOperationException("LanguageModel is not ready.");
                }
            }
            else if (ready is not (AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady))
            {
                throw new InvalidOperationException($"LanguageModel not available: {ready}.");
            }

            _instance = await LanguageModel.CreateAsync();
            return _instance;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _mutex.WaitAsync().ConfigureAwait(false);
        try
        {
            _instance?.Dispose();
            _instance = null;
        }
        finally
        {
            _mutex.Release();
            _mutex.Dispose();
        }
    }
}


