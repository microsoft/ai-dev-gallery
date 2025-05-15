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

internal sealed partial class OpenAIPickerView : BaseModelPickerView
{
    private ObservableCollection<ModelDetails> models = new ObservableCollection<ModelDetails>();

    public OpenAIPickerView()
    {
        this.InitializeComponent();
    }

    public override Task Load(List<ModelType> types)
    {
        return Load();
    }

    private async Task Load()
    {
        VisualStateManager.GoToState(this, "ShowLoading", true);

        var openAIModels = await OpenAIModelProvider.Instance.GetModelsAsync();

        if (openAIModels == null || !openAIModels.Any())
        {
            VisualStateManager.GoToState(this, "ShowInput", true);
        }
        else
        {
            openAIModels.ToList().ForEach(models.Add);
            VisualStateManager.GoToState(this, "ShowModels", true);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
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

    private void ViewModelDetails_Click(object sender, RoutedEventArgs e)
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
        if (sender.SelectedItem is ModelDetails details)
        {
            OnSelectedModelChanged(this, details);
        }
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

    private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OpenAIKeyTextBox.Text))
        {
            return;
        }

        OpenAIModelProvider.OpenAIKey = OpenAIKeyTextBox.Text;
        _ = Load();
    }

    private void RemoveKeyButton_Click(object sender, RoutedEventArgs e)
    {
        OpenAIModelProvider.OpenAIKey = null;
        OpenAIModelProvider.Instance.ClearCachedModels();
        _ = Load();
    }
}