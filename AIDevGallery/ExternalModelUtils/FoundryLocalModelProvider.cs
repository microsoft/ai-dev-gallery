// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils.FoundryLocal;
using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal class FoundryLocalModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _downloadedModels;
    private IEnumerable<ModelDetails>? _catalogModels;
    private FoundryClient? _foundryClient;
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
        var alias = ExtractAlias(url);

        if (_foundryClient == null || string.IsNullOrEmpty(alias))
        {
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        // Must be prepared beforehand via EnsureModelReadyAsync to avoid deadlock
        var preparedInfo = _foundryClient.GetPreparedModel(alias);
        if (preparedInfo == null)
        {
            throw new InvalidOperationException(
                $"Model '{alias}' is not ready yet. The model is being loaded in the background. Please wait a moment and try again.");
        }

        var (serviceUrl, modelId) = preparedInfo.Value;

        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri($"{serviceUrl}/v1")
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetIChatClientString(string url)
    {
        var alias = ExtractAlias(url);

        if (_foundryClient == null)
        {
            return null;
        }

        var preparedInfo = _foundryClient.GetPreparedModel(alias);
        if (preparedInfo == null)
        {
            return null;
        }

        var (serviceUrl, modelId) = preparedInfo.Value;
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{serviceUrl}/v1\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
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
        if (_foundryClient == null)
        {
            return false;
        }

        if (modelDetails.ProviderModelDetails is not FoundryModel model)
        {
            return false;
        }

        return (await _foundryClient.DownloadModel(model, progress, cancellationToken)).Success;
    }

    private void Reset()
    {
        _downloadedModels = null;
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryClient != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        _foundryClient = _foundryClient ?? await FoundryClient.CreateAsync();

        if (_foundryClient == null)
        {
            return;
        }

        url = url ?? await _foundryClient.GetServiceUrl();

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await _foundryClient.ListCatalogModels()).Select(m => ToModelDetails(m));
        }

        var cachedModels = await _foundryClient.ListCachedModels();

        List<ModelDetails> downloadedModels = [];

        var catalogByAlias = _catalogModels.GroupBy(m => ((FoundryModel)m.ProviderModelDetails!).Alias).ToList();

        foreach (var aliasGroup in catalogByAlias)
        {
            var firstModel = aliasGroup.First();
            var catalogModel = (FoundryModel)firstModel.ProviderModelDetails!;
            var hasCachedVariant = cachedModels.Any(cm => cm.Id == catalogModel.Alias);

            if (hasCachedVariant)
            {
                downloadedModels.Add(firstModel);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _foundryClient.PrepareModelAsync(catalogModel.Alias, cancelationToken);
                    }
                    catch
                    {
                        // Silently fail - user will see "not ready" error when attempting to use the model
                    }
                });
            }
        }

        _downloadedModels = downloadedModels;
    }

    private ModelDetails ToModelDetails(FoundryModel model)
    {
        string acceleratorInfo = model.Runtime?.ExecutionProvider switch
        {
            "DirectML" => " (GPU)",
            "QNN" => " (NPU)",
            _ => string.Empty
        };

        return new ModelDetails()
        {
            Id = $"fl-{model.Alias}",
            Name = model.DisplayName + acceleratorInfo,
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
        return _foundryClient != null;
    }

    /// <summary>
    /// Ensures the model is ready to use before calling GetIChatClient.
    /// This method must be called before GetIChatClient to avoid deadlock.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnsureModelReadyAsync(string url, CancellationToken cancellationToken = default)
    {
        var alias = ExtractAlias(url);

        if (_foundryClient == null || string.IsNullOrEmpty(alias))
        {
            throw new InvalidOperationException("Foundry Local client not initialized or invalid model alias");
        }

        if (_foundryClient.GetPreparedModel(alias) != null)
        {
            return;
        }

        await _foundryClient.PrepareModelAsync(alias, cancellationToken);
    }
}