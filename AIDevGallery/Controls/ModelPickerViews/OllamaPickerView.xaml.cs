// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AIDevGallery.Controls.LanguageModelPickerViews;

internal sealed partial class OllamaPickerView : BaseModelPickerView
{
    ObservableCollection<ModelDetails> Models = new ObservableCollection<ModelDetails>();

    public OllamaPickerView()
    {
        this.InitializeComponent();
    }

    public override void Load(List<ModelType> types)
    {
        // add ollama models
        var ollamaModels = OllamaHelper.GetOllamaModels();

        if (ollamaModels != null)
        {
            ollamaModels.Select(om => new ModelDetails()
            {
                Id = $"ollama-{om.Id}",
                Name = om.Name,
                Url = $"ollama://{om.Name}:{om.Tag}",
                Description = $"{om.Name}:{om.Tag} running locally via Ollama",
                HardwareAccelerators = new List<HardwareAccelerator>() { HardwareAccelerator.OLLAMA },
                Size = AppUtils.StringToFileSize(om.Size),
                SupportedOnQualcomm = true,
                ParameterSize = om.Tag.ToUpperInvariant(),
            }).ToList().ForEach(m => Models.Add(m));
        }
    }

    private void SetSelectedModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null)
        {
            ModelSelectionItemsView.Select(Models.IndexOf(modelDetails));
        }
        else
        {
            ModelSelectionItemsView.DeselectAll();
        }
    }

    private void OllamaCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(OllamaHelper.GetOllamaUrl());
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void OllamaViewModelDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = $"https://ollama.com/library/{details.Name}",
                UseShellExecute = true
            });
        }
    }

    private void ModelSelectionItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        OnSelectedModelChanged(this, sender.SelectedItem as ModelDetails);
    }
}