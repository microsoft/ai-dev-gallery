// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests.ExternalModelUtils;

[TestClass]
public class FoundryLocalModelProviderTests
{
    [TestMethod]
    public void NameReturnsFoundryLocal()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual("FoundryLocal", provider.Name);
    }

    [TestMethod]
    public void ModelHardwareAcceleratorReturnsFoundryLocal()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual(HardwareAccelerator.FOUNDRYLOCAL, provider.ModelHardwareAccelerator);
    }

    [TestMethod]
    public void UrlPrefixReturnsCorrectPrefix()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual("fl://", provider.UrlPrefix);
    }

    [TestMethod]
    public void ProviderDescriptionReturnsCorrectDescription()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual("The model will run locally via Foundry Local", provider.ProviderDescription);
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

    [TestMethod]
    public void IChatClientImplementationNamespaceReturnsCorrectNamespace()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual("Microsoft.AI.Foundry.Local", provider.IChatClientImplementationNamespace);
    }

    [TestMethod]
    public void UrlReturnsEmptyString()
    {
        // Arrange
        var provider = FoundryLocalModelProvider.Instance;

        // Act & Assert
        Assert.AreEqual(string.Empty, provider.Url, "Foundry Local uses direct SDK calls, not web service");
    }
}