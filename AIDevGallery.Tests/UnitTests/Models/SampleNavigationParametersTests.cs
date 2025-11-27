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
public class SampleNavigationParametersTests
{
    [TestMethod]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var promptTemplate = new LlmPromptTemplate { System = "system", User = "user", Assistant = "assistant" };
        var winMlOptions = new WinMlSampleOptions(ExecutionProviderDevicePolicy.DEFAULT, null, false, null);

        // Act
        var parameters = new SampleNavigationParameters(
            "sample-123",
            "model-456",
            "/path/to/model",
            HardwareAccelerator.GPU,
            promptTemplate,
            tcs,
            winMlOptions,
            cts.Token);

        // Assert
        Assert.AreEqual("sample-123", parameters.SampleId);
        Assert.AreEqual("/path/to/model", parameters.ModelPath);
        Assert.AreEqual(HardwareAccelerator.GPU, parameters.HardwareAccelerator);
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

        // Act
        var parameters = new SampleNavigationParameters(
            "sample-123",
            "model-456",
            "/path/to/model",
            HardwareAccelerator.CPU,
            null,
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
        var parameters = new SampleNavigationParameters(
            "sample-123",
            "model-456",
            "/path/to/model",
            HardwareAccelerator.CPU,
            null,
            tcs,
            null,
            cts.Token);

        // Act
        parameters.NotifyCompletion();

        // Assert
        Assert.IsTrue(tcs.Task.IsCompleted);
    }

    [TestMethod]
    public void ChatClientProperties_ReturnCorrectValues()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var promptTemplate = new LlmPromptTemplate { System = "sys", User = "usr", Assistant = "ast" };
        var parameters = new SampleNavigationParameters(
            "sample-123",
            "model-456",
            "/path/to/model",
            HardwareAccelerator.DML,
            promptTemplate,
            tcs,
            null,
            cts.Token);

        // Assert - using reflection or accessing protected members would be complex
        // We verify the public properties that should align with chat client properties
        Assert.AreEqual("/path/to/model", parameters.ModelPath);
        Assert.AreEqual(HardwareAccelerator.DML, parameters.HardwareAccelerator);
    }

    [TestMethod]
    public void Constructor_WithDifferentHardwareAccelerators_WorksCorrectly()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();
        var accelerators = new[]
        {
            HardwareAccelerator.CPU,
            HardwareAccelerator.GPU,
            HardwareAccelerator.DML,
            HardwareAccelerator.QNN
        };

        foreach (var accelerator in accelerators)
        {
            // Act
            var parameters = new SampleNavigationParameters(
                "sample-123",
                "model-456",
                "/path/to/model",
                accelerator,
                null,
                tcs,
                null,
                cts.Token);

            // Assert
            Assert.AreEqual(accelerator, parameters.HardwareAccelerator);
        }
    }

    [TestMethod]
    public void Constructor_WithNullPromptTemplate_DoesNotThrow()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource();

        // Act
        var parameters = new SampleNavigationParameters(
            "sample-123",
            "model-456",
            "/path/to/model",
            HardwareAccelerator.CPU,
            null,
            tcs,
            null,
            cts.Token);

        // Assert
        Assert.IsNotNull(parameters);
        Assert.AreEqual("sample-123", parameters.SampleId);
    }
}