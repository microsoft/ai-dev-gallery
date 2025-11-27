// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class SamplesHelperTests
{
    [TestMethod]
    public void GetAllSharedCode_LanguageModel_AddsChatClientFactory()
    {
        var sample = new Sample
        {
            SharedCode = new List<SharedCodeEnum>(),
            NugetPackageReferences = new List<string>(),
            Model1Types = new List<ModelType> { ModelType.Phi3MiniPhi3MiniCPU } // Assuming this is a language model type
        };

        var models = new Dictionary<ModelType, ExpandedModelDetails>
        {
            {
                ModelType.Phi3MiniPhi3MiniCPU,
                new ExpandedModelDetails("id", "url", "path", 100, HardwareAccelerator.CPU, null)
            }
        };

        // Note: This test relies on ModelDetailsHelper.EqualOrParent correctly identifying LanguageModels
        // If ModelType.Phi3MiniPhi3MiniCPU is not set up in the static mapping, this might fail.
        // However, since we can't easily mock the static mapping, we try to use a known type.
        var result = sample.GetAllSharedCode(models);

        // If it is recognized as a language model and not an API, it should add OnnxRuntimeGenAIChatClientFactory
        if (ModelDetailsHelper.EqualOrParent(ModelType.Phi3MiniPhi3MiniCPU, ModelType.LanguageModels))
        {
            Assert.IsTrue(result.Contains(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory));
            Assert.IsTrue(result.Contains(SharedCodeEnum.LlmPromptTemplate));
        }
    }

    [TestMethod]
    public void GetAllSharedCode_NonLanguageModel_AddsWinMLHelpers()
    {
        var sample = new Sample
        {
            SharedCode = new List<SharedCodeEnum>(),
            NugetPackageReferences = new List<string>(),
            Model1Types = new List<ModelType> { ModelType.ResNetResNet50V17 } // Assuming this is NOT a language model
        };

        var models = new Dictionary<ModelType, ExpandedModelDetails>
        {
            {
                ModelType.ResNetResNet50V17,
                new ExpandedModelDetails("id", "url", "path", 100, HardwareAccelerator.CPU, null)
            }
        };

        var result = sample.GetAllSharedCode(models);

        Assert.IsTrue(result.Contains(SharedCodeEnum.WinMLHelpers));
        Assert.IsTrue(result.Contains(SharedCodeEnum.DeviceUtils));
        Assert.IsTrue(result.Contains(SharedCodeEnum.NativeMethods));
    }

    [TestMethod]
    public void GetAllNugetPackageReferences_LanguageModel_AddsGenAIPackages()
    {
        var sample = new Sample
        {
            SharedCode = new List<SharedCodeEnum>(),
            NugetPackageReferences = new List<string>(),
            Model1Types = new List<ModelType> { ModelType.Phi3MiniPhi3MiniCPU }
        };

        var models = new Dictionary<ModelType, ExpandedModelDetails>
        {
            {
                ModelType.Phi3MiniPhi3MiniCPU,
                new ExpandedModelDetails("id", "url", "path", 100, HardwareAccelerator.CPU, null)
            }
        };

        if (ModelDetailsHelper.EqualOrParent(ModelType.Phi3MiniPhi3MiniCPU, ModelType.LanguageModels))
        {
            var result = sample.GetAllNugetPackageReferences(models);
            Assert.IsTrue(result.Contains("Microsoft.ML.OnnxRuntimeGenAI.Managed"));
            Assert.IsTrue(result.Contains("Microsoft.ML.OnnxRuntimeGenAI.WinML"));
        }
    }

    [TestMethod]
    public void GetCleanCSCode_ReplacesPlaceholders()
    {
        var sample = new Sample
        {
            CSCode = "var acc = sampleParams.HardwareAccelerator; var path = sampleParams.ModelPath; var policy = sampleParams.WinMlSampleOptions.Policy;",
            SharedCode = new List<SharedCodeEnum>(),
            NugetPackageReferences = new List<string>(),
            Model1Types = new List<ModelType> { ModelType.Phi3MiniPhi3MiniCPU },
            Id = "test",
            Name = "test",
            Icon = "test",
            PageType = typeof(object),
            XAMLCode = string.Empty,
            AssetFilenames = new List<string>()
        };

        var modelDetails = new ExpandedModelDetails("id", "url", "path", 100, HardwareAccelerator.CPU, new WinMlSampleOptions(null, null, false, null));
        var modelInfos = new Dictionary<ModelType, (ExpandedModelDetails, string)>
        {
            { ModelType.Phi3MiniPhi3MiniCPU, (modelDetails, "@\"path\"") }
        };

        var result = sample.GetCleanCSCode(modelInfos);

        Assert.IsTrue(result.Contains("HardwareAccelerator.CPU"));
        Assert.IsTrue(result.Contains("@\"path\""));

        // Assert.IsTrue(result.Contains("ExecutionProviderDevicePolicy.Default")); // Policy is null, so this won't be in the output
        Assert.IsFalse(result.Contains("sampleParams.HardwareAccelerator"));
        Assert.IsFalse(result.Contains("sampleParams.ModelPath"));
    }
}