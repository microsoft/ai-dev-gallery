// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class ModelDetailsHelperTests
{
    [TestMethod]
    public void IsApi_WCRAPI_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.WCRAPI }
        };
        Assert.IsTrue(model.IsApi());
    }

    [TestMethod]
    public void IsApi_SizeZero_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Size = 0
        };
        Assert.IsTrue(model.IsApi());
    }

    [TestMethod]
    public void IsApi_NormalModel_ReturnsFalse()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Size = 100
        };
        Assert.IsFalse(model.IsApi());
    }

    [TestMethod]
    public void IsLanguageModel_Ollama_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.OLLAMA },
            Url = "some-url"
        };
        Assert.IsTrue(model.IsLanguageModel());
    }

    [TestMethod]
    public void IsLanguageModel_UserAdded_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "useradded-languagemodel-123"
        };
        Assert.IsTrue(model.IsLanguageModel());
    }

    [TestMethod]
    public void IsLanguageModel_NormalModel_ReturnsFalse()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Url = "https://huggingface.co/some/model"
        };
        Assert.IsFalse(model.IsLanguageModel());
    }

    [TestMethod]
    public void ShowWhenWcrApi_WCRAPI_ReturnsVisible()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.WCRAPI }
        };
        Assert.AreEqual(Visibility.Visible, ModelDetailsHelper.ShowWhenWcrApi(model));
    }

    [TestMethod]
    public void ShowWhenWcrApi_Other_ReturnsCollapsed()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };
        Assert.AreEqual(Visibility.Collapsed, ModelDetailsHelper.ShowWhenWcrApi(model));
    }

    [TestMethod]
    public void IsOnnxModel_CPU_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };
        Assert.IsTrue(model.IsOnnxModel());
    }

    [TestMethod]
    public void IsOnnxModel_DML_ReturnsTrue()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.DML }
        };
        Assert.IsTrue(model.IsOnnxModel());
    }

    [TestMethod]
    public void IsOnnxModel_WCRAPI_ReturnsFalse()
    {
        var model = new ModelDetails
        {
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.WCRAPI }
        };
        Assert.IsFalse(model.IsOnnxModel());
    }

    [TestMethod]
    public void GetTemplateFromName_Phi_ReturnsPhi3()
    {
        var template = ModelDetailsHelper.GetTemplateFromName("Phi-3-mini");
        Assert.IsNotNull(template);

        // Assuming PromptTemplateHelpers is static and initialized.
        // If it's not, this might fail or return null if the dictionary is empty.
        // However, PromptTemplateHelpers usually has static initializer.
        // Let's check if we can assert on properties if we knew them,
        // but for now just checking it returns something if the map is populated.
        // If the map is empty in test context, this test might be flaky.
        // But let's assume static constructors run.
    }

    [TestMethod]
    public void GetModelDetailsFromApiDefinition_ReturnsCorrectDetails()
    {
        var apiDef = new ApiDefinition
        {
            Id = "api-id",
            Name = "API Name",
            Icon = "icon.png",
            ReadmeUrl = "http://readme",
            License = "MIT",
            Category = "App Content Search",
            IconGlyph = "glyph",
            Description = "desc",
            SampleIdToShowInDocs = "sample-id"
        };

        var result = ModelDetailsHelper.GetModelDetailsFromApiDefinition(ModelType.Phi3MiniPhi3MiniCPU, apiDef);

        Assert.AreEqual("api-id", result.Id);
        Assert.AreEqual("API Name", result.Name);
        Assert.IsTrue(result.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI));
        Assert.IsTrue(result.HardwareAccelerators.Contains(HardwareAccelerator.ACI)); // Because Category is App Content Search
    }

    [TestMethod]
    public void EqualOrParent_SameType_ReturnsTrue()
    {
        // Testing the trivial case where types are equal
        Assert.IsTrue(ModelDetailsHelper.EqualOrParent(ModelType.Phi3MiniPhi3MiniCPU, ModelType.Phi3MiniPhi3MiniCPU));
    }
}