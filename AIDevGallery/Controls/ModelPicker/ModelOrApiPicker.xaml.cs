// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls.ModelPickerViews;
using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;

// TODO: Add telemetry
internal sealed partial class ModelOrApiPicker : UserControl
{
    private ObservableCollection<ModelSelectionItem> modelSelectionItems = new ObservableCollection<ModelSelectionItem>();

    public delegate void SelectedModelsChangedEventHandler(object sender, List<ModelDetails?> modelDetails);
    public event SelectedModelsChangedEventHandler? SelectedModelsChanged;

    public ModelOrApiPicker()
    {
        this.InitializeComponent();
    }

    private void OnSelectedModelsChanged(object sender, List<ModelDetails?> args)
    {
        SelectedModelsChanged?.Invoke(sender, args);
    }

    public void Show(List<ModelDetails?>? selectedModels = null)
    {
        if (selectedModels != null && selectedModels.Count == modelSelectionItems.Count)
        {
            for (var i = 0; i < selectedModels.Count; i++)
            {
                modelSelectionItems[i].SelectedModel = selectedModels[i];
            }
        }
    }

    public async Task<List<ModelDetails?>> Load(List<List<ModelType>> modelOrApiTypes, ModelDetails? initialModelToLoad = null)
    {
        List<ModelDetails?> selectedModels = [];

        foreach (var modelOrApiType in modelOrApiTypes)
        {
            if (modelOrApiType != null)
            {
                var modelSelectionItem = new ModelSelectionItem(modelOrApiType.Distinct().ToList());
                modelSelectionItems.Add(modelSelectionItem);
            }
        }

        foreach (var types in modelOrApiTypes)
        {
            var models = await GetAllModels(types);

            ModelDetails? modelToPreselect = null;

            if (initialModelToLoad != null)
            {
                modelToPreselect = models.FirstOrDefault(m => m.Id == initialModelToLoad.Id);
            }

            var modelOrApiUsageHistory = App.AppData.UsageHistoryV2?.FirstOrDefault(u => models.Any(m => m.Id == u.Id));
            if (modelToPreselect == null && modelOrApiUsageHistory != default)
            {
                var matchedModels = models.Where(m => m.Id == modelOrApiUsageHistory.Id).ToList();
                if (matchedModels.Count > 0)
                {
                    if (modelOrApiUsageHistory.HardwareAccelerator != null)
                    {
                        var model = matchedModels.FirstOrDefault(m => m.HardwareAccelerators.Contains(modelOrApiUsageHistory.HardwareAccelerator.Value));
                        if (model != null)
                        {
                            modelToPreselect = model;
                        }
                    }

                    if (modelToPreselect == null)
                    {
                        modelToPreselect = matchedModels.FirstOrDefault();
                    }
                }
            }

            selectedModels.Add(modelToPreselect);
        }

        SelectedModelsItemsView.ItemsSource = modelSelectionItems;
        SelectedModelsItemsView.Select(0);

        ValidateSaveButton();

        return selectedModels;
    }

    private async Task<List<ModelDetails>> GetAllModels(List<ModelType> modelOrApiTypes)
    {
        List<ModelDetails> models = [];

        if (modelOrApiTypes.Contains(ModelType.LanguageModels))
        {
            // get all onnx, ollama, wcr, etc modelDetails
            models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(ModelType.LanguageModels));
            models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(ModelType.PhiSilica));
            models.AddRange(await OllamaModelProvider.GetOllamaModelsAsync() ?? []);
            // TODO: add other model types
        }
        else
        {
            // get all the models for types
            foreach (var type in modelOrApiTypes)
            {
                models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type));
            }
        }

        return models;
    }

    private void ModelSelectionItemChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        var selectedItem = sender.SelectedItem as ModelSelectionItem;
        if (selectedItem == null)
        {
            return;
        }

        _ = LoadModels(selectedItem.ModelTypes);
    }

    private async Task LoadModels(List<ModelType> types)
    {
        modelTypeSelector.Items.Clear();
        modelsGrid.Children.Clear();

        List<ModelPickerDefinition> pickers = [];

        if (types.Contains(ModelType.LanguageModels))
        {
            pickers = ModelPickerDefinition.Definitions.Values.ToList();
        }
        else
        {
            List<ModelDetails> models = await GetAllModels(types);

            if (models.Any(m => m.IsOnnxModel()) && ModelPickerDefinition.Definitions.TryGetValue("onnx", out var def))
            {
                pickers.Add(ModelPickerDefinition.Definitions["onnx"]);
            }

            if (models.Any(m => m.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI)))
            {
                pickers.Add(ModelPickerDefinition.Definitions["wcr"]);
            }
        }

        foreach (var def in pickers)
        {
            modelTypeSelector.Items.Add(new SelectorBarItem() { Icon = new ImageIcon() { Source = new BitmapImage(new Uri(def.Icon)), Height = 20 },  Text = def.Name, Tag = def });
        }

        modelTypeSelector.SelectedItem = modelTypeSelector.Items[0];
    }

    private void OnSave_Clicked(object sender, RoutedEventArgs e)
    {
     

        var selectedModels = modelSelectionItems
            .Select(item => item.SelectedModel)
            .ToList();

        OnSelectedModelsChanged(this, selectedModels);
    }

    private void ModelTypeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selectedItem = sender.SelectedItem;
        modelsGrid.Children.Clear();

        BaseModelPickerView? modelPickerView = null;

        var modelSelectionItem = SelectedModelsItemsView.SelectedItem as ModelSelectionItem;

        if (modelSelectionItem == null)
        {
            return;
        }

        if (selectedItem?.Tag is ModelPickerDefinition pickerDefinition)
        {
            if (!modelSelectionItem.ModelPickerViews.TryGetValue(pickerDefinition.Id, out modelPickerView))
            {
                modelPickerView = pickerDefinition.CreatePicker();
                modelPickerView.SelectedModelChanged += ModelPickerView_SelectedModelChanged;
                modelPickerView!.Load(modelSelectionItem.ModelTypes);
                modelSelectionItem.ModelPickerViews[pickerDefinition.Id] = modelPickerView!;
            }

            if (modelPickerView != null)
            {
                modelPickerView.SelectModel(modelSelectionItem.SelectedModel);
                modelsGrid.Children.Add(modelPickerView);
            }
        }
    }

    private void ModelPickerView_SelectedModelChanged(object sender, ModelDetails? modelDetails)
    {
        var modelSelectionItem = SelectedModelsItemsView.SelectedItem as ModelSelectionItem;

        if (modelSelectionItem == null)
        {
            return;
        }

        modelSelectionItem.SelectedModel = modelDetails;

        ValidateSaveButton();
    }

    private void ValidateSaveButton()
    {
        foreach (var item in modelSelectionItems)
        {
            if (item.SelectedModel == null)
            {
                SaveButton.IsEnabled = false;
                return;
            }
        }

        SaveButton.IsEnabled = true;
    }
}

internal class ModelSelectionItem : ObservableObject
{
    private ModelDetails? selectedModel;
    public ModelDetails? SelectedModel
    {
        get => selectedModel;
        set => SetProperty(ref selectedModel, value);
    }

    public List<ModelType> ModelTypes { get; set; }

    public ModelSelectionItem(List<ModelType> modelTypes)
    {
        ModelTypes = modelTypes;
    }

    public Dictionary<string, BaseModelPickerView> ModelPickerViews { get; private set; } = new();
}