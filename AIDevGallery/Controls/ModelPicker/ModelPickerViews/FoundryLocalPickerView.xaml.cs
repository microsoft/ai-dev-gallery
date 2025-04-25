// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
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
    private ObservableCollection<ModelDetails> models = new ObservableCollection<ModelDetails>();
    private FoundryLocalModelProvider provider;
    public FoundryLocalPickerView()
    {
        this.InitializeComponent();
    }

    public override async Task Load(List<ModelType> types)
    {

        provider = new FoundryLocalModelProvider();
        await provider.InitializeAsync();

        var foundryModels = await provider.GetModelsAsync() ?? [];

        foreach (var model in foundryModels)
        {
            models.Add(model);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var url = provider.Url;
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
            var modelDetailsUrl = provider.GetDetailsUrl(details);
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

    private void ModelSelectionItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        OnSelectedModelChanged(this, sender.SelectedItem as ModelDetails);
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null && models.Contains(modelDetails))
        {
            var foundModel = models.FirstOrDefault(m => m.Id == modelDetails.Id);
            if (foundModel != null)
            {
                ModelSelectionItemsView.Select(models.IndexOf(foundModel));
            }
            else
            {
                ModelSelectionItemsView.DeselectAll();
            }
        }
        else
        {
            ModelSelectionItemsView.DeselectAll();
        }
    }
}