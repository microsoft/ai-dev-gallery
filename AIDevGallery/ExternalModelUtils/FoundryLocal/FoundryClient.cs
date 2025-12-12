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

internal class FoundryClient
{
    private FoundryLocalManager? _manager;
    private ICatalog? _catalog;
    private readonly Dictionary<string, (string ServiceUrl, string ModelId)> _preparedModels = new();
    private readonly SemaphoreSlim _prepareLock = new(1, 1);

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
                    Urls = "http://127.0.0.1:0" // Bind to localhost with random port
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

            // Ensure execution providers are downloaded (for WinML package)
            await client._manager.EnsureEpsDownloadedAsync();

            // Get the catalog
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
        var catalogModels = new List<FoundryCatalogModel>();

        foreach (var model in models)
        {
            // Get the selected variant's info
            var variant = model.SelectedVariant;
            var info = variant.Info;

            catalogModels.Add(new FoundryCatalogModel
            {
                Name = info.Name,
                DisplayName = info.DisplayName ?? info.Name,
                Alias = model.Alias,
                FileSizeMb = info.FileSizeMb ?? 0,
                License = info.License ?? string.Empty,
                ModelId = variant.Id,
                Runtime = info.Runtime
            });
        }

        return catalogModels;
    }

    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        if (_catalog == null)
        {
            return [];
        }

        var cachedVariants = await _catalog.GetCachedModelsAsync();
        var cachedModels = new List<FoundryCachedModel>();

        foreach (var variant in cachedVariants)
        {
            cachedModels.Add(new FoundryCachedModel(variant.Info.Name, variant.Alias));
        }

        return cachedModels;
    }

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel catalogModel, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_catalog == null)
        {
            return new FoundryDownloadResult(false, "Catalog not initialized");
        }

        try
        {
            // Get the model by alias
            var model = await _catalog.GetModelAsync(catalogModel.Alias);
            if (model == null)
            {
                return new FoundryDownloadResult(false, "Model not found in catalog");
            }

            // Check if already cached
            if (await model.IsCachedAsync())
            {
                // Model is cached, prepare it for use
                await PrepareModelAsync(catalogModel.Alias, cancellationToken);
                return new FoundryDownloadResult(true, "Model already downloaded");
            }

            // Download with progress callback
            await model.DownloadAsync(
                progressPercent =>
            {
                progress?.Report(progressPercent / 100f);
            }, cancellationToken);

            // After download, prepare the model for use
            await PrepareModelAsync(catalogModel.Alias, cancellationToken);

            return new FoundryDownloadResult(true, null);
        }
        catch (Exception e)
        {
            return new FoundryDownloadResult(false, e.Message);
        }
    }

    /// <summary>
    /// Prepares a model for use by loading it and starting the web service.
    /// Should be called after download or when first accessing a cached model.
    /// Thread-safe: multiple concurrent calls for the same alias will only prepare once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PrepareModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        // Quick check without lock
        if (_preparedModels.ContainsKey(alias))
        {
            return;
        }

        // Acquire lock to prevent concurrent preparation
        await _prepareLock.WaitAsync(cancellationToken);
        try
        {
            // Check again after acquiring lock
            if (_preparedModels.ContainsKey(alias))
            {
                return;
            }

            if (_catalog == null || _manager == null)
            {
                throw new InvalidOperationException("Foundry Local client not initialized");
            }

            // Get model by alias - SDK will automatically select the best variant
            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            var variant = model.SelectedVariant;

            // Load the model if not already loaded
            if (!await variant.IsLoadedAsync())
            {
                await variant.LoadAsync(cancellationToken);
            }

            // Start web service if not already started
            if (_manager.Urls == null || _manager.Urls.Length == 0)
            {
                await _manager.StartWebServiceAsync(cancellationToken);
            }

            var serviceUrl = _manager.Urls?.FirstOrDefault();
            if (string.IsNullOrEmpty(serviceUrl))
            {
                throw new InvalidOperationException("Failed to start Foundry Local web service");
            }

            // Cache the prepared model info
            _preparedModels[alias] = (serviceUrl, model.Id);
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    /// <summary>
    /// Gets the service URL and model ID for a prepared model.
    /// Returns null if the model hasn't been prepared yet.
    /// </summary>
    /// <returns></returns>
    public (string ServiceUrl, string ModelId)? GetPreparedModel(string alias)
    {
        return _preparedModels.TryGetValue(alias, out var info) ? info : null;
    }

    public Task<string?> GetServiceUrl()
    {
        return Task.FromResult(_manager?.Urls?.FirstOrDefault());
    }
}