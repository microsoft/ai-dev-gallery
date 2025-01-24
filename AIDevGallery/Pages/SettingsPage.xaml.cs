// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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
    private readonly RelayCommand endMoveCommand;
    private string? cacheFolderPath;
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

        // DiagnosticDataToggleSwitch.IsOn = App.AppData.IsDiagnosticDataEnabled;
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

    private void GetStorageInfo()
    {
        cachedModels.Clear();

        cacheFolderPath = App.ModelCache.GetCacheFolder();
        FolderPathTxt.Content = cacheFolderPath;

        long totalCacheSize = 0;

        foreach (var cachedModel in App.ModelCache.Models.OrderBy(m => m.Details.Name))
        {
            cachedModels.Add(cachedModel);
            totalCacheSize += cachedModel.ModelSize;
        }

        if (App.ModelCache.Models.Count > 0)
        {
            ModelsExpander.IsExpanded = true;
        }

        TotalCacheTxt.Text = AppUtils.FileSizeToString(totalCacheSize);
    }

    private void FolderPathTxt_Click(object sender, RoutedEventArgs e)
    {
        if (cacheFolderPath != null)
        {
            Process.Start("explorer.exe", cacheFolderPath);
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

    private void ModelFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton hyperlinkButton && hyperlinkButton.Tag is CachedModel model)
        {
            string? path = model.Path;

            if (model.IsFile)
            {
                path = Path.GetDirectoryName(path);
            }

            if (path != null)
            {
                Process.Start("explorer.exe", path);
            }
        }
    }

    // private async void DiagnosticDataToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    // {
    //    if (App.AppData.IsDiagnosticDataEnabled != DiagnosticDataToggleSwitch.IsOn)
    //    {
    //        App.AppData.IsDiagnosticDataEnabled = DiagnosticDataToggleSwitch.IsOn;
    //        await App.AppData.SaveAsync();
    //    }
    // }
    private async void ChangeCacheFolder_Click(object sender, RoutedEventArgs e)
    {
        var downloadCount = App.ModelCache.DownloadQueue.GetDownloads().Count;
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