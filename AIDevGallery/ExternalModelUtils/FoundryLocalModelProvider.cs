// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils.FoundryLocal;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal class FoundryLocalModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _downloadedModels;
    private IEnumerable<ModelDetails>? _catalogModels;
    private FoundryClient? _foundryManager;
    private string? url;

    public static FoundryLocalModelProvider Instance { get; } = new FoundryLocalModelProvider();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public string UrlPrefix => "fl://";

    public string Icon => $"fl{AppUtils.GetThemeAssetSuffix()}.svg";
    public string Url => url ?? string.Empty;

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";
    public string? GetDetailsUrl(ModelDetails details)
    {
        throw new NotImplementedException();
    }

    private string ExtractAlias(string url) => url.Replace(UrlPrefix, string.Empty);

    public IChatClient? GetIChatClient(string url)
    {
        Debug.WriteLine($"[FoundryLocal] GetIChatClient called with url: {url}");
        var alias = ExtractAlias(url);
        Debug.WriteLine($"[FoundryLocal] Extracted alias: {alias}");

        if (_foundryManager == null || string.IsNullOrEmpty(alias))
        {
            Debug.WriteLine($"[FoundryLocal] ERROR: Client not initialized or invalid alias");
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        // Must be prepared beforehand via EnsureModelReadyAsync to avoid deadlock
        Debug.WriteLine($"[FoundryLocal] Getting prepared model for alias: {alias}");
        var model = _foundryManager.GetPreparedModel(alias);
        if (model == null)
        {
            Debug.WriteLine($"[FoundryLocal] ERROR: Model not prepared yet");
            throw new InvalidOperationException(
                $"Model '{alias}' is not ready yet. The model is being loaded in the background. Please wait a moment and try again.");
        }

        Debug.WriteLine($"[FoundryLocal] Model ID: {model.Id}");
        
        // Get the native FoundryLocal chat client - no web service, no SSE issues!
        Debug.WriteLine($"[FoundryLocal] Getting native chat client from model");
        var chatClient = model.GetChatClientAsync().Result;
        
        // Wrap it in our adapter
        Debug.WriteLine($"[FoundryLocal] Creating FoundryLocalChatClientAdapter");
        var adapter = new FoundryLocal.FoundryLocalChatClientAdapter(chatClient, model.Id);
        
        Debug.WriteLine($"[FoundryLocal] IChatClient adapter created successfully");
        return adapter;
    }

    public string? GetIChatClientString(string url)
    {
        var alias = ExtractAlias(url);

        if (_foundryManager == null)
        {
            return null;
        }

        var model = _foundryManager.GetPreparedModel(alias);
        if (model == null)
        {
            return null;
        }

        return $"var model = await catalog.GetModelAsync(\"{alias}\"); await model.LoadAsync(); var chatClient = await model.GetChatClientAsync(); /* Use chatClient.CompleteChatStreamingAsync() */";
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            Reset();
        }

        await InitializeAsync(cancelationToken);

        return _downloadedModels ?? [];
    }

    public IEnumerable<ModelDetails> GetAllModelsInCatalog()
    {
        return _catalogModels ?? [];
    }

    public async Task<bool> DownloadModel(ModelDetails modelDetails, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_foundryManager == null)
        {
            return false;
        }

        if (modelDetails.ProviderModelDetails is not FoundryCatalogModel model)
        {
            return false;
        }

        var result = await _foundryManager.DownloadModel(model, progress, cancellationToken);

        // Log telemetry for both success and failure
        FoundryLocalDownloadEvent.Log(model.Alias, result.Success, result.ErrorMessage);

        return result.Success;
    }

    private void Reset()
    {
        _downloadedModels = null;
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryManager != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        _foundryManager = _foundryManager ?? await FoundryClient.CreateAsync();

        if (_foundryManager == null)
        {
            return;
        }

        url = url ?? await _foundryManager.GetServiceUrl();

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await _foundryManager.ListCatalogModels()).Select(m => ToModelDetails(m));
        }

        var cachedModels = await _foundryManager.ListCachedModels();

        List<ModelDetails> downloadedModels = [];

        var catalogByAlias = _catalogModels.GroupBy(m => ((FoundryCatalogModel)m.ProviderModelDetails!).Alias).ToList();

        foreach (var aliasGroup in catalogByAlias)
        {
            var firstModel = aliasGroup.First();
            var catalogModel = (FoundryCatalogModel)firstModel.ProviderModelDetails!;
            var hasCachedVariant = cachedModels.Any(cm => cm.Id == catalogModel.Alias);

            if (hasCachedVariant)
            {
                downloadedModels.Add(firstModel);
            }
        }

        _downloadedModels = downloadedModels;
    }

    private ModelDetails ToModelDetails(FoundryCatalogModel model)
    {
        return new ModelDetails
        {
            Id = $"fl-{model.Alias}",
            Name = model.DisplayName,
            Url = $"{UrlPrefix}{model.Alias}",
            Description = $"{model.DisplayName} running locally with Foundry Local",
            HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
            Size = model.FileSizeMb * 1024 * 1024,
            SupportedOnQualcomm = true,
            License = model.License?.ToLowerInvariant(),
            ProviderModelDetails = model
        };
    }

    public async Task<bool> IsAvailable()
    {
        await InitializeAsync();
        return _foundryManager != null;
    }

    /// <summary>
    /// Ensures the model is ready to use before calling GetIChatClient.
    /// This method must be called before GetIChatClient to avoid deadlock.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnsureModelReadyAsync(string url, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[FoundryLocal] EnsureModelReadyAsync called with url: {url}");
        var alias = ExtractAlias(url);
        Debug.WriteLine($"[FoundryLocal] Extracted alias: {alias}");

        if (_foundryManager == null || string.IsNullOrEmpty(alias))
        {
            Debug.WriteLine($"[FoundryLocal] ERROR: Client not initialized or invalid alias in EnsureModelReadyAsync");
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        Debug.WriteLine($"[FoundryLocal] Checking if model {alias} is already prepared...");
        if (_foundryManager.GetPreparedModel(alias) != null)
        {
            Debug.WriteLine($"[FoundryLocal] Model {alias} already prepared, skipping PrepareModelAsync");
            return;
        }

        Debug.WriteLine($"[FoundryLocal] Model {alias} not prepared yet, calling PrepareModelAsync...");
        await _foundryManager.PrepareModelAsync(alias, cancellationToken);
        Debug.WriteLine($"[FoundryLocal] PrepareModelAsync completed for {alias}");
    }
}