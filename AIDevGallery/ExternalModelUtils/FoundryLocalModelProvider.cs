// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils.FoundryLocal;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
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

    public List<string> NugetPackageReferences => ["Microsoft.AI.Foundry.Local.WinML", "Microsoft.Extensions.AI"];

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
        var alias = ExtractAlias(url);

        if (_foundryManager == null || string.IsNullOrEmpty(alias))
        {
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        // Must be prepared beforehand via EnsureModelReadyAsync to avoid deadlock
        var model = _foundryManager.GetPreparedModel(alias);
        if (model == null)
        {
            throw new InvalidOperationException(
                $"Model '{alias}' is not ready yet. The model is being loaded in the background. Please call EnsureModelReadyAsync(url) first.");
        }

        // Get the native FoundryLocal chat client - direct SDK usage, no web service needed
        // SAFETY: This synchronous wrapper is safe because:
        // 1. The model is already prepared/loaded (checked above)
        // 2. GetChatClientAsync on a loaded model should complete synchronously
        // 3. ConfigureAwait(false) prevents SynchronizationContext capture
        var chatClient = Task.Run(async () => await model.GetChatClientAsync().ConfigureAwait(false))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        // Get model's MaxOutputTokens if available
        int? maxOutputTokens = _foundryManager.GetModelMaxOutputTokens(alias);

        // Wrap it in our adapter to implement IChatClient interface
        return new FoundryLocal.FoundryLocalChatClientAdapter(chatClient, model.Id, maxOutputTokens);
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

        return $@"// Initialize Foundry Local
var config = new Configuration {{ AppName = ""YourApp"", LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning }};
await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);
var manager = FoundryLocalManager.Instance;
var catalog = await manager.GetCatalogAsync();

// Get and load the model
var model = await catalog.GetModelAsync(""{alias}"");
await model.LoadAsync();

// Get chat client and use it
var chatClient = await model.GetChatClientAsync();
var messages = new List<ChatMessage> {{ new(""user"", ""Your message here"") }};
await foreach (var chunk in chatClient.CompleteChatStreamingAsync(messages))
{{
    // Process streaming response
    Console.Write(chunk.Choices[0].Message?.Content);
}}";
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            await ResetAsync();
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

        FoundryLocalDownloadEvent.Log(model.Alias, result.Success, result.ErrorMessage);

        return result.Success;
    }

    /// <summary>
    /// Resets the provider state by clearing downloaded models cache and unloading all prepared models.
    /// WARNING: This will unload all currently loaded models. Any ongoing inference will fail.
    /// </summary>
    private async Task ResetAsync()
    {
        _downloadedModels = null;

        if (_foundryManager != null)
        {
            await _foundryManager.ClearPreparedModelsAsync();
        }
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

        url = url ?? _foundryManager.GetServiceUrl();

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
        var alias = ExtractAlias(url);

        if (_foundryManager == null || string.IsNullOrEmpty(alias))
        {
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        if (_foundryManager.GetPreparedModel(alias) != null)
        {
            return;
        }

        await _foundryManager.PrepareModelAsync(alias, cancellationToken);
    }

    public async Task<IEnumerable<CachedModel>> GetCachedModelsWithDetails()
    {
        var result = new List<CachedModel>();

        // Get the list of downloaded models (which are already filtered by cached status)
        var models = await GetModelsAsync();

        foreach (var modelDetails in models)
        {
            if (modelDetails.ProviderModelDetails is not FoundryCatalogModel catalogModel)
            {
                continue;
            }

            var cachedModel = new CachedModel(
                modelDetails,
                $"FoundryLocal: {catalogModel.Alias}",
                false,
                modelDetails.Size);
            result.Add(cachedModel);
        }

        return result;
    }

    public async Task<bool> DeleteCachedModelAsync(CachedModel cachedModel)
    {
        if (_foundryManager == null)
        {
            return false;
        }

        try
        {
            if (cachedModel.Details.ProviderModelDetails is FoundryCatalogModel catalogModel)
            {
                var result = await _foundryManager.DeleteModelAsync(catalogModel.ModelId);
                if (result)
                {
                    await ResetAsync();
                }

                return result;
            }

            return false;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("DeleteCachedModelFailed", cachedModel.Details.Name, ex.Message);
            return false;
        }
    }

    public async Task<bool> ClearAllCacheAsync()
    {
        if (_foundryManager == null)
        {
            return true;
        }

        try
        {
            var cachedModels = await GetCachedModelsWithDetails();
            var allDeleted = true;

            foreach (var cachedModel in cachedModels)
            {
                var deleted = await DeleteCachedModelAsync(cachedModel);
                if (!deleted)
                {
                    allDeleted = false;
                }
            }

            await ResetAsync();

            return allDeleted;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ClearAllCacheFailed", "all", ex.Message);
            return false;
        }
    }
}