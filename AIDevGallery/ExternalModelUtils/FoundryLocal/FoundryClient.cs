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
                Web = new Configuration.WebService
                {
                    Urls = "http://127.0.0.1:0"
                }
            };

            await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);

            if (!FoundryLocalManager.IsInitialized)
            {
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
        catch
        {
            return null;
        }
    }

    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalog == null)
        {
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
                Runtime = info.Runtime
            };
        }).ToList();
    }

    public async Task<List<FoundryCachedModelInfo>> ListCachedModels()
    {
        if (_catalog == null)
        {
            return [];
        }

        var cachedVariants = await _catalog.GetCachedModelsAsync();

        return cachedVariants.Select(variant => new FoundryCachedModelInfo(variant.Info.Name, variant.Alias)).ToList();
    }

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel catalogModel, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_catalog == null)
        {
            return new FoundryDownloadResult(false, "Catalog not initialized");
        }

        try
        {
            var model = await _catalog.GetModelAsync(catalogModel.Alias);
            if (model == null)
            {
                return new FoundryDownloadResult(false, "Model not found in catalog");
            }

            if (await model.IsCachedAsync())
            {
                await PrepareModelAsync(catalogModel.Alias, cancellationToken);
                return new FoundryDownloadResult(true, "Model already downloaded");
            }

            await model.DownloadAsync(
                progressPercent =>
                {
                    progress?.Report(progressPercent / 100f);
                },
                cancellationToken);

            await PrepareModelAsync(catalogModel.Alias, cancellationToken);

            return new FoundryDownloadResult(true, null);
        }
        catch (Exception e)
        {
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
                throw new InvalidOperationException("Foundry Local client not initialized");
            }

            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            if (!await model.IsCachedAsync())
            {
                throw new InvalidOperationException($"Model with alias '{alias}' is not cached. Please download it first.");
            }

            if (!await model.IsLoadedAsync())
            {
                await model.LoadAsync(cancellationToken);
            }

            _preparedModels[alias] = model;
            _modelMaxOutputTokens[alias] = (int?)model.SelectedVariant.Info.MaxOutputTokens;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    public IModel? GetPreparedModel(string alias)
    {
        return _preparedModels.TryGetValue(alias, out var model) ? model : null;
    }

    public int? GetModelMaxOutputTokens(string alias)
    {
        return _modelMaxOutputTokens.TryGetValue(alias, out var maxTokens) ? maxTokens : null;
    }

    public async Task<bool> DeleteModelAsync(string modelId)
    {
        if (_catalog == null)
        {
            return false;
        }

        try
        {
            var variant = await _catalog.GetModelVariantAsync(modelId);
            if (variant == null)
            {
                return false;
            }

            if (await variant.IsLoadedAsync())
            {
                await variant.UnloadAsync();
            }

            if (await variant.IsCachedAsync())
            {
                await variant.RemoveFromCacheAsync();
            }

            var alias = variant.Alias;
            if (!string.IsNullOrEmpty(alias))
            {
                _preparedModels.Remove(alias);
                _modelMaxOutputTokens.Remove(alias);
            }

            return true;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("DeleteModelFailed", modelId, ex.Message);
            return false;
        }
    }

    public async Task ClearPreparedModelsAsync()
    {
        // Unload all prepared models before clearing
        foreach (var kvp in _preparedModels)
        {
            try
            {
                var model = kvp.Value;
                if (await model.IsLoadedAsync())
                {
                    await model.UnloadAsync();
                }
            }
            catch (Exception ex)
            {
                // Log but continue unloading other models
                Telemetry.Events.FoundryLocalErrorEvent.Log("UnloadModelFailed", kvp.Key, ex.Message);
            }
        }

        _preparedModels.Clear();
        _modelMaxOutputTokens.Clear();
    }

    public string? GetServiceUrl()
    {
        return _manager?.Urls?.FirstOrDefault();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Unload all prepared models synchronously
        foreach (var kvp in _preparedModels)
        {
            try
            {
                var model = kvp.Value;

                // Note: Using synchronous Wait here as Dispose cannot be async
                if (model.IsLoadedAsync().ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    model.UnloadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Suppress exceptions during disposal to prevent finalizer issues
            }
        }

        _preparedModels.Clear();
        _modelMaxOutputTokens.Clear();
        _prepareLock.Dispose();
        _disposed = true;
    }
}