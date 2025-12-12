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

    public async Task<List<FoundryModel>> ListCatalogModels()
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
            return new FoundryModel
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

    public async Task<FoundryDownloadResult> DownloadModel(FoundryModel catalogModel, IProgress<float>? progress, CancellationToken cancellationToken = default)
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
            }, cancellationToken);

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
        if (_preparedModels.ContainsKey(alias))
        {
            return;
        }

        await _prepareLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern for thread safety
            if (_preparedModels.ContainsKey(alias))
            {
                return;
            }

            if (_catalog == null || _manager == null)
            {
                throw new InvalidOperationException("Foundry Local client not initialized");
            }

            // SDK automatically selects the best variant for the given alias
            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            if (!await model.IsLoadedAsync())
            {
                await model.LoadAsync(cancellationToken);
            }

            if (_manager.Urls == null || _manager.Urls.Length == 0)
            {
                await _manager.StartWebServiceAsync(cancellationToken);
            }

            var serviceUrl = _manager.Urls?.FirstOrDefault();
            if (string.IsNullOrEmpty(serviceUrl))
            {
                throw new InvalidOperationException("Failed to start Foundry Local web service");
            }

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
    public (string ServiceUrl, string ModelId)? GetPreparedModel(string alias)
    {
        return _preparedModels.TryGetValue(alias, out var info) ? info : null;
    }

    public Task<string?> GetServiceUrl()
    {
        return Task.FromResult(_manager?.Urls?.FirstOrDefault());
    }
}