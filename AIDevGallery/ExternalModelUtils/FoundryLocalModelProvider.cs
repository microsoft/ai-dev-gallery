// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils.FoundryLocal;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
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
                $"Model '{alias}' is not ready yet. The model is being loaded in the background. Please wait a moment and try again.");
        }

        // Get the native FoundryLocal chat client - direct SDK usage, no web service needed
        // Note: This synchronous wrapper is safe here because the model is already prepared/loaded
        var chatClient = model.GetChatClientAsync().ConfigureAwait(false).GetAwaiter().GetResult();

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

        // Clear prepared models from FoundryClient to avoid stale references
        _foundryManager?.ClearPreparedModels();
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

    /// <summary>
    /// Gets the base cache directory path for FoundryLocal models.
    /// </summary>
    private string GetCacheBasePath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{AppUtils.AppName}", "cache", "models", "Microsoft");
    }

    /// <summary>
    /// Gets cached models with their file system details (path and size).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<IEnumerable<CachedModel>> GetCachedModelsWithDetails()
    {
        var result = new List<CachedModel>();
        var basePath = GetCacheBasePath();

        if (!Directory.Exists(basePath))
        {
            return result;
        }

        var models = await GetModelsAsync();

        foreach (var modelDetails in models)
        {
            // Find directory that starts with the model name (actual directory has version suffix like -1, -2, etc.)
            var matchingDir = Directory.GetDirectories(basePath)
                .FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(modelDetails.Name + "-", StringComparison.OrdinalIgnoreCase) ||
                                      Path.GetFileName(dir).Equals(modelDetails.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingDir != null)
            {
                var dirInfo = new DirectoryInfo(matchingDir);
                long modelSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);

                var cachedModel = new CachedModel(modelDetails, matchingDir, false, modelSize);
                result.Add(cachedModel);
            }
        }

        return result;
    }

    /// <summary>
    /// Deletes a specific cached model directory.
    /// </summary>
    /// <param name="modelPath">The path to the model directory to delete.</param>
    /// <returns>True if the model was successfully deleted; otherwise, false.</returns>
    public bool DeleteCachedModel(string modelPath)
    {
        if (!Directory.Exists(modelPath))
        {
            return false;
        }

        try
        {
            Directory.Delete(modelPath, true);

            // Reset internal state after deleting a model
            Reset();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears all FoundryLocal cached models.
    /// </summary>
    /// <returns>True if the cache was successfully cleared; otherwise, false.</returns>
    public bool ClearAllCache()
    {
        var basePath = GetCacheBasePath();

        if (!Directory.Exists(basePath))
        {
            return true;
        }

        try
        {
            Directory.Delete(basePath, true);

            // Reset internal state after clearing cache
            Reset();

            return true;
        }
        catch
        {
            return false;
        }
    }
}