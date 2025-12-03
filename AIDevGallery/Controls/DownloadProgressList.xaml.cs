// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AIDevGallery.Controls;

internal sealed partial class DownloadProgressList : UserControl
{
    private readonly ObservableCollection<DownloadableModel> downloadProgresses = [];
    public DownloadProgressList()
    {
        this.InitializeComponent();
        App.ModelDownloadQueue.ModelsChanged += DownloadQueue_ModelsChanged;
        PopulateModels();
    }

    private void PopulateModels()
    {
        downloadProgresses.Clear();
        foreach (var model in App.ModelDownloadQueue.GetDownloads())
        {
            downloadProgresses.Add(new DownloadableModel(model));
        }
    }

    private void DownloadQueue_ModelsChanged(ModelDownloadQueue sender)
    {
        foreach (var model in sender.GetDownloads())
        {
            var existingDownload = downloadProgresses.FirstOrDefault(x => x.ModelDetails.Url == model.Details.Url);
            if (existingDownload != null && existingDownload.Status == DownloadStatus.Canceled)
            {
                downloadProgresses.Remove(existingDownload);
            }

            if (existingDownload == null || existingDownload.Status == DownloadStatus.Canceled)
            {
                downloadProgresses.Add(new DownloadableModel(model));
            }
        }
    }

    private void CancelDownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            downloadableModel.CancelDownload();
        }
    }

    private void GoToModelPageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            var modelDetails = downloadableModel.ModelDetails;

            if (modelDetails != null)
            {
                App.MainWindow.Navigate("Models", modelDetails);
            }
        }
    }

    private void RetryDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            downloadProgresses.Remove(downloadableModel);
            App.ModelDownloadQueue.AddModel(downloadableModel.ModelDetails);
        }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        foreach (DownloadableModel model in downloadProgresses.ToList())
        {
            if (model.Status is DownloadStatus.Completed or DownloadStatus.Canceled)
            {
                downloadProgresses.Remove(model);
            }
        }
    }

    private async void VerificationFailedClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            var dialog = new ContentDialog
            {
                Title = "Integrity verification failed",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "The downloaded model file(s) did not match the expected hash. This could indicate the file was corrupted during download or has been tampered with.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = downloadableModel.VerificationFailureMessage ?? "Unknown verification error",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
                            FontSize = 12
                        },
                        new TextBlock
                        {
                            Text = "Would you like to keep the model anyway or delete it?",
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                },
                PrimaryButtonText = "Delete model",
                SecondaryButtonText = "Keep anyway",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Delete the model
                downloadableModel.DeleteVerificationFailedModel();
                downloadProgresses.Remove(downloadableModel);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Keep the model despite verification failure
                downloadableModel.KeepVerificationFailedModel();
            }

            // If Cancel, do nothing - leave the item in the list
        }
    }
}