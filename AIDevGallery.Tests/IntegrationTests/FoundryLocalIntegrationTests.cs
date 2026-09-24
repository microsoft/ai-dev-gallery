// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Integration tests for Foundry Local SDK contract validation.
// These tests verify that the Foundry Local SDK initializes correctly
// and returns model catalog data in the expected format.
//
// Purpose: Detect breaking changes in the Foundry Local SDK's API contract
// (e.g., field renames, type changes, structural modifications) that would
// silently break AIDG's integration.
//
// These tests use the SDK directly (bypassing FoundryClient) to validate
// the integration boundary independent of the application lifecycle.
//
// Note: Tests will be marked as Inconclusive if the SDK cannot initialize
// on the current platform (e.g., unsupported Windows version).
using AIDevGallery.ExternalModelUtils;
using AIDevGallery.ExternalModelUtils.FoundryLocal;
using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

[TestClass]
public class FoundryLocalIntegrationTests
{
    private const string TestCacheEnvironmentVariable = "AIDG_FOUNDRY_TEST_CACHE";
    private const string TestCacheProperty = "FoundryLocalCachePath";
    private const string AllowDownloadProperty = "FoundryLocalAllowDownload";
    private const string OnnxRuntimeGenAIModelPathProperty = "OnnxRuntimeGenAIModelPath";
    private static bool _sdkInitialized;
    private static bool _allowModelDownload;
    private static bool _usingExternalCache;
    private static string? _testCacheDir;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        _testCacheDir = context.Properties.Contains(TestCacheProperty)
            ? context.Properties[TestCacheProperty]?.ToString()
            : Environment.GetEnvironmentVariable(TestCacheEnvironmentVariable);
        _usingExternalCache = !string.IsNullOrWhiteSpace(_testCacheDir);
        _allowModelDownload = context.Properties.Contains(AllowDownloadProperty) &&
            bool.TryParse(context.Properties[AllowDownloadProperty]?.ToString(), out var allowDownload) &&
            allowDownload;

        if (string.IsNullOrWhiteSpace(_testCacheDir))
        {
            _testCacheDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests", "foundrycache");
        }

        Directory.CreateDirectory(_testCacheDir);

        if (FoundryLocalManager.IsInitialized)
        {
            // Already initialized by another test class in the same process
            _sdkInitialized = true;
            return;
        }

        try
        {
            var config = new Configuration
            {
                AppName = "AIDevGalleryTests",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                ModelCacheDir = _testCacheDir
            };

            await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);
        }
        catch (FoundryLocalException) when (FoundryLocalManager.IsInitialized)
        {
            // Race condition: another thread initialized the manager concurrently.
        }
        catch (Exception ex)
        {
            context.WriteLine($"FoundryLocalManager initialization failed: {ex.GetType().Name}: {ex.Message}");
        }

        _sdkInitialized = FoundryLocalManager.IsInitialized;
    }

    private static void EnsureSdkAvailable()
    {
        if (!_sdkInitialized)
        {
            Assert.Inconclusive("Foundry Local SDK did not initialize on this platform.");
        }
    }

    [TestMethod]
    public void SdkInitializesSuccessfully()
    {
        EnsureSdkAvailable();

        Assert.IsTrue(FoundryLocalManager.IsInitialized);
        Assert.IsNotNull(FoundryLocalManager.Instance);
    }

    [TestMethod]
    public void ExecutionProvidersCanBeDiscovered()
    {
        EnsureSdkAvailable();

        var executionProviders = FoundryLocalManager.Instance.DiscoverEps();
        Assert.IsNotNull(executionProviders);
    }

    [TestMethod]
    public async Task CatalogIsAccessibleAndReturnsModels()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        Assert.IsNotNull(catalog, "GetCatalogAsync should return a non-null catalog");

        var models = await catalog.ListModelsAsync();
        Assert.IsNotNull(models, "ListModelsAsync should return a non-null collection");
        Assert.IsTrue(models.Count > 0, "Catalog should contain at least one model");
    }

    [TestMethod]
    public async Task CatalogModelsHaveRequiredFields()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();

        foreach (var model in models)
        {
            // Validate fields that AIDG's ListCatalogModelsAsync relies on
            Assert.IsFalse(
                string.IsNullOrEmpty(model.Alias),
                "Model.Alias must not be null or empty");

            var info = model.Info;
            Assert.IsNotNull(
                info,
                $"Model '{model.Alias}' must have Info");

            Assert.IsFalse(
                string.IsNullOrEmpty(info.Name),
                $"Model '{model.Alias}' Info.Name must not be null or empty");

            Assert.IsFalse(
                string.IsNullOrEmpty(model.Id),
                $"Model '{model.Alias}' Id must not be null or empty");

            // DisplayName may be null (code falls back to Name), but should be string type
            // info.DisplayName ?? info.Name is the pattern used in ListCatalogModelsAsync
            var displayName = info.DisplayName ?? info.Name;
            Assert.IsFalse(
                string.IsNullOrEmpty(displayName),
                $"Model '{model.Alias}' must have a usable display name");
        }
    }

    [TestMethod]
    public async Task CatalogModelsConvertToFoundryCatalogModel()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();

        foreach (var model in models)
        {
            var info = model.Info;

            // Replicate the exact conversion from ListCatalogModelsAsync
            var catalogModel = new FoundryCatalogModel
            {
                Name = info.Name,
                DisplayName = info.DisplayName ?? info.Name,
                Alias = model.Alias,
                FileSizeMb = info.FileSizeMb ?? 0,
                License = info.License ?? string.Empty,
                ModelId = model.Id,
                Runtime = info.Runtime,
                Task = info.Task
            };

            Assert.IsFalse(
                string.IsNullOrEmpty(catalogModel.Name),
                $"FoundryCatalogModel.Name should not be empty for alias '{model.Alias}'");
            Assert.IsFalse(
                string.IsNullOrEmpty(catalogModel.DisplayName),
                $"FoundryCatalogModel.DisplayName should not be empty for alias '{model.Alias}'");
            Assert.IsFalse(
                string.IsNullOrEmpty(catalogModel.Alias),
                $"FoundryCatalogModel.Alias should not be empty");
            Assert.IsFalse(
                string.IsNullOrEmpty(catalogModel.ModelId),
                $"FoundryCatalogModel.ModelId should not be empty for alias '{model.Alias}'");
            Assert.IsTrue(
                catalogModel.FileSizeMb >= 0,
                $"FoundryCatalogModel.FileSizeMb should be non-negative for alias '{model.Alias}'");
        }
    }

    [TestMethod]
    public async Task CatalogModelsProduceValidModelDetails()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var provider = FoundryLocalModelProvider.Instance;

        foreach (var model in models)
        {
            var info = model.Info;

            var catalogModel = new FoundryCatalogModel
            {
                Name = info.Name,
                DisplayName = info.DisplayName ?? info.Name,
                Alias = model.Alias,
                FileSizeMb = info.FileSizeMb ?? 0,
                License = info.License ?? string.Empty,
                ModelId = model.Id,
                Runtime = info.Runtime,
                Task = info.Task
            };

            // Replicate the exact ModelDetails conversion from ToModelDetails
            var modelDetails = new ModelDetails
            {
                Id = $"fl-{catalogModel.Alias}",
                Name = catalogModel.DisplayName,
                Url = $"{provider.UrlPrefix}{catalogModel.Alias}",
                Description = $"{catalogModel.DisplayName} is running locally with Foundry Local",
                HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
                Size = catalogModel.FileSizeMb * 1024 * 1024,
                SupportedOnQualcomm = true,
                License = catalogModel.License?.ToLowerInvariant(),
                ProviderModelDetails = catalogModel
            };

            // Validate critical downstream fields
            Assert.IsFalse(
                string.IsNullOrEmpty(modelDetails.Name),
                $"ModelDetails.Name should not be empty for alias '{model.Alias}'");
            Assert.IsTrue(
                modelDetails.Url.StartsWith("fl://", StringComparison.Ordinal),
                $"ModelDetails.Url should start with 'fl://' for alias '{model.Alias}', got: '{modelDetails.Url}'");
            Assert.IsTrue(
                modelDetails.Size >= 0,
                $"ModelDetails.Size should be non-negative for alias '{model.Alias}'");
            Assert.IsNotNull(
                modelDetails.ProviderModelDetails,
                $"ModelDetails.ProviderModelDetails should not be null for alias '{model.Alias}'");
            Assert.IsInstanceOfType(
                modelDetails.ProviderModelDetails,
                typeof(FoundryCatalogModel),
                $"ModelDetails.ProviderModelDetails should be FoundryCatalogModel for alias '{model.Alias}'");

            // Verify the ProviderModelDetails round-trips correctly
            var roundTripped = (FoundryCatalogModel)modelDetails.ProviderModelDetails;
            Assert.AreEqual(catalogModel.Alias, roundTripped.Alias);
            Assert.AreEqual(catalogModel.ModelId, roundTripped.ModelId);
        }
    }

    [TestMethod]
    public async Task CatalogModelsHaveValidTaskTypes()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();

        // At least some models should have the chat-completion task
        // which is the primary use case for AIDG
        var chatModelCount = models.Count(m =>
            m.Info.Task == ModelTaskTypes.ChatCompletion);

        Assert.IsTrue(
            chatModelCount > 0,
            "Catalog should contain at least one model with 'chat-completion' task type");
    }

    [TestMethod]
    public async Task CachedModelsQueryDoesNotThrow()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();

        // GetCachedModelsAsync should work regardless of whether any models are downloaded
        var cachedModels = await catalog.GetCachedModelsAsync();
        Assert.IsNotNull(cachedModels, "GetCachedModelsAsync should return a non-null collection");

        // Cached models (if any) should have valid fields
        foreach (var variant in cachedModels)
        {
            Assert.IsFalse(
                string.IsNullOrEmpty(variant.Alias),
                "Cached model variant should have a non-empty Alias");
            Assert.IsFalse(
                string.IsNullOrEmpty(variant.Id),
                "Cached model variant should have a non-empty Id");
            Assert.IsNotNull(
                variant.Info,
                $"Cached model '{variant.Alias}' should have Info");
            Assert.IsFalse(
                string.IsNullOrEmpty(variant.Info.Name),
                $"Cached model '{variant.Alias}' Info.Name should not be empty");
        }
    }

    [TestMethod]
    public async Task GetModelByAliasReturnsValidModel()
    {
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();

        if (models.Count == 0)
        {
            Assert.Inconclusive("No models in catalog to test GetModelAsync");
        }

        // Pick the first model's alias and verify we can look it up
        var firstAlias = models[0].Alias;
        var lookedUp = await catalog.GetModelAsync(firstAlias);

        Assert.IsNotNull(lookedUp, $"GetModelAsync('{firstAlias}') should return a non-null model");
        Assert.AreEqual(
            firstAlias,
            lookedUp.Alias,
            "Looked-up model Alias should match the requested alias");
    }

    [TestMethod]
    public async Task CachedChatModelCanStreamResponse()
    {
        EnsureSdkAvailable();

        if (!_usingExternalCache)
        {
            Assert.Inconclusive($"Set the {TestCacheProperty} test parameter or {TestCacheEnvironmentVariable} to run cached-model inference.");
        }

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var model = await GetCachedChatModelAsync(catalog);
        if (model == null)
        {
            Assert.Inconclusive("No cached chat-completion model is available.");
            return;
        }

        await model.LoadAsync();
        try
        {
            var responseText = await StreamShortFoundryResponseAsync(model);
            Assert.IsFalse(string.IsNullOrWhiteSpace(responseText));
        }
        finally
        {
            if (await model.IsLoadedAsync())
            {
                await model.UnloadAsync();
            }
        }
    }

    [TestMethod]
    public async Task SmallCpuChatModelCanDownloadAndStreamResponse()
    {
        EnsureSdkAvailable();

        if (!_allowModelDownload)
        {
            Assert.Inconclusive($"Set the {AllowDownloadProperty} test parameter to true to run model download and inference.");
        }

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var model = (await catalog.ListModelsAsync())
            .Where(IsCpuChatModel)
            .OrderBy(model => model.Info.FileSizeMb ?? int.MaxValue)
            .FirstOrDefault();
        Assert.IsNotNull(model, "No CPU chat-completion model is available.");

        var wasCached = await model.IsCachedAsync();
        try
        {
            if (!wasCached)
            {
                await model.DownloadAsync();
            }

            await model.LoadAsync();
            var responseText = await StreamShortFoundryResponseAsync(model);
            Assert.IsFalse(string.IsNullOrWhiteSpace(responseText));
        }
        finally
        {
            if (await model.IsLoadedAsync())
            {
                await model.UnloadAsync();
            }

            if (!wasCached && await model.IsCachedAsync())
            {
                await model.RemoveFromCacheAsync();
            }
        }
    }

    [TestMethod]
    public async Task FoundryAndOnnxRuntimeGenAIModelsCanStreamInSameProcess()
    {
        EnsureSdkAvailable();

        if (!_usingExternalCache ||
            !TestContext.Properties.Contains(OnnxRuntimeGenAIModelPathProperty) ||
            TestContext.Properties[OnnxRuntimeGenAIModelPathProperty]?.ToString() is not { Length: > 0 } modelPath)
        {
            Assert.Inconclusive(
                $"Set {TestCacheProperty}/{TestCacheEnvironmentVariable} and {OnnxRuntimeGenAIModelPathProperty} to run shared-runtime inference.");
            return;
        }

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var foundryModel = await GetCachedChatModelAsync(catalog);
        if (foundryModel == null)
        {
            Assert.Inconclusive("No cached Foundry Local chat-completion model is available.");
            return;
        }

        await foundryModel.LoadAsync();
        try
        {
            var foundryResponse = await StreamShortFoundryResponseAsync(foundryModel);
            Assert.IsFalse(string.IsNullOrWhiteSpace(foundryResponse));

            using var onnxRuntimeGenAIClient = await OnnxRuntimeGenAIChatClientFactory.CreateAsync(modelPath);
            Assert.IsNotNull(onnxRuntimeGenAIClient);

            var onnxRuntimeGenAIResponse = await StreamShortResponseAsync(onnxRuntimeGenAIClient);
            Assert.IsFalse(string.IsNullOrWhiteSpace(onnxRuntimeGenAIResponse));
            Assert.IsTrue(await foundryModel.IsLoadedAsync(), "The Foundry Local model should remain loaded during ORT GenAI inference.");
        }
        finally
        {
            if (await foundryModel.IsLoadedAsync())
            {
                await foundryModel.UnloadAsync();
            }
        }
    }

    private static bool IsCpuChatModel(IModel model)
    {
        return model.Info.Task == ModelTaskTypes.ChatCompletion &&
            model.Info.Runtime?.ExecutionProvider.Contains("CPU", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<IModel?> GetCachedChatModelAsync(ICatalog catalog)
    {
        var cachedVariant = (await catalog.GetCachedModelsAsync())
            .FirstOrDefault(model => model.Info.Task == ModelTaskTypes.ChatCompletion);
        if (cachedVariant == null)
        {
            return null;
        }

        var model = await catalog.GetModelAsync(cachedVariant.Alias);
        if (model == null)
        {
            return null;
        }

        if (model.Id != cachedVariant.Id)
        {
            var selectedVariant = model.Variants.FirstOrDefault(variant => variant.Id == cachedVariant.Id);
            if (selectedVariant == null)
            {
                return null;
            }

            model.SelectVariant(selectedVariant);
        }

        var executionProvider = model.Info.Runtime?.ExecutionProvider;
        if (!string.IsNullOrWhiteSpace(executionProvider))
        {
            var result = await FoundryLocalManager.Instance.DownloadAndRegisterEpsAsync([executionProvider]);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to register {executionProvider}: {result.Status}");
            }
        }

        return model;
    }

    private static async Task<string> StreamShortFoundryResponseAsync(IModel model)
    {
        using var chatClient = new FoundryLocalChatClientAdapter(model, model.Id, (int?)model.Info.MaxOutputTokens);
        return await StreamShortResponseAsync(chatClient);
    }

    private static async Task<string> StreamShortResponseAsync(IChatClient chatClient)
    {
        var responseText = string.Empty;
        await foreach (var update in chatClient.GetStreamingResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Reply with OK only.")],
            new ChatOptions { MaxOutputTokens = 16 }))
        {
            responseText += update.Text;
        }

        return responseText;
    }
}