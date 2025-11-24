// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal partial class FoundryLocalModelProvider : IExternalModelProvider
{
    private static readonly object _initLock = new object();
    private ILogger<FoundryLocalModelProvider>? _logger;
    private IEnumerable<ModelDetails>? _downloadedModels;
    private IEnumerable<ModelDetails>? _catalogModels;
    private FoundryLocalManager? _foundryManager;
    private ICatalog? _catalog;
    private string? _serviceUrl;
    private bool _isInitialized;

    public static FoundryLocalModelProvider Instance { get; } = new FoundryLocalModelProvider();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI", "Microsoft.AI.Foundry.Local"];

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public string UrlPrefix => "fl://";

    public string Icon => $"fl{AppUtils.GetThemeAssetSuffix()}.svg";
    public string Url => _serviceUrl ?? string.Empty;

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";
    
    public string? GetDetailsUrl(ModelDetails details)
    {
        return null;
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(Url))
        {
            return null;
        }

        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri($"{Url}/v1")
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(Url))
        {
            return null;
        }

        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}/v1\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
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
        if (_catalog == null || _foundryManager == null)
        {
            return false;
        }

        try
        {
            // Try to find the model by alias first
            var model = await _catalog.GetModelAsync(modelDetails.Name, cancellationToken);
            if (model == null)
            {
                // If not found by name, try to find it from the stored provider details
                if (modelDetails.ProviderModelDetails is Model foundryModel)
                {
                    model = foundryModel;
                }
                else
                {
                    return false;
                }
            }

            Action<float>? progressCallback = progress != null ? progress.Report : null;
            await model.DownloadAsync(progressCallback, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            LogDownloadError(_logger, ex, modelDetails.Name);
            return false;
        }
    }

    private void Reset()
    {
        lock (_initLock)
        {
            _downloadedModels = null;
            _catalogModels = null;
            _isInitialized = false;
        }

        _ = InitializeAsync();
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_isInitialized && _foundryManager != null && _catalog != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        lock (_initLock)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        try
        {
            var config = new Configuration
            {
                AppName = "AIDevGallery",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                Web = new Configuration.WebService
                {
                    Urls = "http://127.0.0.1:55588"
                }
            };

            using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
            var logger = loggerFactory.CreateLogger<FoundryLocalModelProvider>();

            // Try to create or get existing FoundryLocalManager
            try
            {
                await FoundryLocalManager.CreateAsync(config, logger, cancelationToken);
            }
            catch (FoundryLocalException ex) when (ex.Message.Contains("has already been created"))
            {
                // Manager already exists, this is fine
            }
            catch (Exception ex)
            {
                // Service might not be available or other initialization errors
                LogInitializationError(_logger, ex);
                return;
            }
            
            _foundryManager = FoundryLocalManager.Instance;

            if (_foundryManager == null)
            {
                return;
            }

            _catalog = await _foundryManager.GetCatalogAsync(cancelationToken);
            _serviceUrl = _foundryManager.Urls?.FirstOrDefault() ?? "http://127.0.0.1:55588";

            if (_catalogModels == null || !_catalogModels.Any())
            {
                var catalogModels = await _catalog.ListModelsAsync(cancelationToken);
                var catalogModelsList = new List<ModelDetails>();
                
                foreach (var model in catalogModels)
                {
                    foreach (var variant in model.Variants)
                    {
                        catalogModelsList.Add(ToModelDetails(model, variant));
                    }
                }
                
                _catalogModels = catalogModelsList;
            }

            var cachedModels = await _catalog.GetCachedModelsAsync(cancelationToken);

            List<ModelDetails> downloadedModels = [];

            // Add cached models to downloaded models list
            foreach (var cachedModel in cachedModels)
            {
                var catalogModel = _catalogModels.FirstOrDefault(m => m.Name == cachedModel.Alias);
                if (catalogModel != null)
                {
                    var clonedModel = new ModelDetails
                    {
                        Id = $"{UrlPrefix}{cachedModel.Id}",
                        Name = catalogModel.Name,
                        Url = catalogModel.Url,
                        Description = catalogModel.Description,
                        HardwareAccelerators = catalogModel.HardwareAccelerators,
                        Size = catalogModel.Size,
                        SupportedOnQualcomm = catalogModel.SupportedOnQualcomm,
                        License = catalogModel.License,
                        ProviderModelDetails = catalogModel.ProviderModelDetails
                    };
                    downloadedModels.Add(clonedModel);
                }
                else
                {
                    // Handle models not in catalog but cached
                    downloadedModels.Add(new ModelDetails()
                    {
                        Id = $"fl-{cachedModel.Alias}",
                        Name = cachedModel.Alias,
                        Url = $"{UrlPrefix}{cachedModel.Id}",
                        Description = $"{cachedModel.Alias} running locally with Foundry Local",
                        HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
                        SupportedOnQualcomm = true,
                        ProviderModelDetails = cachedModel
                    });
                }
            }

            _downloadedModels = downloadedModels;

            lock (_initLock)
            {
                _isInitialized = true;
            }
        }
        catch (Exception ex)
        {
            LogInitializationError(_logger, ex);
        }
    }

    private ModelDetails ToModelDetails(Model model, ModelVariant variant)
    {
        return new ModelDetails()
        {
            Id = $"fl-{variant.Alias}",
            Name = variant.Alias,
            Url = $"{UrlPrefix}{variant.Alias}",
            Description = $"{variant.Alias} running locally with Foundry Local",
            HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
            Size = (variant.Info.FileSizeMb ?? 0) * 1024L * 1024L,
            SupportedOnQualcomm = true,
            License = variant.Info.License?.ToLowerInvariant(),
            ProviderModelDetails = model // Store the Model instead of variant
        };
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to download model {modelName}")]
    private static partial void LogDownloadError(ILogger? logger, Exception ex, string modelName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to initialize FoundryLocalManager")]
    private static partial void LogInitializationError(ILogger? logger, Exception ex);

    public async Task<bool> IsAvailable()
    {
        try
        {
            await InitializeAsync();
            return _foundryManager != null && _catalog != null;
        }
        catch
        {
            return false;
        }
    }
}