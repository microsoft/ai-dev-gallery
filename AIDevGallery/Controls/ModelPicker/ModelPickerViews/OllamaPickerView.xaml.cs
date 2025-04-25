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

internal sealed partial class OllamaPickerView : BaseModelPickerView
{
    private ObservableCollection<ModelDetails> models = new ObservableCollection<ModelDetails>();

    public OllamaPickerView()
    {
        this.InitializeComponent();
    }

    public override async Task Load(List<ModelType> types)
    {
        // add ollama models
        var ollamaModels = await OllamaModelProvider.GetOllamaModelsAsync() ?? [];
        ollamaModels.ToList().ForEach(models.Add);
    }

    private void OllamaCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var url = ExternalModelHelper.GetModelUrl(details);
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(url);
            Clipboard.SetContentWithOptions(dataPackage, null);
        }
    }

    private void OllamaViewModelDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var modelDetailsUrl = ExternalModelHelper.GetModelDetailsUrl(details);
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