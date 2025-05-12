// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Controls.ModelPickerViews;

internal sealed partial class WinAIApiPickerView : BaseModelPickerView
{
    private ObservableCollection<ModelDetails> models = new();

    public WinAIApiPickerView()
    {
        this.InitializeComponent();
    }

    public override Task Load(List<ModelType> types)
    {
        List<ModelDetails> modelDetails = [];

        // filter to only wcr apis
        foreach (var type in types)
        {
            if (type == ModelType.LanguageModels)
            {
                modelDetails.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(ModelType.PhiSilica));
            }
            else
            {
                modelDetails.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type)
                    .Where(m => m.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI)));
            }
        }

        modelDetails.DistinctBy(m => m.Id).ToList().ForEach(models.Add);

        return Task.CompletedTask;
    }

    private void ModelSelectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView modelView && modelView.SelectedItem is ModelDetails details)
        {
            OnSelectedModelChanged(this, details);
        }
    }

    private void ApiDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            App.MainWindow.Navigate("apis", details);
        }
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null )
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