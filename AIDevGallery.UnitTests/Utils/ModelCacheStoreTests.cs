using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests.Utils
{
    [TestClass]
    public class ModelCacheStoreTests
    {
        private string _tempDir;

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
    }
}
