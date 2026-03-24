// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class MiniMaxModelProviderTests
{
    [TestMethod]
    public void BasicPropertiesReturnExpectedValues()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;

        // Assert - Test all basic properties together
        Assert.AreEqual("MiniMax", provider.Name);
        Assert.AreEqual(HardwareAccelerator.MINIMAX, provider.ModelHardwareAccelerator);
        Assert.AreEqual("minimax://", provider.UrlPrefix);
        Assert.AreEqual("https://api.minimax.io/v1", provider.Url);
        Assert.AreEqual("The model will run on the cloud via MiniMax", provider.ProviderDescription);
        Assert.AreEqual("OpenAI", provider.IChatClientImplementationNamespace);
    }

    [TestMethod]
    public void NugetPackageReferencesContainsRequiredPackages()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;

        // Act
        var packages = provider.NugetPackageReferences;

        // Assert
        Assert.IsNotNull(packages);
        Assert.AreEqual(1, packages.Count, "Should contain exactly 1 package");
        Assert.IsTrue(packages.Contains("Microsoft.Extensions.AI.OpenAI"));
    }

    [TestMethod]
    public void GetDetailsUrlReturnsDocumentationPage()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var modelDetails = new ModelDetails { Name = "MiniMax-M2.7" };

        // Act
        var url = provider.GetDetailsUrl(modelDetails);

        // Assert
        Assert.IsNotNull(url);
        Assert.AreEqual("https://platform.minimaxi.com/document/Models", url);
    }

    [TestMethod]
    public void InstanceIsSingleton()
    {
        // Arrange & Act
        var instance1 = MiniMaxModelProvider.Instance;
        var instance2 = MiniMaxModelProvider.Instance;

        // Assert
        Assert.AreSame(instance1, instance2, "Instance should be a singleton");
    }

    [TestMethod]
    public async Task GetModelsAsyncWithoutApiKeyReturnsEmpty()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var originalKey = MiniMaxModelProvider.MiniMaxKey;

        try
        {
            // Ensure no API key is set
            MiniMaxModelProvider.MiniMaxKey = null;

            // Act
            var models = await provider.GetModelsAsync();

            // Assert
            Assert.IsNotNull(models);
            Assert.IsFalse(models.Any(), "Should return empty when no API key is set");
        }
        finally
        {
            // Restore original key
            if (originalKey != null)
            {
                MiniMaxModelProvider.MiniMaxKey = originalKey;
            }
        }
    }

    [TestMethod]
    public async Task GetModelsAsyncWithApiKeyReturnsKnownModels()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var originalKey = MiniMaxModelProvider.MiniMaxKey;

        try
        {
            // Set a test API key
            MiniMaxModelProvider.MiniMaxKey = "test-api-key";

            // Act
            var models = await provider.GetModelsAsync();
            var modelList = models.ToList();

            // Assert
            Assert.IsNotNull(modelList);
            Assert.AreEqual(3, modelList.Count, "Should return 3 known MiniMax models");

            // Verify model IDs and URL prefixes
            Assert.IsTrue(modelList.Any(m => m.Name == "MiniMax-M2.7"), "Should include M2.7");
            Assert.IsTrue(modelList.Any(m => m.Name == "MiniMax-M2.5"), "Should include M2.5");
            Assert.IsTrue(modelList.Any(m => m.Name == "MiniMax-M2.5-highspeed"), "Should include M2.5-highspeed");

            // Verify all models use the minimax URL prefix
            foreach (var model in modelList)
            {
                Assert.IsTrue(model.Url.StartsWith("minimax://"), $"Model URL should start with minimax://, got: {model.Url}");
                Assert.IsTrue(model.Id.StartsWith("minimax-"), $"Model ID should start with minimax-, got: {model.Id}");
                Assert.AreEqual(HardwareAccelerator.MINIMAX, model.HardwareAccelerators.First());
            }
        }
        finally
        {
            // Restore original key
            if (originalKey != null)
            {
                MiniMaxModelProvider.MiniMaxKey = originalKey;
            }
            else
            {
                MiniMaxModelProvider.MiniMaxKey = null;
            }
        }
    }

    [TestMethod]
    public void GetIChatClientWithoutApiKeyReturnsNull()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var originalKey = MiniMaxModelProvider.MiniMaxKey;

        try
        {
            MiniMaxModelProvider.MiniMaxKey = null;

            // Act
            var client = provider.GetIChatClient("minimax://MiniMax-M2.7");

            // Assert
            Assert.IsNull(client, "Should return null when no API key is set");
        }
        finally
        {
            if (originalKey != null)
            {
                MiniMaxModelProvider.MiniMaxKey = originalKey;
            }
        }
    }

    [TestMethod]
    public void GetIChatClientWithEmptyModelIdReturnsNull()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;

        // Act
        var client = provider.GetIChatClient("minimax://");

        // Assert
        Assert.IsNull(client, "Should return null for empty model ID");
    }

    [TestMethod]
    public void GetIChatClientStringReturnsValidCodeSnippet()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var url = "minimax://MiniMax-M2.7";

        // Act
        var result = provider.GetIChatClientString(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("OpenAIClient"), "Code snippet should reference OpenAIClient");
        Assert.IsTrue(result.Contains("api.minimax.io/v1"), "Code snippet should reference MiniMax API URL");
        Assert.IsTrue(result.Contains("MiniMax-M2.7"), "Code snippet should reference the model ID");
        Assert.IsTrue(result.Contains("AsIChatClient"), "Code snippet should include AsIChatClient()");
    }

    [TestMethod]
    public void ClearCachedModelsDoesNotThrow()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;

        // Act & Assert - Should not throw
        provider.ClearCachedModels();
    }

    [TestMethod]
    public async Task GetModelsAsyncWithIgnoreCachedReturnsConsistentResults()
    {
        // Arrange
        var provider = MiniMaxModelProvider.Instance;
        var originalKey = MiniMaxModelProvider.MiniMaxKey;

        try
        {
            MiniMaxModelProvider.MiniMaxKey = "test-api-key";

            // Act
            var models1 = await provider.GetModelsAsync(ignoreCached: false);
            var models2 = await provider.GetModelsAsync(ignoreCached: true);

            // Assert
            Assert.IsNotNull(models1);
            Assert.IsNotNull(models2);
            Assert.AreEqual(models1.Count(), models2.Count(), "Both calls should return same number of models");
        }
        finally
        {
            if (originalKey != null)
            {
                MiniMaxModelProvider.MiniMaxKey = originalKey;
            }
            else
            {
                MiniMaxModelProvider.MiniMaxKey = null;
            }
        }
    }
}
