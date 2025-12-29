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
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] DownloadModel called for: {catalogModel.Alias}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken.CanBeCanceled: {cancellationToken.CanBeCanceled}");
        
        if (_catalog == null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] ERROR: Catalog not initialized");
            return new FoundryDownloadResult(false, "Catalog not initialized");
        }

        var startTime = DateTime.Now;
        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Getting model from catalog: {catalogModel.Alias}");
            var model = await _catalog.GetModelAsync(catalogModel.Alias);
            if (model == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] ERROR: Model not found in catalog");
                return new FoundryDownloadResult(false, "Model not found in catalog");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model found, checking if cached");
            var isCached = await model.IsCachedAsync();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model IsCached: {isCached}");
            
            if (isCached)
            {
                await EnsureModelLoadedAsync(catalogModel.Alias, cancellationToken);
                return new FoundryDownloadResult(true, "Model already cached and loaded");
            }

            // Key Perf Log
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Starting download for model: {catalogModel.Alias}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model size: {catalogModel.FileSizeMb} MB");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken status before download - IsCancellationRequested: {cancellationToken.IsCancellationRequested}");
            
            var lastReportedProgress = -1f;
            await model.DownloadAsync(
                progressPercent =>
                {
                    // Log every 10% progress
                    if (progressPercent - lastReportedProgress >= 10)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Download progress: {progressPercent:F1}% (CancelRequested: {cancellationToken.IsCancellationRequested})");
                        lastReportedProgress = progressPercent;
                    }
                    progress?.Report(progressPercent / 100f);
                },
                cancellationToken);

            // Key Perf Log
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Download completed for model: {catalogModel.Alias}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken status after download - IsCancellationRequested: {cancellationToken.IsCancellationRequested}");

            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelDownload", catalogModel.Alias, duration);

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Attempting to load model after download");
            try
            {
                await EnsureModelLoadedAsync(catalogModel.Alias, cancellationToken);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model loaded successfully after download");
                return new FoundryDownloadResult(true, null);
            }
            catch (Exception ex)
            {
                var warningMsg = ex.Message.Split('\n')[0];
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Load warning after download: {warningMsg}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Full exception message: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Inner exception: {ex.InnerException.Message}");
                }
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDownload", "LoadWarning", catalogModel.Alias, ex.Message);
                return new FoundryDownloadResult(true, warningMsg);
            }
        }
        catch (OperationCanceledException e)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Download was canceled: {catalogModel.Alias}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Exception message: {e.Message}");
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelDownload", "Canceled", catalogModel.Alias, e.Message);
            return new FoundryDownloadResult(false, "Download was canceled");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Download exception for model: {catalogModel.Alias}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Exception type: {e.GetType().Name}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Exception message: {e.Message}");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Stack trace: {e.StackTrace}");
            if (e.InnerException != null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Inner exception: {e.InnerException.Message}");
            }
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
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] EnsureModelLoadedAsync called for: {alias}");
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");
        
        if (_loadedModels.ContainsKey(alias))
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model already loaded: {alias}");
            return;
        }

        var startTime = DateTime.Now;
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Waiting for load lock...");
        await _loadLock.WaitAsync(cancellationToken);
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Load lock acquired");
        try
        {
            // Double-check inside lock to ensure thread safety
            if (_loadedModels.ContainsKey(alias))
            {
                return;
            }

            if (_catalog == null || _manager == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] ERROR: Client not initialized");
                throw new InvalidOperationException("FoundryLocal client not initialized");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Getting model from catalog for loading: {alias}");
            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] ERROR: Model not found in catalog");
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            var isCached = await model.IsCachedAsync();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model IsCached check: {isCached}");
            
            if (!isCached)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] ERROR: Model not cached");
                throw new InvalidOperationException($"Model with alias '{alias}' is not cached. Please download it first.");
            }

            var isLoaded = await model.IsLoadedAsync();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model IsLoaded check: {isLoaded}");
            
            if (!isLoaded)
            {
                // Key Perf Log
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Loading model: {alias} ({model.SelectedVariant.Info.Id})");
                await model.LoadAsync(cancellationToken);

                // Key Perf Log
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model loaded: {alias}");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Caching loaded model");
            _loadedModels[alias] = model;
            _modelMaxOutputTokens[alias] = (int?)model.SelectedVariant.Info.MaxOutputTokens;

            // Pre-create and cache the chat client to avoid sync-over-async in GetChatClient
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Creating chat client");
            var chatClient = await model.GetChatClientAsync();
            _chatClients[alias] = chatClient;
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Chat client created and cached");

            var duration = (DateTime.Now - startTime).TotalSeconds;
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FoundryLocal] Model load completed in {duration:F2} seconds");
            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelLoad", alias, duration);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
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
                return false;
            }

            var alias = variant.Alias;

            if (await variant.IsLoadedAsync())
            {
                await variant.UnloadAsync();
            }

            if (await variant.IsCachedAsync())
            {
                await variant.RemoveFromCacheAsync();
            }

            if (!string.IsNullOrEmpty(alias))
            {
                _loadedModels.Remove(alias);
                _modelMaxOutputTokens.Remove(alias);
                _chatClients.Remove(alias);
            }

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
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelUnload", "Exception", alias, ex.Message);
            }
        }

        _loadedModels.Clear();
        _modelMaxOutputTokens.Clear();
        _chatClients.Clear();
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