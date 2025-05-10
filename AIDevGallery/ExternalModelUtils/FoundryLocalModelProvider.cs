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
    private FoundryClient? _foundryManager;

    public static FoundryLocalModelProvider Instance { get; } = new FoundryLocalModelProvider();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run localy via Foundry Local";

    public string UrlPrefix => "fl://";

    public string LightIcon => "azure-ai-foundry.svg";

    public string DarkIcon => LightIcon;

    public string Url { get; private set; } = "http://localhost:5272/v1";

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";
    public string? GetDetailsUrl(ModelDetails details)
    {
        throw new NotImplementedException();
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri(Url)
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
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

    public Task<bool> DownloadModel(string name, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_foundryManager == null)
        {
            return Task.FromResult(false);
        }

        return _foundryManager.DownloadModel(name, progress, cancellationToken);
    }

    private void Reset()
    {
        _downloadedModels = null;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryManager != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        _foundryManager = await FoundryClient.CreateAsync();

        if (_foundryManager == null)
        {
            return;
        }

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await _foundryManager.ListCatalogModels()).Select(m => ToModelDetails(m.Name, null, null, m.License));
        }

        var cachedModels = await _foundryManager.ListCachedModels();

        List<ModelDetails> downloadedModels = [];

        foreach (var model in _catalogModels)
        {
            var cachedModel = cachedModels.FirstOrDefault(m => m.Name == model.Name);

            if (cachedModel != default)
            {
                model.Id = $"{UrlPrefix}{cachedModel.Id}";
                downloadedModels.Add(model);
                cachedModels.Remove(cachedModel);
            }
        }

        foreach (var model in cachedModels)
        {
            downloadedModels.Add(ToModelDetails(model.Id, null, model.Name));
        }

        _downloadedModels = downloadedModels;

        return;
    }

    private ModelDetails ToModelDetails(string name, string? size = null, string? paramSize = null, string? license = null)
    {
        return new ModelDetails()
        {
            Id = $"fl-{name}",
            Name = name,
            Url = $"{UrlPrefix}{name}",
            Description = $"{name} running localy with Foundry Local",
            HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
            Size = size != null ? AppUtils.StringToFileSize(size) : 0,
            SupportedOnQualcomm = true,
            ParameterSize = paramSize,
            License = license?.ToLowerInvariant()
        };
    }
}