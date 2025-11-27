// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class MultiModelSampleNavigationParametersTests
{
    [TestMethod]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2" };
        var modelPaths = new[] { "/path/model1", "/path/model2" };
        var accelerators = new[] { HardwareAccelerator.CPU, HardwareAccelerator.GPU };
        var templates = new LlmPromptTemplate?[]
        {
            new LlmPromptTemplate { System = "sys1", User = "usr1", Assistant = "ast1" },
            new LlmPromptTemplate { System = "sys2", User = "usr2", Assistant = "ast2" }
        };
        var winMlOptions = new WinMlSampleOptions(ExecutionProviderDevicePolicy.DEFAULT, null, false, null);

        // Act
        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            winMlOptions,
            cts.Token);

        // Assert
        CollectionAssert.AreEqual(modelPaths, parameters.ModelPaths);
        CollectionAssert.AreEqual(accelerators, parameters.HardwareAccelerators);
        Assert.AreEqual("CPU", parameters.PreferedEP);
        Assert.AreEqual(winMlOptions, parameters.WinMlSampleOptions);
        Assert.AreEqual(tcs, parameters.SampleLoadedCompletionSource);
        Assert.AreEqual(cts.Token, parameters.CancellationToken);
    }

    [TestMethod]
    public void WinMlSampleOptions_WhenNull_ReturnsDefaultValue()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2" };
        var modelPaths = new[] { "/path/model1", "/path/model2" };
        var accelerators = new[] { HardwareAccelerator.CPU, HardwareAccelerator.GPU };
        var templates = new LlmPromptTemplate?[] { null, null };

        // Act
        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            null,
            cts.Token);

        // Assert
        var options = parameters.WinMlSampleOptions;
        Assert.IsNotNull(options);
        Assert.IsNull(options.Policy);
        Assert.IsNull(options.EpName);
        Assert.IsFalse(options.CompileModel);
        Assert.IsNull(options.DeviceType);
    }

    [TestMethod]
    public void NotifyCompletion_SetsTaskCompletionSourceResult()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2" };
        var modelPaths = new[] { "/path/model1", "/path/model2" };
        var accelerators = new[] { HardwareAccelerator.CPU, HardwareAccelerator.GPU };
        var templates = new LlmPromptTemplate?[] { null, null };

        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            null,
            cts.Token);

        // Act
        parameters.NotifyCompletion();

        // Assert
        Assert.IsTrue(tcs.Task.IsCompleted);
    }

    [TestMethod]
    public void ModelPaths_ReturnsAllPaths()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2", "model-3" };
        var modelPaths = new[] { "/path/model1", "/path/model2", "/path/model3" };
        var accelerators = new[] { HardwareAccelerator.CPU, HardwareAccelerator.GPU, HardwareAccelerator.DML };
        var templates = new LlmPromptTemplate?[] { null, null, null };

        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            null,
            cts.Token);

        // Assert
        Assert.AreEqual(3, parameters.ModelPaths.Length);
        Assert.AreEqual("/path/model1", parameters.ModelPaths[0]);
        Assert.AreEqual("/path/model2", parameters.ModelPaths[1]);
        Assert.AreEqual("/path/model3", parameters.ModelPaths[2]);
    }

    [TestMethod]
    public void HardwareAccelerators_ReturnsAllAccelerators()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2" };
        var modelPaths = new[] { "/path/model1", "/path/model2" };
        var accelerators = new[] { HardwareAccelerator.QNN, HardwareAccelerator.DML };
        var templates = new LlmPromptTemplate?[] { null, null };

        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            null,
            cts.Token);

        // Assert
        Assert.AreEqual(2, parameters.HardwareAccelerators.Length);
        Assert.AreEqual(HardwareAccelerator.QNN, parameters.HardwareAccelerators[0]);
        Assert.AreEqual(HardwareAccelerator.DML, parameters.HardwareAccelerators[1]);
    }

    [TestMethod]
    public void Constructor_WithMixedPromptTemplates_WorksCorrectly()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var modelIds = new[] { "model-1", "model-2" };
        var modelPaths = new[] { "/path/model1", "/path/model2" };
        var accelerators = new[] { HardwareAccelerator.CPU, HardwareAccelerator.GPU };
        var templates = new LlmPromptTemplate?[]
        {
            new LlmPromptTemplate { System = "sys", User = "usr", Assistant = "ast" },
            null
        };

        // Act
        var parameters = new MultiModelSampleNavigationParameters(
            "sample-123",
            modelIds,
            modelPaths,
            accelerators,
            templates,
            tcs,
            null,
            cts.Token);

        // Assert
        Assert.IsNotNull(parameters);
        Assert.AreEqual(2, parameters.ModelPaths.Length);
    }
}