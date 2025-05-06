// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;

internal sealed partial class FoundryLocalPickerView : BaseModelPickerView
{
    private ObservableCollection<ModelDetails> AvailableModels { get; } = [];
    private ObservableCollection<DownloadableModel> DownloadableModels { get; } = [];

    private string FoundryLocalUrl => FoundryLocalModelProvider.Instance?.Url ?? string.Empty;

    public FoundryLocalPickerView()
    {
        this.InitializeComponent();

        App.ModelDownloadQueue.ModelDownloadCompleted += ModelDownloadQueue_ModelDownloadCompleted;
    }

    private void ModelDownloadQueue_ModelDownloadCompleted(object? sender, Utils.ModelDownloadCompletedEventArgs e)
    {
        _ = Load([]);
    }

    public override async Task Load(List<ModelType> types)
    {
        AvailableModels.Clear();
        DownloadableModels.Clear();

        (await FoundryLocalModelProvider.Instance.GetModelsAsync() ?? [])
            .ToList()
            .ForEach(AvailableModels.Add);

        var catalogModels = FoundryLocalModelProvider.Instance.GetAllModelsInCatalog()
            .Where(m => !AvailableModels.Any(cm => cm.Name == m.Name))
            .ToList();

        foreach (var m in catalogModels)
        {
            DownloadableModels.Add(new DownloadableModel(m));
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var url = FoundryLocalModelProvider.Instance?.Url;
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(url);
            Clipboard.SetContentWithOptions(dataPackage, null);
        }
    }

    private void ViewModelDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var modelDetailsUrl = FoundryLocalModelProvider.Instance?.GetDetailsUrl(details);
            if (string.IsNullOrEmpty(modelDetailsUrl))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = modelDetailsUrl,
                UseShellExecute = true
            });
        }
    }

    private void ModelSelectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView modelView && modelView.SelectedItem is ModelDetails model)
        {
            OnSelectedModelChanged(this, model);
        }
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null && AvailableModels.Contains(modelDetails))
        {
            var foundModel = AvailableModels.FirstOrDefault(m => m.Id == modelDetails.Id);
            if (foundModel != null)
            {
                ModelSelectionItemsView.SelectedIndex = AvailableModels.IndexOf(foundModel);
            }
            else
            {
                ModelSelectionItemsView.SelectedItem = null;
            }
        }
        else
        {
            ModelSelectionItemsView.SelectedItem = null;
        }
    }

    private void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            downloadableModel.StartDownload();
        }
    }
}