// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class SampleNavigationArgsTests
{
    private Sample CreateTestSample()
    {
        return new Sample
        {
            Id = "test-sample",
            Name = "Test Sample",
            Icon = "icon.png",
            PageType = typeof(object),
            CSCode = "// code",
            XAMLCode = "// xaml",
            SharedCode = new List<SharedCodeEnum>(),
            NugetPackageReferences = new List<string>(),
            Model1Types = new List<ModelType> { ModelType.Phi3MiniPhi3MiniCPU },
            AssetFilenames = new List<string>()
        };
    }

    private ModelDetails CreateTestModelDetails()
    {
        return new ModelDetails
        {
            Id = "model-id",
            Name = "Test Model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "https://example.com/model",
            Size = 1024000,
            Description = "Description",
            SupportedOnQualcomm = true
        };
    }

    [TestMethod]
    public void Constructor_WithSampleOnly_SetsSampleProperty()
    {
        // Arrange
        var sample = CreateTestSample();

        // Act
        var args = new SampleNavigationArgs(sample);

        // Assert
        Assert.AreEqual(sample, args.Sample);
        Assert.IsNull(args.ModelDetails);
        Assert.IsNull(args.OpenCodeView);
    }

    [TestMethod]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var sample = CreateTestSample();
        var modelDetails = CreateTestModelDetails();

        // Act
        var args = new SampleNavigationArgs(sample, modelDetails, true);

        // Assert
        Assert.AreEqual(sample, args.Sample);
        Assert.AreEqual(modelDetails, args.ModelDetails);
        Assert.IsTrue(args.OpenCodeView.HasValue);
        Assert.IsTrue(args.OpenCodeView.Value);
    }

    [TestMethod]
    public void Constructor_WithNullModelDetails_SetsToNull()
    {
        // Arrange
        var sample = CreateTestSample();

        // Act
        var args = new SampleNavigationArgs(sample, null, false);

        // Assert
        Assert.AreEqual(sample, args.Sample);
        Assert.IsNull(args.ModelDetails);
        Assert.IsTrue(args.OpenCodeView.HasValue);
        Assert.IsFalse(args.OpenCodeView.Value);
    }

    [TestMethod]
    public void Constructor_WithDefaultOpenCodeView_SetsFalse()
    {
        // Arrange
        var sample = CreateTestSample();
        var modelDetails = CreateTestModelDetails();

        // Act
        var args = new SampleNavigationArgs(sample, modelDetails);

        // Assert
        Assert.AreEqual(sample, args.Sample);
        Assert.AreEqual(modelDetails, args.ModelDetails);
        Assert.IsTrue(args.OpenCodeView.HasValue);
        Assert.IsFalse(args.OpenCodeView.Value);
    }

    [TestMethod]
    public void Properties_AreImmutable()
    {
        // Arrange
        var sample = CreateTestSample();
        var modelDetails = CreateTestModelDetails();
        var args = new SampleNavigationArgs(sample, modelDetails, true);

        // Assert - properties should have private setters
        Assert.IsNotNull(args.Sample);
        Assert.IsNotNull(args.ModelDetails);
        Assert.IsNotNull(args.OpenCodeView);

        // Verify values don't change
        Assert.AreEqual(sample, args.Sample);
        Assert.AreEqual(modelDetails, args.ModelDetails);
        Assert.IsTrue(args.OpenCodeView.HasValue);
        Assert.IsTrue(args.OpenCodeView.Value);
    }
}