// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class FoundryClient : IDisposable
{
    private readonly Dictionary<string, IModel> _preparedModels = new();
    private readonly Dictionary<string, int?> _modelMaxOutputTokens = new();
    private readonly SemaphoreSlim _prepareLock = new(1, 1);
    private FoundryLocalManager? _manager;
    private ICatalog? _catalog;
    private bool _disposed;

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

    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalog == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ListCatalogModels", "CatalogNotInitialized", "N/A", "Catalog not initialized");
            return [];
        }

        var models = await _catalog.ListModelsAsync();
        return models.Select(model =>
        {
            var variant = model.SelectedVariant;
            var info = variant.Info;
            return new FoundryCatalogModel
            {
                Name = info.Name,
                DisplayName = info.DisplayName ?? info.Name,
                Alias = model.Alias,
                FileSizeMb = info.FileSizeMb ?? 0,
                License = info.License ?? string.Empty,
                ModelId = variant.Id,
                Runtime = info.Runtime,
                Task = info.Task
            };
        }).ToList();
    }

    public async Task<List<FoundryCachedModelInfo>> ListCachedModels()
    {
        if (_catalog == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ListCachedModels", "CatalogNotInitialized", "N/A", "Catalog not initialized");
            return [];
        }

        return (await _catalog.GetCachedModelsAsync())
            .Select(variant => new FoundryCachedModelInfo(variant.Info.Name, variant.Alias))
            .ToList();
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
                await PrepareModelAsync(catalogModel.Alias, cancellationToken);
                return new FoundryDownloadResult(true, "Model already downloaded");
            }

            await model.DownloadAsync(
                progressPercent => progress?.Report(progressPercent / 100f),
                cancellationToken);

            await PrepareModelAsync(catalogModel.Alias, cancellationToken);

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
    /// Prepares a model for use by loading it.
    /// Should be called after download or when first accessing a cached model.
    /// Thread-safe: multiple concurrent calls for the same alias will only prepare once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PrepareModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        if (_preparedModels.ContainsKey(alias))
        {
            return;
        }

        var startTime = DateTime.Now;
        await _prepareLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check inside lock to ensure thread safety
            if (_preparedModels.ContainsKey(alias))
            {
                return;
            }

            if (_catalog == null || _manager == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelPrepare", "ClientNotInitialized", alias, "Foundry Local client not initialized");
                throw new InvalidOperationException("Foundry Local client not initialized");
            }

            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelPrepare", "ModelNotFound", alias, $"Model with alias '{alias}' not found in catalog");
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            if (!await model.IsCachedAsync())
            {
                Telemetry.Events.FoundryLocalErrorEvent.Log("ModelPrepare", "ModelNotCached", alias, $"Model with alias '{alias}' is not cached");
                throw new InvalidOperationException($"Model with alias '{alias}' is not cached. Please download it first.");
            }

            if (!await model.IsLoadedAsync())
            {
                await model.LoadAsync(cancellationToken);
            }

            _preparedModels[alias] = model;
            _modelMaxOutputTokens[alias] = (int?)model.SelectedVariant.Info.MaxOutputTokens;

            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalOperationEvent.Log("ModelPrepare", alias, duration);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var duration = (DateTime.Now - startTime).TotalSeconds;
            Telemetry.Events.FoundryLocalErrorEvent.Log("ModelPrepare", "Exception", alias, ex.Message);
            throw;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    public IModel? GetPreparedModel(string alias) =>
        _preparedModels.GetValueOrDefault(alias);

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
                await variant.RemoveFromCacheAsync();
            }

            if (!string.IsNullOrEmpty(alias))
            {
                _preparedModels.Remove(alias);
                _modelMaxOutputTokens.Remove(alias);
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

    public async Task ClearPreparedModelsAsync()
    {
        var modelCount = _preparedModels.Count;

        // Unload all prepared models before clearing
        foreach (var (alias, model) in _preparedModels)
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

        _preparedModels.Clear();
        _modelMaxOutputTokens.Clear();

        if (modelCount > 0)
        {
            Telemetry.Events.FoundryLocalOperationEvent.Log("ClearPreparedModels", $"{modelCount} models");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _preparedModels.Clear();
        _modelMaxOutputTokens.Clear();
        _prepareLock.Dispose();
        _disposed = true;
    }
}