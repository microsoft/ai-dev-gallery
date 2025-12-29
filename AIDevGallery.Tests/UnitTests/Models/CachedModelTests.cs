// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class CachedModelTests
{
    [TestMethod]
    public void CachedModel_WithFoundryLocalUrl_SetsSourceToFoundryLocal()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "Phi-3.5",
            Url = "fl://phi-3.5-mini"
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/path/to/model", false, 1024);

        // Assert
        Assert.AreEqual(CachedModelSource.FoundryLocal, cachedModel.Source);
        Assert.AreEqual("fl://phi-3.5-mini", cachedModel.Url);
    }

    [TestMethod]
    public void CachedModel_WithGitHubUrl_SetsSourceToGitHub()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "Test Model",
            Url = "https://github.com/user/repo/model.onnx"
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/path/to/model", true, 2048);

        // Assert
        Assert.AreEqual(CachedModelSource.GitHub, cachedModel.Source);
    }

    [TestMethod]
    public void CachedModel_WithLocalUrl_SetsSourceToLocal()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "Local Model",
            Url = "local://C:/models/model.onnx"
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "C:/models/model.onnx", true, 512);

        // Assert
        Assert.AreEqual(CachedModelSource.Local, cachedModel.Source);
    }

    [TestMethod]
    public void CachedModel_WithHuggingFaceUrl_SetsSourceToHuggingFace()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "HF Model",
            Url = "microsoft/phi-2"
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/path/to/model", false, 4096);

        // Assert
        Assert.AreEqual(CachedModelSource.HuggingFace, cachedModel.Source);
    }

    [TestMethod]
    public void CachedModel_Constructor_SetsAllProperties()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "Test Model",
            Url = "fl://test-model"
        };
        var path = "/test/path";
        var isFile = true;
        var modelSize = 12345L;

        // Act
        var cachedModel = new CachedModel(modelDetails, path, isFile, modelSize);

        // Assert
        Assert.AreEqual(modelDetails, cachedModel.Details);
        Assert.AreEqual(path, cachedModel.Path);
        Assert.AreEqual(isFile, cachedModel.IsFile);
        Assert.AreEqual(modelSize, cachedModel.ModelSize);
        Assert.IsTrue(cachedModel.DateTimeCached <= DateTime.Now);
        Assert.IsTrue(cachedModel.DateTimeCached > DateTime.Now.AddSeconds(-5));
    }

    [TestMethod]
    public void CachedModel_FoundryLocalUrl_CaseInsensitive()
    {
        // Arrange - Test case insensitivity
        var testUrls = new[] { "fl://model", "FL://model", "Fl://model" };

        foreach (var url in testUrls)
        {
            var modelDetails = new ModelDetails
            {
                Name = "Test",
                Url = url
            };

            // Act
            var cachedModel = new CachedModel(modelDetails, "/path", false, 100);

            // Assert
            Assert.AreEqual(CachedModelSource.FoundryLocal, cachedModel.Source, 
                $"URL '{url}' should be recognized as FoundryLocal");
        }
    }

    [TestMethod]
    public void CachedModelSource_FoundryLocal_EnumExists()
    {
        // Verify the new enum value exists
        var foundryLocalValue = CachedModelSource.FoundryLocal;
        
        Assert.IsTrue(Enum.IsDefined(typeof(CachedModelSource), foundryLocalValue));
        Assert.AreEqual("FoundryLocal", foundryLocalValue.ToString());
    }

    [TestMethod]
    public void CachedModelSource_AllValues_AreDefined()
    {
        // Ensure all expected sources are defined
        var expectedSources = new[]
        {
            CachedModelSource.GitHub,
            CachedModelSource.HuggingFace,
            CachedModelSource.Local,
            CachedModelSource.FoundryLocal
        };

        foreach (var source in expectedSources)
        {
            Assert.IsTrue(Enum.IsDefined(typeof(CachedModelSource), source),
                $"CachedModelSource.{source} should be defined");
        }
    }
}
