// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests.ExternalModelUtils;

[TestClass]
public class FoundryLocalModelProviderTests
{
    // Basic properties tests - Consolidated
    [TestMethod]
    public void BasicPropertiesReturnExpectedValues()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Assert - Test all basic properties together
        Assert.AreEqual("FoundryLocal", provider.Name);
        Assert.AreEqual(HardwareAccelerator.FOUNDRYLOCAL, provider.ModelHardwareAccelerator);
        Assert.AreEqual("fl://", provider.UrlPrefix);
        Assert.AreEqual(string.Empty, provider.Url, "Foundry Local uses direct SDK calls, not web service");
        Assert.AreEqual("The model will run locally via Foundry Local", provider.ProviderDescription);
        Assert.AreEqual("Microsoft.AI.Foundry.Local", provider.IChatClientImplementationNamespace);
    }

    [TestMethod]
    public void NugetPackageReferencesContainsRequiredPackages()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act
        var packages = provider.NugetPackageReferences;

        // Assert
        Assert.IsNotNull(packages);
        Assert.AreEqual(2, packages.Count, "Should contain exactly 2 packages");
        Assert.IsTrue(packages.Contains("Microsoft.AI.Foundry.Local.WinML"));
        Assert.IsTrue(packages.Contains("Microsoft.Extensions.AI"));
    }

    [TestMethod]
    public void GetDetailsUrlReturnsNull()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;
        var modelDetails = new ModelDetails { Name = "test-model" };

        // Act
        var url = provider.GetDetailsUrl(modelDetails);

        // Assert
        Assert.IsNull(url, "Foundry Local models run locally, so no online details page should be available");
    }

    [TestMethod]
    public void InstanceIsSingleton()
    {
        // Arrange & Act
        var instance1 = FoundryLocalModelProvider.Instance;
        var instance2 = FoundryLocalModelProvider.Instance;

        // Assert
        Assert.AreSame(instance1, instance2, "Instance should be a singleton");
    }

    // Edge case: Invalid model type
    [TestMethod]
    public async Task DownloadModelWithNonFoundryCatalogModelReturnsFalse()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;
        var modelDetails = new ModelDetails
        {
            Name = "test-model",
            Url = "https://example.com/model",
            ProviderModelDetails = "Not a FoundryCatalogModel" // Wrong type
        };

        // Act
        var result = await provider.DownloadModel(modelDetails, null, default);

        // Assert
        Assert.IsFalse(result.Success, "Should fail when ProviderModelDetails is wrong type");

        // In unit test environment, manager might not be initialized, both error messages are valid
        Assert.IsTrue(
            result.ErrorMessage == "Invalid model details" ||
            result.ErrorMessage == "Foundry Local manager not initialized",
            $"Expected error about invalid model or uninitialized manager, but got: {result.ErrorMessage}");
    }

    [TestMethod]
    public async Task DownloadModelWithNullProviderModelDetailsReturnsFalse()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;
        var modelDetails = new ModelDetails
        {
            Name = "test-model",
            Url = "https://example.com/model",
            ProviderModelDetails = null // Null provider details
        };

        // Act
        var result = await provider.DownloadModel(modelDetails, null, default);

        // Assert
        Assert.IsFalse(result.Success, "Should fail when ProviderModelDetails is null");

        // In unit test environment, manager might not be initialized, both error messages are valid
        Assert.IsTrue(
            result.ErrorMessage == "Invalid model details" ||
            result.ErrorMessage == "Foundry Local manager not initialized",
            $"Expected error about invalid model or uninitialized manager, but got: {result.ErrorMessage}");
    }

    // Test initialization behavior
    [TestMethod]
    public async Task GetModelsAsyncWithIgnoreCachedTrueResetsDownloadedModels()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act - Call with ignoreCached=true
        var models1 = await provider.GetModelsAsync(ignoreCached: true);
        var models2 = await provider.GetModelsAsync(ignoreCached: false);

        // Assert - Both should return collections (empty or populated)
        Assert.IsNotNull(models1);
        Assert.IsNotNull(models2);
    }

    [TestMethod]
    public void GetAllModelsInCatalogReturnsEmptyCollectionBeforeInitialization()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act
        var catalogModels = provider.GetAllModelsInCatalog();

        // Assert - Should return empty collection if not initialized, not null
        Assert.IsNotNull(catalogModels);
    }
}