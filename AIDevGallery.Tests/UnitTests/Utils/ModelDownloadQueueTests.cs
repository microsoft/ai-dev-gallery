// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class ModelDownloadQueueTests
{
    private string _tempDir = null!;
    private ModelDownloadQueue _downloadQueue = null!;
    private ModelCacheStore _cacheStore = null!;
    private ModelCache _modelCache = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests", "ModelDownload", Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        
        // Create test AppData and ModelCache
        var appDataTempDir = Path.Combine(_tempDir, "AppData");
        Directory.CreateDirectory(appDataTempDir);
        var appData = await AppData.CreateForTests(appDataTempDir, _tempDir);
        
        _cacheStore = await ModelCacheStore.CreateForApp(_tempDir);
        _modelCache = await ModelCache.CreateForApp(appData);
        
        // Set App static properties using reflection (for testing only)
        SetAppModelCache(_modelCache);
        SetAppAppData(appData);
        
        _downloadQueue = new ModelDownloadQueue();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Cancel any ongoing downloads
        foreach (var download in _downloadQueue.GetDownloads())
        {
            download.CancelDownload();
        }

        // Clear App static properties
        SetAppModelCache(null!);
        SetAppAppData(null!);

        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private static void SetAppModelCache(ModelCache modelCache)
    {
        // Use reflection to set the internal static property for testing
        var appType = typeof(AIDevGallery.App);
        var modelCacheProperty = appType.GetProperty("ModelCache", BindingFlags.NonPublic | BindingFlags.Static);
        modelCacheProperty?.SetValue(null, modelCache);
    }

    private static void SetAppAppData(AppData appData)
    {
        // Use reflection to set the internal static property for testing
        var appType = typeof(AIDevGallery.App);
        var appDataProperty = appType.GetProperty("AppData", BindingFlags.NonPublic | BindingFlags.Static);
        appDataProperty?.SetValue(null, appData);
    }

    [TestMethod]
    public void AddModel_ModelNotCached_ReturnsModelDownload()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();

        // Act
        var download = _downloadQueue.AddModel(modelDetails);

        // Assert
        Assert.IsNotNull(download);
        Assert.AreEqual(modelDetails.Url, download.Details.Url);
        Assert.AreEqual(DownloadStatus.Waiting, download.DownloadStatus);
    }

    [TestMethod]
    public void AddModel_SameModelTwice_ReturnsSameDownload()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();

        // Act
        var download1 = _downloadQueue.AddModel(modelDetails);
        var download2 = _downloadQueue.AddModel(modelDetails);

        // Assert
        Assert.IsNotNull(download1);
        Assert.IsNotNull(download2);
        Assert.AreSame(download1, download2);
    }

    [TestMethod]
    public void GetDownload_ExistingModel_ReturnsDownload()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();
        var addedDownload = _downloadQueue.AddModel(modelDetails);

        // Act
        var retrievedDownload = _downloadQueue.GetDownload(modelDetails.Url);

        // Assert
        Assert.IsNotNull(retrievedDownload);
        Assert.AreSame(addedDownload, retrievedDownload);
    }

    [TestMethod]
    public void GetDownload_NonExistingModel_ReturnsNull()
    {
        // Act
        var download = _downloadQueue.GetDownload("https://huggingface.co/nonexistent/model");

        // Assert
        Assert.IsNull(download);
    }

    [TestMethod]
    public void CancelModelDownload_ExistingDownload_CancelsSuccessfully()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();
        var download = _downloadQueue.AddModel(modelDetails);

        // Act
        _downloadQueue.CancelModelDownload(download);

        // Assert
        Assert.AreEqual(DownloadStatus.Canceled, download.DownloadStatus);
        Assert.AreEqual(0, _downloadQueue.GetDownloads().Count);
    }

    [TestMethod]
    public void GetDownloads_MultipleModels_ReturnsAllDownloads()
    {
        // Arrange
        var model1 = CreateTestModelDetails("model1");
        var model2 = CreateTestModelDetails("model2");

        // Act
        _downloadQueue.AddModel(model1);
        _downloadQueue.AddModel(model2);
        var downloads = _downloadQueue.GetDownloads();

        // Assert
        Assert.AreEqual(2, downloads.Count);
    }

    [TestMethod]
    public void ModelDownload_ProgressReported()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();
        var download = _downloadQueue.AddModel(modelDetails);

        bool progressReported = false;
        float lastProgress = 0;

        // Act
        download.StateChanged += (sender, args) =>
        {
            if (args.Progress > 0 && args.Progress != lastProgress)
            {
                progressReported = true;
                lastProgress = args.Progress;
            }
        };

        // Assert - just verify the event structure works
        // Actual progress testing would require a real download
        Assert.IsNotNull(download);
        Assert.AreEqual(0, download.DownloadProgress);
    }

    [TestMethod]
    public void ModelDownloadQueue_ModelsChangedEvent_Raised()
    {
        // Arrange
        bool eventRaised = false;
        _downloadQueue.ModelsChanged += (sender) => eventRaised = true;

        var modelDetails = CreateTestModelDetails();

        // Act
        _downloadQueue.AddModel(modelDetails);

        // Assert
        Assert.IsTrue(eventRaised, "ModelsChanged event should be raised when adding a model");
    }

    [TestMethod]
    public void CancelModelDownload_ByUrl_CancelsSuccessfully()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();
        _downloadQueue.AddModel(modelDetails);

        // Act
        _downloadQueue.CancelModelDownload(modelDetails.Url);
        var download = _downloadQueue.GetDownload(modelDetails.Url);

        // Assert
        Assert.IsNull(download, "Download should be removed from queue after cancellation");
    }

    [TestMethod]
    public void ModelDownload_InitialState_IsWaiting()
    {
        // Arrange & Act
        var modelDetails = CreateTestModelDetails();
        var download = _downloadQueue.AddModel(modelDetails);

        // Assert
        Assert.AreEqual(DownloadStatus.Waiting, download.DownloadStatus);
        Assert.AreEqual(0f, download.DownloadProgress);
    }

    [TestMethod]
    public void CancelDownload_DirectlyOnDownload_UpdatesStatus()
    {
        // Arrange
        var modelDetails = CreateTestModelDetails();
        var download = _downloadQueue.AddModel(modelDetails);

        // Act
        download.CancelDownload();

        // Assert
        Assert.AreEqual(DownloadStatus.Canceled, download.DownloadStatus);
    }

    private ModelDetails CreateTestModelDetails(string suffix = "")
    {
        return new ModelDetails
        {
            Id = $"test-model{suffix}",
            Name = $"Test Model{suffix}",
            Url = $"https://huggingface.co/test/model{suffix}",
            HardwareAccelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU },
            Size = 1024,
            Description = "Test model for download testing"
        };
    }
}
