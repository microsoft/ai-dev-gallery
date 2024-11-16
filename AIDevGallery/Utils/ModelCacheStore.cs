// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Utils
{
    internal class ModelCacheStore
    {
        public IReadOnlyList<CachedModel> Models => _models.AsReadOnly();

        public delegate void ModelsChangedHandler(ModelCacheStore sender);
        public event ModelsChangedHandler? ModelsChanged;

        private readonly List<CachedModel> _models = [];

        public string CacheDir { get; init; } = null!;

        private ModelCacheStore(string cacheDir, List<CachedModel>? models)
        {
            CacheDir = cacheDir;
            _models = models ?? [];
        }

        public static async Task<ModelCacheStore> CreateForApp(string cacheDir, List<CachedModel>? models = null)
        {
            try
            {
                if (models == null)
                {
                    var cacheFile = Path.Combine(cacheDir, "cache.json");
                    if (File.Exists(cacheFile))
                    {
                        var json = await File.ReadAllTextAsync(cacheFile);

                        return new ModelCacheStore(cacheDir, JsonSerializer.Deserialize(json, AppDataSourceGenerationContext.Default.ListCachedModel));
                    }
                }
                else
                {
                    ModelCacheStore modelCacheStore = new(cacheDir, models);
                    await modelCacheStore.Save();
                    return modelCacheStore;
                }
            }
            catch
            {
            }

            return new ModelCacheStore(cacheDir, null);
        }

        private async Task Save()
        {
            var cacheFile = Path.Combine(CacheDir, "cache.json");

            var str = JsonSerializer.Serialize(_models, AppDataSourceGenerationContext.Default.ListCachedModel);
            await File.WriteAllTextAsync(cacheFile, str);
        }

        public async Task AddModel(CachedModel model)
        {
            var existingModel = _models.Where(m => m.Url == model.Url).ToList();
            foreach (var cachedModel in existingModel)
            {
                _models.Remove(cachedModel);
            }

            _models.Add(model);

            ModelsChanged?.Invoke(this);

            await Save();
        }

        public async Task RemoveModel(CachedModel model)
        {
            _models.Remove(model);
            ModelsChanged?.Invoke(this);
            await Save();
        }

        public async Task DeleteCache()
        {
            _models.Clear();
            ModelsChanged?.Invoke(this);
            await Save();
        }
    }
}