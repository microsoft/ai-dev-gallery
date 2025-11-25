// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.UnitTests.Models;

[TestClass]
public class ExpandedModelDetailsTests
{
    [TestMethod]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var winMlOptions = new WinMlSampleOptions(ExecutionProviderDevicePolicy.DEFAULT, "DML", true, "GPU");
        var details = new ExpandedModelDetails(
            "model-id-123",
            "/path/to/model",
            "https://example.com/model",
            1024000,
            HardwareAccelerator.GPU,
            winMlOptions);

        // Assert
        Assert.AreEqual("model-id-123", details.Id);
        Assert.AreEqual("/path/to/model", details.Path);
        Assert.AreEqual("https://example.com/model", details.Url);
        Assert.AreEqual(1024000, details.ModelSize);
        Assert.AreEqual(HardwareAccelerator.GPU, details.HardwareAccelerator);
        Assert.AreEqual(winMlOptions, details.WinMlSampleOptions);
    }

    [TestMethod]
    public void Constructor_WithoutWinMlSampleOptions_SetsToNull()
    {
        // Arrange & Act
        var details = new ExpandedModelDetails(
            "model-id",
            "/path",
            "https://url.com",
            500,
            HardwareAccelerator.CPU);

        // Assert
        Assert.IsNull(details.WinMlSampleOptions);
    }

    [TestMethod]
    public void Equality_SameValues_ReturnsTrue()
    {
        // Arrange
        var winMlOptions = new WinMlSampleOptions(ExecutionProviderDevicePolicy.DEFAULT, null, false, null);
        var details1 = new ExpandedModelDetails("id", "path", "url", 100, HardwareAccelerator.CPU, winMlOptions);
        var details2 = new ExpandedModelDetails("id", "path", "url", 100, HardwareAccelerator.CPU, winMlOptions);

        // Assert
        Assert.AreEqual(details1, details2);
    }

    [TestMethod]
    public void Equality_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var details1 = new ExpandedModelDetails("id1", "path1", "url1", 100, HardwareAccelerator.CPU, null);
        var details2 = new ExpandedModelDetails("id2", "path2", "url2", 200, HardwareAccelerator.GPU, null);

        // Assert
        Assert.AreNotEqual(details1, details2);
    }

    [TestMethod]
    public void Deconstruction_WorksCorrectly()
    {
        // Arrange
        var winMlOptions = new WinMlSampleOptions(ExecutionProviderDevicePolicy.DEFAULT, null, false, null);
        var details = new ExpandedModelDetails("id", "path", "url", 100, HardwareAccelerator.DML, winMlOptions);

        // Act
        var (id, path, url, size, accelerator, options) = details;

        // Assert
        Assert.AreEqual("id", id);
        Assert.AreEqual("path", path);
        Assert.AreEqual("url", url);
        Assert.AreEqual(100, size);
        Assert.AreEqual(HardwareAccelerator.DML, accelerator);
        Assert.AreEqual(winMlOptions, options);
    }
}