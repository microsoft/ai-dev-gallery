// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;

internal sealed partial class LemonadePickerView : BaseModelPickerView
{
    private ObservableCollection<ModelDetails> models = new ObservableCollection<ModelDetails>();

    public LemonadePickerView()
    {
        this.InitializeComponent();
    }

    public override async Task Load(List<ModelType> types)
    {
        VisualStateManager.GoToState(this, "ShowLoading", true);
        var lemonadeModels = await LemonadeModelProvider.Instance.GetModelsAsync(ignoreCached: true) ?? [];
        lemonadeModels.ToList().ForEach(models.Add);
        VisualStateManager.GoToState(this, "ShowModels", true);
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

    private void ModelSelectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView modelView && modelView.SelectedItem is ModelDetails details)
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
                ModelSelectionView.SelectedIndex = models.IndexOf(foundModel);
            }
            else
            {
                ModelSelectionView.SelectedItem = null;
            }
        }
        else
        {
            ModelSelectionView.SelectedItem = null;
        }
    }
}