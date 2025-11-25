// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AIDevGallery.UnitTests.Utils;

[TestClass]
public class AppUtilsTests
{
    [TestMethod]
    public void FileSizeToString_ConvertsCorrectly()
    {
        Assert.AreEqual("1.0KB", AppUtils.FileSizeToString(1024));
        Assert.AreEqual("1.0MB", AppUtils.FileSizeToString(1024 * 1024));

        // Use a tolerance or exact string match if we know the implementation
        // 1.5GB = 1.5 * 1024 * 1024 * 1024 = 1610612736
        Assert.AreEqual("1.5GB", AppUtils.FileSizeToString(1610612736));
        Assert.AreEqual("500 Bytes", AppUtils.FileSizeToString(500));
    }

    [TestMethod]
    public void StringToFileSize_ConvertsCorrectly()
    {
        Assert.AreEqual(1024, AppUtils.StringToFileSize("1KB"));
        Assert.AreEqual(1024 * 1024, AppUtils.StringToFileSize("1MB"));
        Assert.AreEqual(500, AppUtils.StringToFileSize("500B"));
        Assert.AreEqual(0, AppUtils.StringToFileSize("Invalid"));
    }

    [TestMethod]
    public void ToLlmPromptTemplate_ConvertsCorrectly()
    {
        var template = new PromptTemplate
        {
            System = "System prompt",
            User = "User prompt",
            Assistant = "Assistant prompt",
            Stop = new[] { "stop1", "stop2" }
        };

        var llmTemplate = AppUtils.ToLlmPromptTemplate(template);

        Assert.AreEqual(template.System, llmTemplate.System);
        Assert.AreEqual(template.User, llmTemplate.User);
        Assert.AreEqual(template.Assistant, llmTemplate.Assistant);
        CollectionAssert.AreEqual(template.Stop, llmTemplate.Stop);
    }

    [TestMethod]
    public void ToPerc_FormatsCorrectly()
    {
        Assert.AreEqual("50.5%", AppUtils.ToPerc(50.5f));
        Assert.AreEqual("100.0%", AppUtils.ToPerc(100.0f));
        Assert.AreEqual("0.0%", AppUtils.ToPerc(0.0f));
    }

    [TestMethod]
    public void GetHardwareAcceleratorsString_ReturnsCommaSeparatedString()
    {
        var accelerators = new List<HardwareAccelerator>
        {
            HardwareAccelerator.CPU,
            HardwareAccelerator.GPU
        };

        var result = AppUtils.GetHardwareAcceleratorsString(accelerators);

        // Note: The order depends on the input list order and implementation.
        // GetHardwareAcceleratorString returns "CPU" for CPU and "GPU" for GPU/DML.
        Assert.IsTrue(result.Contains("CPU"));
        Assert.IsTrue(result.Contains("GPU"));
        Assert.IsTrue(result.Contains(", "));
    }

    [TestMethod]
    public void GetModelTypeStringFromHardwareAccelerators_ReturnsCorrectType()
    {
        var accelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU };
        Assert.AreEqual("ONNX", AppUtils.GetModelTypeStringFromHardwareAccelerators(accelerators));

        accelerators = new List<HardwareAccelerator> { HardwareAccelerator.GPU };
        Assert.AreEqual("ONNX", AppUtils.GetModelTypeStringFromHardwareAccelerators(accelerators));

        accelerators = new List<HardwareAccelerator> { HardwareAccelerator.NPU };
        Assert.AreEqual("ONNX", AppUtils.GetModelTypeStringFromHardwareAccelerators(accelerators));

        accelerators = new List<HardwareAccelerator>();
        Assert.AreEqual(string.Empty, AppUtils.GetModelTypeStringFromHardwareAccelerators(accelerators));
    }

    [TestMethod]
    public void GetHardwareAcceleratorString_ReturnsCorrectString()
    {
        Assert.AreEqual("GPU", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.GPU));
        Assert.AreEqual("GPU", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.DML));
        Assert.AreEqual("NPU", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.NPU));
        Assert.AreEqual("NPU", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.QNN));
        Assert.AreEqual("Windows AI API", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.ACI));
        Assert.AreEqual("CPU", AppUtils.GetHardwareAcceleratorString(HardwareAccelerator.CPU));
    }

    [TestMethod]
    public void GetHardwareAcceleratorDescription_ReturnsCorrectDescription()
    {
        Assert.AreEqual("This model will run on CPU", AppUtils.GetHardwareAcceleratorDescription(HardwareAccelerator.CPU));
        Assert.AreEqual("This model will run on supported GPUs with DirectML", AppUtils.GetHardwareAcceleratorDescription(HardwareAccelerator.GPU));
        Assert.AreEqual("This model will run on NPUs", AppUtils.GetHardwareAcceleratorDescription(HardwareAccelerator.NPU));
        Assert.AreEqual("The model will run locally via Ollama", AppUtils.GetHardwareAcceleratorDescription(HardwareAccelerator.OLLAMA));
    }

    [TestMethod]
    public void GetModelSourceOriginFromUrl_ReturnsCorrectOrigin()
    {
        Assert.AreEqual("This model was downloaded from Hugging Face", AppUtils.GetModelSourceOriginFromUrl("https://huggingface.co/model"));
        Assert.AreEqual("This model was downloaded from GitHub", AppUtils.GetModelSourceOriginFromUrl("https://github.com/model"));
        Assert.AreEqual("This model was added by you", AppUtils.GetModelSourceOriginFromUrl("local/path"));
        Assert.AreEqual(string.Empty, AppUtils.GetModelSourceOriginFromUrl("https://example.com"));
    }

    [TestMethod]
    public void GetLicenseTitleFromString_ReturnsCorrectTitle()
    {
        Assert.AreEqual("MIT", AppUtils.GetLicenseTitleFromString("mit"));
        Assert.AreEqual("Unknown", AppUtils.GetLicenseTitleFromString("unknown-license"));
    }

    [TestMethod]
    public void GetLicenseShortNameFromString_ReturnsCorrectName()
    {
        Assert.AreEqual("mit", AppUtils.GetLicenseShortNameFromString("mit"));
        Assert.AreEqual("Unknown", AppUtils.GetLicenseShortNameFromString(null));
        Assert.AreEqual("Unknown", AppUtils.GetLicenseShortNameFromString(string.Empty));
    }

    [TestMethod]
    public void GetLicenseUrlFromModel_ReturnsCorrectUrl()
    {
        var model = new ModelDetails
        {
            License = "mit",
            Url = "https://model.url"
        };

        var uri = AppUtils.GetLicenseUrlFromModel(model);
        Assert.IsTrue(uri.ToString().Contains("mit"));

        model.License = "unknown";
        uri = AppUtils.GetLicenseUrlFromModel(model);
        Assert.AreEqual("https://model.url/", uri.ToString());
    }
}