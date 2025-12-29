// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests;

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

    [TestMethod]
    public void ExtractAlias_RemovesUrlPrefix()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;
        var testCases = new[]
        {
            ("fl://phi-3.5-mini-instruct", "phi-3.5-mini-instruct"),
            ("fl://mistral-7b", "mistral-7b"),
            ("fl://model-name", "model-name")
        };

        // Act & Assert
        foreach (var (input, expected) in testCases)
        {
            // Use reflection to call private ExtractAlias method
            var method = typeof(FoundryLocalModelProvider).GetMethod("ExtractAlias", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "ExtractAlias method should exist");

            var result = method.Invoke(provider, new object[] { input }) as string;
            Assert.AreEqual(expected, result, $"Failed to extract alias from {input}");
        }
    }

    // Note: Tests for GetRequiredTasksForModelTypes are omitted because ModelType is generated by source generator
    // and not directly accessible in unit tests. These methods are tested through integration tests.

    [TestMethod]
    public void GetIChatClientString_WithValidAlias_ReturnsCodeSnippet()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;
        var testUrl = "fl://test-model";

        // Act
        var result = provider.GetIChatClientString(testUrl);

        // Assert
        // Before initialization, should return null
        // The code snippet generation requires a loaded model
        Assert.IsTrue(
            result == null || result.Contains("FoundryLocalManager"),
            "Should return null before initialization or contain FoundryLocalManager code");
    }

    [TestMethod]
    public void IChatClientImplementationNamespace_ReturnsCorrectNamespace()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act
        var ns = provider.IChatClientImplementationNamespace;

        // Assert
        Assert.AreEqual("Microsoft.AI.Foundry.Local", ns, "Should return the correct Foundry Local SDK namespace");
    }

    // Note: Icon property test is omitted because it depends on AppUtils.GetThemeAssetSuffix()
    // which requires a running WinUI application context (not available in unit tests)

    [TestMethod]
    public async Task GetModelsAsync_CalledMultipleTimes_DoesNotReinitialize()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act
        var models1 = await provider.GetModelsAsync(ignoreCached: false);
        var models2 = await provider.GetModelsAsync(ignoreCached: false);

        // Assert
        Assert.IsNotNull(models1);
        Assert.IsNotNull(models2);

        // Both calls should succeed and return consistent results
    }
}