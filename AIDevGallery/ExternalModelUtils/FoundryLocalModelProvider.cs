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

    public static FoundryLocalModelProvider Instance { get; } = new FoundryLocalModelProvider();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.AI.Foundry.Local.WinML", "Microsoft.Extensions.AI"];

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public string UrlPrefix => "fl://";

    public string Icon => $"fl{AppUtils.GetThemeAssetSuffix()}.svg";

    // Note: Foundry Local uses direct SDK calls, not web service, so Url is not applicable
    public string Url => string.Empty;

    public string? IChatClientImplementationNamespace { get; } = "Microsoft.AI.Foundry.Local";
    public string? GetDetailsUrl(ModelDetails details)
    {
        // Foundry Local models run locally via SDK, no online details page available
        return null;
    }

    private string ExtractAlias(string url) => url.Replace(UrlPrefix, string.Empty);

    public IChatClient? GetIChatClient(string url)
    {
        var alias = ExtractAlias(url);

        if (_foundryManager == null || string.IsNullOrEmpty(alias))
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("GetChatClient", "ClientNotInitialized", alias ?? "unknown", "Foundry Local client not initialized or invalid model alias");
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        // Must be prepared beforehand via EnsureModelReadyAsync to avoid deadlock
        var model = _foundryManager.GetLoadedModel(alias);
        if (model == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("GetChatClient", "ModelNotReady", alias, "Model is not ready yet. EnsureModelReadyAsync must be called first");
            throw new InvalidOperationException(
                $"Model '{alias}' is not ready yet. The model is being loaded in the background. Please call EnsureModelReadyAsync(url) first.");
        }

        // Get the pre-cached chat client created during EnsureModelLoadedAsync
        // This avoids sync-over-async anti-pattern
        var chatClient = _foundryManager.GetChatClient(alias);
        if (chatClient == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("GetChatClient", "ChatClientNotCached", alias, "Chat client not cached. This should not happen.");
            throw new InvalidOperationException($"Chat client for model '{alias}' was not cached during loading.");
        }

        // Get model's MaxOutputTokens if available
        int? maxOutputTokens = _foundryManager.GetModelMaxOutputTokens(alias);

        // Wrap it in our adapter to implement IChatClient interface
        Telemetry.Events.FoundryLocalOperationEvent.Log("GetChatClient", alias);
        return new FoundryLocal.FoundryLocalChatClientAdapter(chatClient, model.Id, maxOutputTokens);
    }

    public string? GetIChatClientString(string url)
    {
        var alias = ExtractAlias(url);

        if (_foundryManager == null)
        {
            return null;
        }

        var model = _foundryManager.GetLoadedModel(alias);
        if (model == null)
        {
            return null;
        }

        return $@"// Initialize Foundry Local
var config = new Configuration 
{{ 
    AppName = ""YourApp"", 
    LogLevel = LogLevel.Warning 
}};
await FoundryLocalManager.CreateAsync(config);
var manager = FoundryLocalManager.Instance;
var catalog = await manager.GetCatalogAsync();

// Get and load the model
var model = await catalog.GetModelAsync(""{alias}"");
await model.LoadAsync();

// Get chat client and use it
var chatClient = await model.GetChatClientAsync();
var messages = new List<ChatMessage> {{ new ChatMessage(ChatRole.User, ""Your message here"") }};
await foreach (var chunk in chatClient.CompleteChatStreamingAsync(messages))
{{
    // Process streaming response
    Console.Write(chunk.Choices[0].Delta?.Content);
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

    /// <summary>
    /// Maps ModelType enums to Foundry Local task type strings for filtering models.
    /// </summary>
    /// <param name="types">List of ModelType enums to map.</param>
    /// <returns>Set of task type strings (e.g., "chat-completion", "automatic-speech-recognition").</returns>
    public static HashSet<string> GetRequiredTasksForModelTypes(List<ModelType> types)
    {
        var requiredTasks = new HashSet<string>();

        foreach (var type in types)
        {
            var typeName = type.ToString();

            // Language models and chat-related models use chat-completion
            if (type == ModelType.LanguageModels ||
                type == ModelType.PhiSilica ||
                type == ModelType.PhiSilicaLora ||
                (typeName.StartsWith("Phi", StringComparison.Ordinal) && !typeName.Contains("Vision")) ||
                typeName.StartsWith("Mistral", StringComparison.Ordinal) ||
                type == ModelType.TextSummarizer ||
                type == ModelType.TextRewriter ||
                type == ModelType.DescribeYourChange ||
                type == ModelType.TextToTableConverter)
            {
                requiredTasks.Add(ModelTaskTypes.ChatCompletion);
            }

            // Audio models use automatic-speech-recognition
            else if (type == ModelType.AudioModels ||
                     type == ModelType.Whisper ||
                     typeName.StartsWith("Whisper", StringComparison.Ordinal))
            {
                requiredTasks.Add(ModelTaskTypes.AutomaticSpeechRecognition);
            }

            // For other model types, no filtering is applied (empty set will show all models)
        }

        return requiredTasks;
    }

    /// <summary>
    /// Lists all models available in the Foundry Local catalog.
    /// </summary>
    private async Task<List<FoundryCatalogModel>> ListCatalogModelsAsync()
    {
        if (_foundryManager?.Catalog == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ListCatalogModels", "CatalogNotInitialized", "N/A", "Catalog not initialized");
            return [];
        }

        var models = await _foundryManager.Catalog.ListModelsAsync();
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

    /// <summary>
    /// Lists all cached (downloaded) models.
    /// </summary>
    private async Task<List<FoundryCachedModelInfo>> ListCachedModelsAsync()
    {
        if (_foundryManager?.Catalog == null)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log("ListCachedModels", "CatalogNotInitialized", "N/A", "Catalog not initialized");
            return [];
        }

        return (await _foundryManager.Catalog.GetCachedModelsAsync())
            .Select(variant => new FoundryCachedModelInfo(variant.Info.Name, variant.Alias))
            .ToList();
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

        var startTime = DateTime.Now;
        var result = await _foundryManager.DownloadModel(model, progress, cancellationToken);
        var duration = (DateTime.Now - startTime).TotalSeconds;

        FoundryLocalDownloadEvent.Log(
            model.Alias,
            result.Success,
            result.ErrorMessage,
            model.FileSizeMb,
            duration);

        return result.Success;
    }

    /// <summary>
    /// Resets the provider state by clearing downloaded models cache and unloading all loaded models.
    /// WARNING: This will unload all currently loaded models. Any ongoing inference will fail.
    /// </summary>
    private async Task ResetAsync()
    {
        _downloadedModels = null;

        if (_foundryManager != null)
        {
            await _foundryManager.UnloadAllModelsAsync();
        }
    }

    /// <summary>
    /// Retries initialization of the Foundry Local provider.
    /// This will reset the provider state and attempt to reinitialize the manager.
    /// </summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    public async Task<bool> RetryInitializationAsync()
    {
        _downloadedModels = null;
        _catalogModels = null;
        _foundryManager = null;

        await InitializeAsync();

        return _foundryManager != null;
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

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await ListCatalogModelsAsync()).Select(m => ToModelDetails(m));
        }

        var cachedModels = await ListCachedModelsAsync();

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

        Telemetry.Events.FoundryLocalOperationEvent.Log("ProviderInitialization", $"{downloadedModels.Count} cached models");
    }

    private ModelDetails ToModelDetails(FoundryCatalogModel model)
    {
        return new ModelDetails
        {
            Id = $"fl-{model.Alias}",
            Name = model.DisplayName,
            Url = $"{UrlPrefix}{model.Alias}",
            Description = $"{model.DisplayName} is running locally with Foundry Local",
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

        if (_foundryManager.GetLoadedModel(alias) != null)
        {
            return;
        }

        await _foundryManager.EnsureModelLoadedAsync(alias, cancellationToken);
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
                $"FoundryLocal: {catalogModel.Alias}/{catalogModel.ModelId}",
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
                    if (_downloadedModels != null)
                    {
                        _downloadedModels = _downloadedModels.Where(m =>
                            (m.ProviderModelDetails as FoundryCatalogModel)?.Alias != catalogModel.Alias);
                    }
                }

                return result;
            }

            return false;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log(
                "CachedModelDelete",
                "Exception",
                cachedModel.Details.Name,
                ex.Message);
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
            // Get snapshot of cached models to avoid collection modification during enumeration
            var cachedModels = (await GetCachedModelsWithDetails()).ToList();
            var allDeleted = true;
            var deletedCount = 0;

            foreach (var cachedModel in cachedModels)
            {
                if (cachedModel.Details.ProviderModelDetails is not FoundryCatalogModel catalogModel)
                {
                    continue;
                }

                try
                {
                    var deleted = await _foundryManager.DeleteModelAsync(catalogModel.ModelId);
                    if (deleted)
                    {
                        deletedCount++;
                    }
                    else
                    {
                        allDeleted = false;
                    }
                }
                catch (Exception ex)
                {
                    Telemetry.Events.FoundryLocalErrorEvent.Log(
                        "ClearAllCache",
                        "ModelDeletion",
                        catalogModel.Alias,
                        ex.Message);
                    allDeleted = false;
                }
            }

            await ResetAsync();

            Telemetry.Events.FoundryLocalOperationEvent.Log(
                "ClearAllCache",
                $"{deletedCount}/{cachedModels.Count} models deleted");

            return allDeleted;
        }
        catch (Exception ex)
        {
            Telemetry.Events.FoundryLocalErrorEvent.Log(
                "ClearAllCache",
                "Exception",
                "all",
                ex.Message);
            return false;
        }
    }
}