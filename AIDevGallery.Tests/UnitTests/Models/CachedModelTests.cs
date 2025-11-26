// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Tests.UnitTests.Models;

[TestClass]
public class CachedModelTests
{
    [TestMethod]
    public void Constructor_WithGitHubUrl_SetsSourceToGitHub()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "https://github.com/user/repo/model.onnx",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/path", true, 1024000);

        // Assert
        Assert.AreEqual(CachedModelSource.GitHub, cachedModel.Source);
        Assert.AreEqual("https://github.com/user/repo/model.onnx", cachedModel.Url);
    }

    [TestMethod]
    public void Constructor_WithHuggingFaceUrl_SetsSourceToHuggingFace()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "microsoft/model-name",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/path", true, 1024000);

        // Assert
        Assert.AreEqual(CachedModelSource.HuggingFace, cachedModel.Source);
        Assert.IsTrue(cachedModel.Url.Contains("huggingface.co"));
    }

    [TestMethod]
    public void Constructor_WithLocalUrl_SetsSourceToLocal()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "local://path/to/model",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/path", true, 1024000);

        // Assert
        Assert.AreEqual(CachedModelSource.Local, cachedModel.Source);
        Assert.AreEqual("local://path/to/model", cachedModel.Url);
    }

    [TestMethod]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "https://github.com/test/model.onnx",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var before = DateTime.Now;
        var cachedModel = new CachedModel(modelDetails, "/cache/path/model.onnx", true, 2048000);
        var after = DateTime.Now;

        // Assert
        Assert.AreEqual(modelDetails, cachedModel.Details);
        Assert.AreEqual("/cache/path/model.onnx", cachedModel.Path);
        Assert.IsTrue(cachedModel.IsFile);
        Assert.AreEqual(2048000, cachedModel.ModelSize);
        Assert.IsTrue(cachedModel.DateTimeCached >= before && cachedModel.DateTimeCached <= after);
    }

    [TestMethod]
    public void Constructor_WithDirectory_IsFileFalse()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "https://github.com/test/model",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/directory", false, 512000);

        // Assert
        Assert.IsFalse(cachedModel.IsFile);
        Assert.AreEqual("/cache/directory", cachedModel.Path);
    }

    [TestMethod]
    public void Constructor_GitHubUrl_CaseInsensitive()
    {
        // Arrange - test with different casing
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "HTTPS://GITHUB.COM/user/repo/model.onnx",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/path", true, 1024000);

        // Assert
        Assert.AreEqual(CachedModelSource.GitHub, cachedModel.Source);
    }

    [TestMethod]
    public void Constructor_LocalUrl_CaseInsensitive()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "LOCAL://path/to/model",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };

        // Act
        var cachedModel = new CachedModel(modelDetails, "/cache/path", true, 1024000);

        // Assert
        Assert.AreEqual(CachedModelSource.Local, cachedModel.Source);
    }
}