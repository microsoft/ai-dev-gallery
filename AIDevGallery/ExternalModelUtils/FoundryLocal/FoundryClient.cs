// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class FoundryClient : IDisposable
{
    private readonly Dictionary<string, IModel> _loadedModels = new();
    private readonly Dictionary<string, int?> _modelMaxOutputTokens = new();
    private readonly Dictionary<string, OpenAIChatClient> _chatClients = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private FoundryLocalManager? _manager;
    private ICatalog? _catalog;
    private bool _disposed;

    /// <summary>
    /// Gets the underlying catalog for direct access to model queries.
    /// Provider layer should use this to implement business logic.
    /// </summary>
    public ICatalog? Catalog => _catalog;

    public static async Task<FoundryClient?> CreateAsync()
    {
        try
        {
            var config = new Configuration
            {
                AppName = "AIDevGallery",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                ModelCacheDir = App.ModelCache.GetCacheFolder()
            };

            await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);

            if (!FoundryLocalManager.IsInitialized)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ClientInitialization", "ManagerCreation", "N/A", "FoundryLocalManager failed to initialize");
                return null;
            }

            var client = new FoundryClient
            {
                _manager = FoundryLocalManager.Instance
            };

            await client._manager.EnsureEpsDownloadedAsync();
            client._catalog = await client._manager.GetCatalogAsync();

            Telemetry.Events.FoundryLocalOperationEvent.Log("ClientInitialization", "N/A");
            return client;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ClientInitialization", "Exception", "N/A", ex.Message);
            return null;
        }
    }

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel catalogModel, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_catalog == null)
        {
            return new FoundryDownloadResult(false, "Catalog not initialized");
        }

        var startTime = DateTime.Now;
        try
        {
            var model = await _catalog.GetModelAsync(catalogModel.Alias);
            if (model == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDownload", "ModelNotFound", catalogModel.Alias, "Model not found in catalog");
                return new FoundryDownloadResult(false, "Model not found in catalog");
            }

            if (await model.IsCachedAsync())
            {
                await EnsureModelLoadedAsync(catalogModel.Alias, cancellationToken);
                return new FoundryDownloadResult(true, "Model already cached and loaded");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Starting download for model: {catalogModel.Alias}");
            await model.DownloadAsync(
                progressPercent => progress?.Report(progressPercent / 100f),
                cancellationToken);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Download completed for model: {catalogModel.Alias}");

            await EnsureModelLoadedAsync(catalogModel.Alias, cancellationToken);

            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelDownload", catalogModel.Alias, duration);

            return new FoundryDownloadResult(true, null);
        }
        catch (Exception e)
        {
            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDownload", "Exception", catalogModel.Alias, e.Message);
            return new FoundryDownloadResult(false, e.Message);
        }
    }

    /// <summary>
    /// Ensures a model is loaded into memory for use.
    /// Should be called after download or when first accessing a cached model.
    /// Thread-safe: multiple concurrent calls for the same alias will only load once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnsureModelLoadedAsync(string alias, CancellationToken cancellationToken = default)
    {
        if (_loadedModels.ContainsKey(alias))
        {
            return;
        }

        var startTime = DateTime.Now;
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check inside lock to ensure thread safety
            if (_loadedModels.ContainsKey(alias))
            {
                return;
            }

            if (_catalog == null || _manager == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelLoad", "ClientNotInitialized", alias, "FoundryLocal client not initialized");
                throw new InvalidOperationException("FoundryLocal client not initialized");
            }

            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelLoad", "ModelNotFound", alias, $"Model with alias '{alias}' not found in catalog");
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            if (!await model.IsCachedAsync())
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelLoad", "ModelNotCached", alias, $"Model with alias '{alias}' is not cached");
                throw new InvalidOperationException($"Model with alias '{alias}' is not cached. Please download it first.");
            }

            if (!await model.IsLoadedAsync())
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Loading model: {alias} ({model.SelectedVariant.Info.Id})");
                await model.LoadAsync(cancellationToken);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model loaded: {alias}");
            }

            _loadedModels[alias] = model;
            _modelMaxOutputTokens[alias] = (int?)model.SelectedVariant.Info.MaxOutputTokens;

            // Pre-create and cache the chat client to avoid sync-over-async in GetChatClient
            var chatClient = await model.GetChatClientAsync();
            _chatClients[alias] = chatClient;

            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelLoad", alias, duration);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelLoad", "Exception", alias, ex.Message);
            throw;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public IModel? GetLoadedModel(string alias) =>
        _loadedModels.GetValueOrDefault(alias);

    public OpenAIChatClient? GetChatClient(string alias) =>
        _chatClients.GetValueOrDefault(alias);

    public int? GetModelMaxOutputTokens(string alias) =>
        _modelMaxOutputTokens.GetValueOrDefault(alias);

    public async Task<bool> DeleteModelAsync(string modelId)
    {
        if (_catalog == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDelete", "CatalogNotInitialized", modelId, "Catalog not initialized");
            return false;
        }

        try
        {
            var variant = await _catalog.GetModelVariantAsync(modelId);
            if (variant == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDelete", "VariantNotFound", modelId, "Model variant not found");
                return false;
            }

            var alias = variant.Alias;

            if (await variant.IsLoadedAsync())
            {
                await variant.UnloadAsync();
            }

            if (await variant.IsCachedAsync())
            {
                try
                {
                    await variant.RemoveFromCacheAsync();
                }
                catch (FoundryLocalException ex) when (ex.InnerException is System.IO.DirectoryNotFoundException)
                {
                    Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDelete", "CacheDirectoryNotFound", modelId, "Cache directory already removed or corrupted");
                }
            }

            if (!string.IsNullOrEmpty(alias))
            {
                _loadedModels.Remove(alias);
                _modelMaxOutputTokens.Remove(alias);
                _chatClients.Remove(alias);
            }

            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelDelete", alias ?? modelId);
            return true;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDelete", "Exception", modelId, ex.Message);
            return false;
        }
    }

    public async Task UnloadAllModelsAsync()
    {
        var modelCount = _loadedModels.Count;

        // Unload all loaded models before clearing
        foreach (var (alias, model) in _loadedModels)
        {
            try
            {
                if (await model.IsLoadedAsync())
                {
                    await model.UnloadAsync();
                }
            }
            catch (Exception ex)
            {
                // Log but continue unloading other models
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelUnload", "Exception", alias, ex.Message);
            }
        }

        _loadedModels.Clear();
        _modelMaxOutputTokens.Clear();
        _chatClients.Clear();

        if (modelCount > 0)
        {
            Telemetry.Events.FoundryLocalOperationEvent.Log("UnloadAllModels", $"{modelCount} models");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _loadedModels.Clear();
        _modelMaxOutputTokens.Clear();
        _chatClients.Clear();
        _loadLock.Dispose();
        _disposed = true;
    }
}