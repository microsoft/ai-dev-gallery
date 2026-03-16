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
using Microsoft.AI.Foundry.Local;
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
    private static bool _sdkInitialized;
    private static string? _testCacheDir;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests", "foundrycache");
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

            var variant = model.SelectedVariant;
            Assert.IsNotNull(
                variant,
                $"Model '{model.Alias}' must have a SelectedVariant");

            var info = variant.Info;
            Assert.IsNotNull(
                info,
                $"Model '{model.Alias}' SelectedVariant must have Info");

            Assert.IsFalse(
                string.IsNullOrEmpty(info.Name),
                $"Model '{model.Alias}' Info.Name must not be null or empty");

            Assert.IsFalse(
                string.IsNullOrEmpty(variant.Id),
                $"Model '{model.Alias}' SelectedVariant.Id must not be null or empty");

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
            var variant = model.SelectedVariant;
            var info = variant.Info;

            // Replicate the exact conversion from ListCatalogModelsAsync
            var catalogModel = new FoundryCatalogModel
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
            var variant = model.SelectedVariant;
            var info = variant.Info;

            var catalogModel = new FoundryCatalogModel
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
            m.SelectedVariant.Info.Task == ModelTaskTypes.ChatCompletion);

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
}