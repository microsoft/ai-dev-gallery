// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests.Utils;

[TestClass]
public class ModelCacheStoreTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests", Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public async Task CreateForApp_CreatesNewStore()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        Assert.IsNotNull(store);
        Assert.AreEqual(_tempDir, store.CacheDir);
        Assert.AreEqual(0, store.Models.Count);
    }

    [TestMethod]
    public async Task AddModel_AddsModelAndSaves()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        // Create a dummy file for the model path so validation passes
        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");

        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);

        await store.AddModel(cachedModel);

        Assert.AreEqual(1, store.Models.Count);
        Assert.AreEqual("test-model", store.Models[0].Details.Id);

        // Verify persistence
        var store2 = await ModelCacheStore.CreateForApp(_tempDir);
        Assert.AreEqual(1, store2.Models.Count);
        Assert.AreEqual("test-model", store2.Models[0].Details.Id);
    }

    [TestMethod]
    public async Task RemoveModel_RemovesModelAndSaves()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");

        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);
        await store.AddModel(cachedModel);

        await store.RemoveModel(cachedModel);

        Assert.AreEqual(0, store.Models.Count);

        // Verify persistence
        var store2 = await ModelCacheStore.CreateForApp(_tempDir);
        Assert.AreEqual(0, store2.Models.Count);
    }

    [TestMethod]
    public async Task ClearAsync_ClearsAllModels()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");

        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);
        await store.AddModel(cachedModel);

        await store.ClearAsync();

        Assert.AreEqual(0, store.Models.Count);
    }

    [TestMethod]
    public async Task CreateForApp_ValidatesPaths()
    {
        // 1. Create a store and add a model
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");

        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);
        await store.AddModel(cachedModel);

        // 2. Delete the model file
        File.Delete(modelPath);

        // 3. Re-load the store. It should validate and remove the model because the file is missing.
        var store2 = await ModelCacheStore.CreateForApp(_tempDir);
        Assert.AreEqual(0, store2.Models.Count);
    }

    [TestMethod]
    public async Task AddModel_OverwritesExistingModel_WhenUrlMatches()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");

        var cachedModel1 = new CachedModel(modelDetails, modelPath, true, 100);
        await store.AddModel(cachedModel1);

        // Create a second model with the same URL but different details (e.g. size)
        var cachedModel2 = new CachedModel(modelDetails, modelPath, true, 200);

        await store.AddModel(cachedModel2);

        Assert.AreEqual(1, store.Models.Count);
        Assert.AreEqual(200, store.Models[0].ModelSize);
    }

    [TestMethod]
    public async Task ModelsChanged_IsRaised_OnAddRemoveClear()
    {
        var store = await ModelCacheStore.CreateForApp(_tempDir);
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };
        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");
        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);

        bool eventRaised = false;
        store.ModelsChanged += (s) => eventRaised = true;

        // Test Add
        await store.AddModel(cachedModel);
        Assert.IsTrue(eventRaised, "Event should be raised on Add");

        eventRaised = false;

        // Test Remove
        await store.RemoveModel(cachedModel);
        Assert.IsTrue(eventRaised, "Event should be raised on Remove");

        // Re-add for Clear test
        await store.AddModel(cachedModel);
        eventRaised = false;

        // Test Clear
        await store.ClearAsync();
        Assert.IsTrue(eventRaised, "Event should be raised on Clear");
    }

    [TestMethod]
    public async Task CreateForApp_WithExplicitModels_InitializesCorrectly()
    {
        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };
        var modelPath = Path.Combine(_tempDir, "model.onnx");
        File.WriteAllText(modelPath, "dummy content");
        var cachedModel = new CachedModel(modelDetails, modelPath, true, 100);

        var initialModels = new List<CachedModel> { cachedModel };

        var store = await ModelCacheStore.CreateForApp(_tempDir, initialModels);

        Assert.AreEqual(1, store.Models.Count);
        Assert.AreEqual("test-model", store.Models[0].Details.Id);
    }

    [TestMethod]
    public async Task CreateForApp_WithCorruptJson_ReturnsEmptyStore()
    {
        var cacheFile = Path.Combine(_tempDir, "cache.json");
        await File.WriteAllTextAsync(cacheFile, "{ invalid json }");

        var store = await ModelCacheStore.CreateForApp(_tempDir);

        Assert.IsNotNull(store);
        Assert.AreEqual(0, store.Models.Count);
    }

    [TestMethod]
    public async Task SaveAsync_CreatesDirectory_IfMissing()
    {
        // Create store and ensure directory exists initially
        var store = await ModelCacheStore.CreateForApp(_tempDir);

        // Delete the directory
        Directory.Delete(_tempDir, true);
        Assert.IsFalse(Directory.Exists(_tempDir));

        var modelDetails = new ModelDetails
        {
            Id = "test-model",
            Name = "Test Model",
            Url = "https://huggingface.co/test/model",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU }
        };

        // We need a valid model path for ValidateAndSaveAsync not to remove it,
        // but since we deleted the temp dir, we need to recreate the model file location
        // OR just rely on the fact that SaveAsync is called inside AddModel.
        // However, AddModel calls SaveAsync.
        // But ValidateAndSaveAsync runs on load.

        // Let's just try to add a model.
        // Note: AddModel calls SaveAsync.
        // But wait, if we deleted _tempDir, we also deleted the model file if it was inside it.
        // So we should use a model path OUTSIDE _tempDir for this test to ensure it's not removed by validation logic,
        // OR we just want to test that cache.json is written.

        // Actually, let's just call ClearAsync which also calls SaveAsync.
        await store.ClearAsync();

        Assert.IsTrue(Directory.Exists(_tempDir));
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "cache.json")));
    }
}