// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class ModelCache
{
    private readonly AppData _appData;

    /* private long _movedSize; */

    public ModelDownloadQueue DownloadQueue { get; }
    public ModelCacheStore CacheStore { get; private set; }
    public IReadOnlyList<CachedModel> Models => CacheStore.Models;

    private ModelCache(AppData appData, ModelDownloadQueue modelDownloadQueue, ModelCacheStore modelCacheStore)
    {
        _appData = appData;
        DownloadQueue = modelDownloadQueue;
        CacheStore = modelCacheStore;
    }

    public static async Task<ModelCache> CreateForApp(AppData appData)
    {
        var downloadQueue = new ModelDownloadQueue(appData.ModelCachePath);

        var modelCacheStore = await ModelCacheStore.CreateForApp(appData.ModelCachePath);
        var instance = new ModelCache(appData, downloadQueue, modelCacheStore)
        {
            CacheStore = modelCacheStore
        };
        instance.DownloadQueue.ModelDownloadCompleted += instance.ModelDownloadQueue_ModelDownloadCompleted;

        return instance;
    }

    private async void ModelDownloadQueue_ModelDownloadCompleted(object? sender, ModelDownloadCompletedEventArgs e)
    {
        var cachedModel = e.CachedModel;
        await CacheStore.AddModel(cachedModel);
    }

    public string GetCacheFolder()
    {
        return _appData.ModelCachePath;
    }

    public async Task SetCacheFolderPath(string newPath, List<CachedModel>? models = null)
    {
        _appData.ModelCachePath = newPath;
        await _appData.SaveAsync();

        CacheStore = await ModelCacheStore.CreateForApp(newPath, models);

        // cancel existing downloads
        DownloadQueue.CacheDir = newPath;
        DownloadQueue.GetDownloads().ToList().ForEach(DownloadQueue.CancelModelDownload);
    }

    public ModelDownload? AddModelToDownloadQueue(ModelDetails modelDetails)
    {
        if (IsModelCached(modelDetails.Url))
        {
            return null;
        }

        var existingDownload = DownloadQueue.GetDownload(modelDetails.Url);
        if (existingDownload != null)
        {
            return existingDownload;
        }

        var download = DownloadQueue.EnqueueModelDownload(modelDetails);
        return download;
    }

    public async Task<CachedModel> AddLocalModelToCache(ModelDetails modelDetails, string modelPath, bool isFile = false)
    {
        var cachedModel = new CachedModel(modelDetails, modelPath, isFile, modelDetails.Size);
        await CacheStore.AddModel(cachedModel);
        return cachedModel;
    }

    public CachedModel? GetCachedModel(string url)
    {
        url = UrlHelpers.GetFullUrl(url);
        return CacheStore.Models.FirstOrDefault(m => m.Url == url);
    }

    public CachedModel? GetCachedModelByPath(string path)
    {
        return CacheStore.Models.FirstOrDefault(m => m.Path == path);
    }

    public bool IsModelCached(string url)
    {
        url = UrlHelpers.GetFullUrl(url);
        return CacheStore.Models.Any(m => m.Url == url);
    }

    public async Task DeleteModelFromCache(string url)
    {
        if (IsModelCached(url))
        {
            url = UrlHelpers.GetFullUrl(url);
            await DeleteModelFromCache(CacheStore.Models.First(m => m.Url == url));
        }
    }

    public async Task DeleteModelFromCache(CachedModel model)
    {
        ModelDeletedEvent.Log(model.Url);
        await CacheStore.RemoveModel(model);

        if (model.Url.StartsWith("local", System.StringComparison.OrdinalIgnoreCase))
        {
            // do not delete models added by user that are not in the cache folder
            return;
        }

        if (model.IsFile && File.Exists(model.Path))
        {
            File.Delete(model.Path);
        }
        else if (Directory.Exists(model.Path))
        {
            Directory.Delete(model.Path, true);
        }
    }

    public async Task ClearCache()
    {
        ModelCacheDeletedEvent.Log();

        var cacheDir = GetCacheFolder();
        Directory.Delete(cacheDir, true);
        await CacheStore.ClearAsync();
    }

    public async Task MoveCache(string path, CancellationToken ct)
    {
        ModelCacheMovedEvent.Log();
        var sourceFolder = GetCacheFolder();
        /* _movedSize = 0; */

        await Task.Run(
            () =>
            {
                if (Directory.Exists(sourceFolder))
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }

                    MoveFolder(sourceFolder, path, ct);
                    if (!ct.IsCancellationRequested && Directory.Exists(sourceFolder))
                    {
                        Directory.Delete(sourceFolder, true);
                    }
                }
            },
            ct);

        var newModels = CacheStore.Models.Select(m => new CachedModel(m.Details, m.Path.Replace(sourceFolder, path), m.IsFile, m.ModelSize));
        await SetCacheFolderPath(path, newModels.ToList());
    }

    private void MoveFolder(string sourcePath, string destinationPath, CancellationToken ct)
    {
        if (Path.GetPathRoot(sourcePath) != Path.GetPathRoot(destinationPath))
        {
            CopyFolder(sourcePath, destinationPath, ct);
        }
        else
        {
            Directory.Move(sourcePath, destinationPath);
        }
    }

    private void CopyFolder(string sourceFolder, string destFolder, CancellationToken ct)
    {
        if (!Directory.Exists(destFolder))
        {
            Directory.CreateDirectory(destFolder);
        }

        ct.ThrowIfCancellationRequested();

        string[] files = Directory.GetFiles(sourceFolder);
        ct.ThrowIfCancellationRequested();

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string dest = Path.Combine(destFolder, name);
            File.Copy(file, dest);
            /* _movedSize += new FileInfo(dest).Length; */
            ct.ThrowIfCancellationRequested();
        }

        string[] folders = Directory.GetDirectories(sourceFolder);
        ct.ThrowIfCancellationRequested();

        foreach (string folder in folders)
        {
            string name = Path.GetFileName(folder);
            string dest = Path.Combine(destFolder, name);
            MoveFolder(folder, dest, ct);
            ct.ThrowIfCancellationRequested();
        }
    }
}