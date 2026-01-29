// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Search.AppContentIndex;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Storage.Pickers;

namespace AIDevGallery.Pages;

internal sealed partial class SettingsPage : Page
{
    private readonly ObservableCollection<CachedModel> cachedModels = [];
    private readonly ObservableCollection<AppContentIndexStores> indexStores = [];
    private readonly RelayCommand endMoveCommand;
    private string? cacheFolderPath;
    private string? indexFolderPath;
    private bool isMovingCache;

    private CancellationTokenSource? _cts;


    public SettingsPage()
    {
        this.InitializeComponent();
        endMoveCommand = new RelayCommand(() => _cts?.Cancel());
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        NavigatedToPageEvent.Log(nameof(SettingsPage));

        VersionTextRun.Text = AppUtils.GetAppVersion();
        GetStorageInfo();
        GetAppContentIndexStorageInfo();

        DiagnosticDataToggleSwitch.IsOn = App.AppData.IsDiagnosticDataEnabled;
        SemanticSearchToggleSwitch.IsOn = App.AppData.IsAppContentSearchEnabled;
        if (e.Parameter is string manageModels && manageModels == "ModelManagement")
        {
            ModelsExpander.IsExpanded = true;
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (isMovingCache)
        {
            e.Cancel = true;
        }

        base.OnNavigatingFrom(e);
    }

    private async void GetStorageInfo()
    {
        try
        {
            cachedModels.Clear();

            cacheFolderPath = App.ModelCache.GetCacheFolder();
            FolderPathTxt.Content = cacheFolderPath;

            long totalCacheSize = 0;
            var allModels = await App.ModelCache.GetAllModelsAsync();

            foreach (var cachedModel in allModels.OrderBy(m => m.Details.Name))
            {
                cachedModels.Add(cachedModel);
                totalCacheSize += cachedModel.ModelSize;
            }

            if (cachedModels.Count > 0)
            {
                ModelsExpander.IsExpanded = true;
            }

            TotalCacheTxt.Text = AppUtils.FileSizeToString(totalCacheSize);
        }
        catch (Exception)
        {
            TotalCacheTxt.Text = $"Error loading cache info";
        }
    }

    private void FolderPathTxt_Click(object sender, RoutedEventArgs e)
    {
        if (cacheFolderPath != null)
        {
            Process.Start("explorer.exe", cacheFolderPath);
        }
    }

    private void GetAppContentIndexStorageInfo()
    {
        try
        {
            indexStores.Clear();

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var appContentIndicesFolder = Path.Combine(localFolder, "AppContentIndices");

            indexFolderPath = appContentIndicesFolder;
            IndexFolderPathTxt.Content = appContentIndicesFolder.Length > 100
                ? string.Concat("...", appContentIndicesFolder.AsSpan(appContentIndicesFolder.Length - 100))
                : appContentIndicesFolder;
            ToolTipService.SetToolTip(IndexFolderPathTxt, appContentIndicesFolder);

            if (Directory.Exists(appContentIndicesFolder))
            {
                // Find subdirectories under AppContentIndices. These correspond to each index associated with the app.
                var indexFolders = Directory.GetDirectories(appContentIndicesFolder);
                long totalIndexSize = 0;

                if (indexFolders.Length > 0)
                {
                    foreach (var folder in indexFolders)
                    {
                        var indexSize = GetDirectorySize(folder);
                        var folderName = Path.GetFileName(folder);

                        indexStores.Add(new AppContentIndexStores(folderName, folder, indexSize));
                        totalIndexSize += indexSize;
                    }

                    ToolTipService.SetToolTip(IndexFolderPathTxt, appContentIndicesFolder);
                    TotalIndexSizeText.Text = AppUtils.FileSizeToString(totalIndexSize);
                }
                else
                {
                    TotalIndexSizeText.Text = string.Empty;
                }

                if (indexStores.Count > 0)
                {
                    IndexStorageExpander.IsExpanded = true;
                }
            }
            else
            {
                indexFolderPath = null;
                IndexFolderPathTxt.Content = "Index storage not found";
                TotalIndexSizeText.Text = string.Empty;
            }
        }
        catch (Exception ex)
        {
            indexFolderPath = null;
            IndexFolderPathTxt.Content = "Unable to locate index storage";
            TotalIndexSizeText.Text = string.Empty;
            Debug.WriteLine($"Error getting AppContentIndex storage info: {ex.Message}");
        }
    }

    private long GetDirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            return files.Sum(file => new FileInfo(file).Length);
        }
        catch
        {
            return 0;
        }
    }

    private void IndexFolderPathTxt_Click(object sender, RoutedEventArgs e)
    {
        if (indexFolderPath != null && Directory.Exists(indexFolderPath))
        {
            Process.Start("explorer.exe", indexFolderPath);
        }
        else if (indexFolderPath != null)
        {
            // If the exact path doesn't exist, try the parent directory
            var parentDir = Path.GetDirectoryName(indexFolderPath);
            if (parentDir != null && Directory.Exists(parentDir))
            {
                Process.Start("explorer.exe", parentDir);
            }
        }
    }

    private void IndexFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton hyperlinkButton && hyperlinkButton.Tag is AppContentIndexStores indexStore)
        {
            string? path = indexStore.Path;

            if (path != null && Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }
    }

    private async void DeleteIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AppContentIndexStores indexStore)
        {
            ContentDialog deleteDialog = new()
            {
                Title = "Delete index",
                Content = "Are you sure you want to delete this index? The app will recreate it when needed.",
                PrimaryButtonText = "Yes",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                CloseButtonText = "No"
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var deleteResult = AppContentIndexer.DeleteIndex(indexStore.IndexName, DeleteIndexWhileInUseBehavior.FailIfInUse);

                if (!deleteResult.Succeeded)
                {
                    ContentDialog errorDialog = new()
                    {
                        Title = $"Failed to delete index {indexStore.IndexName}",
                        Content = $"Reason: {deleteResult.Status}, {deleteResult.ExtendedError}",
                        XamlRoot = this.Content.XamlRoot,
                        CloseButtonText = "OK"
                    };
                    await errorDialog.ShowAsync();
                }

                GetAppContentIndexStorageInfo();
            }
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CachedModel model)
        {
            ContentDialog deleteDialog = new()
            {
                Title = "Delete model",
                Content = "Are you sure you want to delete this model?",
                PrimaryButtonText = "Yes",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                CloseButtonText = "No"
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await App.ModelCache.DeleteModelFromCache(model);
                GetStorageInfo();
            }
        }
    }

    private async void ResetModelConfig_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog resetDialog = new()
        {
            Title = "Reset model configuration",
            Content = "Are you sure you want to reset model configuration?\n\nDownloaded model files will not be affected.",
            PrimaryButtonText = "Reset",
            XamlRoot = this.Content.XamlRoot,
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
            CloseButtonText = "Cancel"
        };

        var result = await resetDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            // Clear usage history
            App.AppData.UsageHistoryV2?.Clear();

            // Clear user-added model mappings
            App.AppData.ModelTypeToUserAddedModelsMapping?.Clear();

            // Clear most recently used items
            App.AppData.MostRecentlyUsedItems.Clear();

            await App.AppData.SaveAsync();

            // Show confirmation
            ContentDialog confirmDialog = new()
            {
                Title = "Reset complete",
                Content = "Model configuration has been reset successfully.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await confirmDialog.ShowAsync();
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog deleteDialog = new()
        {
            Title = "Clear cache",
            Content = "Are you sure you want to clear the entire cache? All downloaded models will be deleted.",
            PrimaryButtonText = "Yes",
            XamlRoot = this.Content.XamlRoot,
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
            CloseButtonText = "No"
        };

        var result = await deleteDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await App.ModelCache.ClearCache();
            GetStorageInfo();
        }
    }

    private void Reindex_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.IndexAppSearchIndexStatic();
    }

    private void ModelFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton hyperlinkButton && hyperlinkButton.Tag is CachedModel model)
        {
            string? path = model.Path;

            if (model.IsFile)
            {
                path = Path.GetDirectoryName(path);
            }

            if (path != null && Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }
    }

    private async void DiagnosticDataToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (App.AppData.IsDiagnosticDataEnabled != DiagnosticDataToggleSwitch.IsOn)
        {
            App.AppData.IsDiagnosticDataEnabled = DiagnosticDataToggleSwitch.IsOn;
            await App.AppData.SaveAsync();
        }
    }

    private async void SemanticSearchToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (App.AppData.IsAppContentSearchEnabled != SemanticSearchToggleSwitch.IsOn)
        {
            App.AppData.IsAppContentSearchEnabled = SemanticSearchToggleSwitch.IsOn;
            await App.AppData.SaveAsync();
        }
    }

    private async void ChangeCacheFolder_Click(object sender, RoutedEventArgs e)
    {
        var downloadCount = App.ModelDownloadQueue.GetDownloads().Count;
        if (downloadCount > 0)
        {
            ContentDialog dialog = new()
            {
                Title = "Downloads in progress",
                Content = $"There are currently {downloadCount} downloads in progress. Please cancel them or wait for them to complete before changing the cache path.",
                XamlRoot = this.Content.XamlRoot,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null && folder.Path != App.ModelCache.GetCacheFolder())
        {
            if (Directory.GetFiles(folder.Path).Length > 0 || Directory.GetDirectories(folder.Path).Length > 0)
            {
                ContentDialog confirmFolderDialog = new()
                {
                    Title = "Folder not empty",
                    Content = @"The destination folder contains files. Please select an empty folder for the destination.",
                    XamlRoot = this.Content.XamlRoot,
                    CloseButtonText = "OK"
                };

                await confirmFolderDialog.ShowAsync();
                return;
            }

            var cacheSize = App.ModelCache.Models.Sum(m => m.ModelSize);

            var sourceDrive = Path.GetPathRoot(App.ModelCache.GetCacheFolder());
            var destDrive = Path.GetPathRoot(folder.Path);

            if (destDrive == null)
            {
                return;
            }

            var driveInfo = new DriveInfo(destDrive);
            var availableSpace = driveInfo.IsReady ? driveInfo.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0 : 0;

            var cacheSizeInGb = cacheSize / 1024.0 / 1024.0 / 1024.0;

            if (cacheSizeInGb > availableSpace && sourceDrive != destDrive)
            {
                ContentDialog dialog = new()
                {
                    Title = "Insufficient space",
                    Content = $@"You don't have enough space on drive {destDrive[0]}.

    Required space {cacheSizeInGb:N1} GB  
    Available space {availableSpace:N1} GB

Please free up some space before moving the cache.",
                    XamlRoot = this.Content.XamlRoot,
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
                return;
            }

            var result = ContentDialogResult.Primary;

            if (cacheSizeInGb > 1 && sourceDrive != destDrive)
            {
                ContentDialog confirmDialog = new()
                {
                    Title = "Confirm moving files",
                    Content = $@"You have {cacheSizeInGb:N1} GB to move, which may take a while.

You can speed things up by clearing the cache or deleting models from it first.

Do you want to proceed with the move?",
                    PrimaryButtonText = "Confirm",
                    PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                    XamlRoot = this.Content.XamlRoot,
                    CloseButtonText = "Cancel"
                };

                result = await confirmDialog.ShowAsync();
            }

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    StartMovingCache();
                    await App.ModelCache.MoveCache(folder.Path, _cts.Token);
                    GetStorageInfo();
                    EndMovingCache();
                }
                catch (Exception ex)
                {
                    EndMovingCache();
                    if (ex is OperationCanceledException)
                    {
                        return;
                    }

                    ContentDialog errorDialog = new()
                    {
                        Title = "Error moving files",
                        Content = $@"The cache folder could not be moved:
{ex.Message}",
                        XamlRoot = this.Content.XamlRoot,
                        CloseButtonText = "OK"
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }

    private void StartMovingCache()
    {
        isMovingCache = true;
        _ = ProgressDialog.ShowAsync();
    }

    private void EndMovingCache()
    {
        isMovingCache = false;
        ProgressDialog?.Hide();
        _cts?.Dispose();
        _cts = null;
    }
}