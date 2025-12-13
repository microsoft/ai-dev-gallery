// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class FoundryClient
{
    private readonly Dictionary<string, (string ServiceUrl, string ModelId)> _preparedModels = new();
    private readonly SemaphoreSlim _prepareLock = new(1, 1);
    private FoundryLocalManager? _manager;
    private ICatalog? _catalog;

    public static async Task<FoundryClient?> CreateAsync()
    {
        try
        {
            Debug.WriteLine("[FoundryClient] Creating FoundryClient...");
            var config = new Configuration
            {
                AppName = "AIDevGallery",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Debug,  // Changed from Warning to Debug
                Web = new Configuration.WebService
                {
                    Urls = "http://127.0.0.1:0"
                }
            };

            Debug.WriteLine("[FoundryClient] Creating FoundryLocalManager...");
            await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);

            if (!FoundryLocalManager.IsInitialized)
            {
                Debug.WriteLine("[FoundryClient] ERROR: FoundryLocalManager not initialized");
                return null;
            }

            Debug.WriteLine("[FoundryClient] FoundryLocalManager initialized successfully");
            var client = new FoundryClient
            {
                _manager = FoundryLocalManager.Instance
            };

            Debug.WriteLine("[FoundryClient] Ensuring EPs downloaded...");
            await client._manager.EnsureEpsDownloadedAsync();
            Debug.WriteLine("[FoundryClient] Getting catalog...");
            client._catalog = await client._manager.GetCatalogAsync();
            Debug.WriteLine("[FoundryClient] FoundryClient created successfully");

            return client;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FoundryClient] ERROR: Failed to create FoundryClient: {ex.Message}");
            Debug.WriteLine($"[FoundryClient] Stack trace: {ex.StackTrace}");
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
    /// Prepares a model for use by loading it and starting the web service.
    /// Should be called after download or when first accessing a cached model.
    /// Thread-safe: multiple concurrent calls for the same alias will only prepare once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PrepareModelAsync(string alias, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[FoundryClient] PrepareModelAsync called for alias: {alias}");
        
        if (_preparedModels.ContainsKey(alias))
        {
            Debug.WriteLine($"[FoundryClient] Model {alias} already prepared");
            return;
        }

        Debug.WriteLine($"[FoundryClient] Acquiring prepare lock for {alias}...");
        await _prepareLock.WaitAsync(cancellationToken);
        Debug.WriteLine($"[FoundryClient] Lock acquired for {alias}");
        try
        {
            // Double-check pattern for thread safety
            if (_preparedModels.ContainsKey(alias))
            {
                Debug.WriteLine($"[FoundryClient] Model {alias} was prepared while waiting for lock");
                return;
            }

            if (_catalog == null || _manager == null)
            {
                Debug.WriteLine($"[FoundryClient] ERROR: Catalog or manager is null");
                throw new InvalidOperationException("Foundry Local client not initialized");
            }

            // SDK automatically selects the best variant for the given alias
            Debug.WriteLine($"[FoundryClient] Getting model from catalog: {alias}");
            var model = await _catalog.GetModelAsync(alias);
            if (model == null)
            {
                Debug.WriteLine($"[FoundryClient] ERROR: Model {alias} not found in catalog");
                throw new InvalidOperationException($"Model with alias '{alias}' not found in catalog");
            }

            Debug.WriteLine($"[FoundryClient] Model found. ID: {model.Id}");
            Debug.WriteLine($"[FoundryClient] Checking if model is loaded...");
            if (!await model.IsLoadedAsync())
            {
                Debug.WriteLine($"[FoundryClient] Model not loaded. Loading model {alias}...");
                await model.LoadAsync(cancellationToken);
                Debug.WriteLine($"[FoundryClient] Model {alias} loaded successfully");
            }
            else
            {
                Debug.WriteLine($"[FoundryClient] Model {alias} already loaded");
            }

            Debug.WriteLine($"[FoundryClient] Checking web service status...");
            if (_manager.Urls == null || _manager.Urls.Length == 0)
            {
                Debug.WriteLine($"[FoundryClient] Starting web service...");
                await _manager.StartWebServiceAsync(cancellationToken);
                Debug.WriteLine($"[FoundryClient] Web service started");
            }
            else
            {
                Debug.WriteLine($"[FoundryClient] Web service already running at: {string.Join(", ", _manager.Urls)}");
            }

            var serviceUrl = _manager.Urls?.FirstOrDefault();
            if (string.IsNullOrEmpty(serviceUrl))
            {
                Debug.WriteLine($"[FoundryClient] ERROR: Failed to get service URL");
                throw new InvalidOperationException("Failed to start Foundry Local web service");
            }

            Debug.WriteLine($"[FoundryClient] Service URL: {serviceUrl}, Model ID: {model.Id}");
            _preparedModels[alias] = (serviceUrl, model.Id);
            
            // Test the endpoint to verify it's working
            await TestEndpointAsync(serviceUrl, model.Id);
            
            Debug.WriteLine($"[FoundryClient] Model {alias} prepared successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FoundryClient] ERROR in PrepareModelAsync: {ex.Message}");
            Debug.WriteLine($"[FoundryClient] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            _prepareLock.Release();
            Debug.WriteLine($"[FoundryClient] Lock released for {alias}");
        }
    }

    /// <summary>
    /// Gets the service URL and model ID for a prepared model.
    /// Returns null if the model hasn't been prepared yet.
    /// </summary>
    /// <returns>A tuple containing the service URL and model ID, or null if not prepared.</returns>
    public (string ServiceUrl, string ModelId)? GetPreparedModel(string alias)
    {
        return _preparedModels.TryGetValue(alias, out var info) ? info : null;
    }

    public Task<string?> GetServiceUrl()
    {
        return Task.FromResult(_manager?.Urls?.FirstOrDefault());
    }

    private async Task TestEndpointAsync(string serviceUrl, string modelId)
    {
        try
        {
            Debug.WriteLine($"[FoundryClient] Testing endpoint {serviceUrl}/v1/models...");
            using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await testClient.GetAsync($"{serviceUrl}/v1/models");
            Debug.WriteLine($"[FoundryClient] Endpoint test response status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[FoundryClient] Endpoint test response: {content.Substring(0, Math.Min(200, content.Length))}...");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FoundryClient] WARNING: Endpoint test failed: {ex.Message}");
        }
    }
}