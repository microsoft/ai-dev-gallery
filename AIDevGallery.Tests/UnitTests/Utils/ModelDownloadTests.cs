// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class ModelDownloadTests
{
    #region Path Traversal Protection Tests

    [TestMethod]
    public void IsPathWithinDirectory_ValidSubPath_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(basePath, "model.onnx");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_ValidNestedSubPath_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(basePath, "subfolder", "deep", "model.onnx");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_PathTraversalWithDotDot_ReturnsFalse()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(basePath, "..", "evil", "malware.exe");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_AbsolutePathOutsideBase_ReturnsFalse()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(Path.GetTempPath(), "other", "file.txt");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_SimilarPrefixButDifferentFolder_ReturnsFalse()
    {
        // Arrange - This tests the trailing separator fix
        // Without proper handling, "models_evil" would match "models" prefix
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(Path.GetTempPath(), "cache", "models_evil", "file.txt");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_BasePathWithTrailingSeparator_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models") + Path.DirectorySeparatorChar;
        var filePath = Path.Combine(Path.GetTempPath(), "cache", "models", "model.onnx");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPathWithinDirectory_BasePathWithoutTrailingSeparator_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "cache", "models");
        var filePath = Path.Combine(basePath, "model.onnx");

        // Act
        var result = ModelDownload.IsPathWithinDirectory(basePath, filePath);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region Model Download Tests

    [TestMethod]
    public void ModelDownloadEventArgsWithWarningMessageStoresWarning()
    {
        // Arrange
        var warningMessage = "Model loaded but with minor issues";
        var eventArgs = new ModelDownloadEventArgs
        {
            Progress = 1.0f,
            Status = DownloadStatus.Completed,
            WarningMessage = warningMessage
        };

        // Act & Assert
        Assert.AreEqual(warningMessage, eventArgs.WarningMessage);
        Assert.AreEqual(1.0f, eventArgs.Progress);
        Assert.AreEqual(DownloadStatus.Completed, eventArgs.Status);
    }

    [TestMethod]
    public void ModelDownloadEventArgsWithoutWarningMessageIsNull()
    {
        // Arrange
        var eventArgs = new ModelDownloadEventArgs
        {
            Progress = 0.5f,
            Status = DownloadStatus.InProgress
        };

        // Act & Assert
        Assert.IsNull(eventArgs.WarningMessage);
    }

    [TestMethod]
    public void DownloadStatusHasExpectedValues()
    {
        // This test ensures the DownloadStatus enum has expected values
        // Critical for FoundryLocal integration which uses these states

        // Assert
        Assert.IsTrue(Enum.IsDefined(DownloadStatus.Waiting));
        Assert.IsTrue(Enum.IsDefined(DownloadStatus.InProgress));
        Assert.IsTrue(Enum.IsDefined(DownloadStatus.Completed));
        Assert.IsTrue(Enum.IsDefined(DownloadStatus.Canceled));
    }

    [TestMethod]
    public void FoundryLocalModelDownloadIsSubclassOfModelDownload()
    {
        // Arrange
        var foundryDownloadType = Type.GetType("AIDevGallery.Utils.FoundryLocalModelDownload, AIDevGallery");
        var modelDownloadType = typeof(ModelDownload);

        // Assert
        Assert.IsNotNull(foundryDownloadType, "FoundryLocalModelDownload type should exist");
        Assert.IsTrue(
            modelDownloadType.IsAssignableFrom(foundryDownloadType),
            "FoundryLocalModelDownload should inherit from ModelDownload");
    }

    [TestMethod]
    public void FoundryLocalModelDownloadConstructorInitializesWithModelDetails()
    {
        // Arrange
        var modelDetails = new ModelDetails
        {
            Name = "test-model",
            Url = "fl://test-model"
        };

        // Act
        var download = CreateFoundryLocalModelDownload(modelDetails);

        // Assert
        Assert.IsNotNull(download);
        var details = GetProperty<ModelDetails>(download, "Details");
        Assert.AreEqual(modelDetails.Name, details.Name);
        Assert.AreEqual(modelDetails.Url, details.Url);
    }

    [TestMethod]
    public void ModelDownloadWarningMessagePropertyExists()
    {
        // Verify that the WarningMessage property was added to ModelDownload base class
        var modelDownloadType = typeof(ModelDownload);
        var warningProperty = modelDownloadType.GetProperty("WarningMessage");

        Assert.IsNotNull(warningProperty, "WarningMessage property should exist on ModelDownload");
        Assert.AreEqual(typeof(string), warningProperty.PropertyType);
    }

    private static object CreateFoundryLocalModelDownload(ModelDetails modelDetails)
    {
        var type = Type.GetType("AIDevGallery.Utils.FoundryLocalModelDownload, AIDevGallery");
        Assert.IsNotNull(type, "FoundryLocalModelDownload type not found");

        var constructor = type.GetConstructor(new[] { typeof(ModelDetails) });
        Assert.IsNotNull(constructor, "Constructor not found");

        return constructor.Invoke(new object[] { modelDetails });
    }

    private static T GetProperty<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(property, $"Property {propertyName} not found");
        return (T)property.GetValue(obj)!;
    }

    #endregion
}