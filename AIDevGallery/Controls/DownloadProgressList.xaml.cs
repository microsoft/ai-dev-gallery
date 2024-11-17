// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;

namespace AIDevGallery.Controls
{
    internal sealed partial class DownloadProgressList : UserControl
    {
        private readonly ObservableCollection<DownloadableModel> downloadProgresses = [];
        public DownloadProgressList()
        {
            this.InitializeComponent();
            App.ModelCache.DownloadQueue.ModelsChanged += DownloadQueue_ModelsChanged;
            PopulateModels();
        }

        private void PopulateModels()
        {
            downloadProgresses.Clear();
            foreach (var model in App.ModelCache.DownloadQueue.GetDownloads())
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
                App.ModelCache.AddModelToDownloadQueue(downloadableModel.ModelDetails);
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
    }
}