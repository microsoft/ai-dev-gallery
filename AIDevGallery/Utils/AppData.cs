// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace AIDevGallery.Utils;

internal class AppData
{
    public required string ModelCachePath { get; set; }
    public required LinkedList<MostRecentlyUsedItem> MostRecentlyUsedItems { get; set; }

    // model or api ids
    public required LinkedList<string> UsageHistory { get; set; }

    public bool IsDiagnosticDataEnabled { get; set; }

    public bool IsFirstRun { get; set; }

    public bool IsDiagnosticsMessageDismissed { get; set; }

    public AppData()
    {
        IsDiagnosticDataEnabled = !PrivacyConsentHelpers.IsPrivacySensitiveRegion();
        IsFirstRun = true;
        IsDiagnosticsMessageDismissed = false;
    }

    private static string GetConfigFilePath()
    {
        var appDataFolder = ApplicationData.Current.LocalFolder.Path;
        return Path.Combine(appDataFolder, "state.json");
    }

    public static async Task<AppData> GetForApp()
    {
        AppData? appData = null;

        var configFile = GetConfigFilePath();

        try
        {
            if (File.Exists(configFile))
            {
                var file = await File.ReadAllTextAsync(configFile);
                appData = JsonSerializer.Deserialize(file, AppDataSourceGenerationContext.Default.AppData);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            appData ??= GetDefault();
        }

        return appData;
    }

    public async Task SaveAsync()
    {
        var str = JsonSerializer.Serialize(this, AppDataSourceGenerationContext.Default.AppData);
        await File.WriteAllTextAsync(GetConfigFilePath(), str);
    }

    public async Task AddMru(MostRecentlyUsedItem item, string? modelOrApiId = null)
    {
        foreach (var toRemove in MostRecentlyUsedItems.Where(i => i.ItemId == item.ItemId).ToArray())
        {
            MostRecentlyUsedItems.Remove(toRemove);
        }

        if (MostRecentlyUsedItems.Count > 5)
        {
            MostRecentlyUsedItems.RemoveLast();
        }

        if (!string.IsNullOrWhiteSpace(modelOrApiId))
        {
            UsageHistory.Remove(modelOrApiId);
            UsageHistory.AddFirst(modelOrApiId);
        }

        MostRecentlyUsedItems.AddFirst(item);
        await SaveAsync();
    }

    private static AppData GetDefault()
    {
        var homeDirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cacheDir = Path.Combine(homeDirPath, ".cache", "aigallery");

        return new AppData
        {
            ModelCachePath = cacheDir,
            MostRecentlyUsedItems = new(),
            UsageHistory = new()
        };
    }
}