// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls.LanguageModelPickerViews;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;
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

    public void Show()
    {
        this.Visibility = Visibility.Visible;
    }

    public List<ModelDetails?> Load(List<List<ModelType>> modelOrApiTypes, ModelDetails? initialModelToLoad = null)
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
            var models = GetAllModels(types);

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

    private List<ModelDetails> GetAllModels(List<ModelType> modelOrApiTypes)
    {
        List<ModelDetails> models = [];

        if (modelOrApiTypes.Contains(ModelType.LanguageModels))
        {
            // get all onnx, ollama, wcr, etc modelDetails
            models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(ModelType.LanguageModels));
            models.AddRange(OllamaHelper.GetOllamaModels() ?? []);
            // TODO: add other model types
        }
        else
        {
            // get all the models (we assume it's just onnx for now)
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

        if (selectedItem.ModelTypes.Contains(ModelType.LanguageModels))
        {
            LoadLanguageModels(selectedItem);
        }
        else
        {
            LoadNonLanguageModels(selectedItem);
        }
    }

    private void LoadLanguageModels(ModelSelectionItem selectionItem)
    {
        myLLMTypeSelector.Items.Clear();

        foreach (var (name, modelPicker) in LLMModelPickers.LLMModelPickerTypes)
        {
            myLLMTypeSelector.Items.Add(new SelectorBarItem() { Text = name, Tag = name });
        }

        myLLMTypeSelector.Visibility = Visibility.Visible;
        myLLMTypeSelector.SelectedItem = myLLMTypeSelector.Items[0];
    }

    private void LoadNonLanguageModels(ModelSelectionItem selectionItem)
    {
        myLLMTypeSelector.Items.Clear();
        myLLMTypeSelector.Visibility = Visibility.Collapsed;
        modelsGrid.Children.Clear();

        BaseModelPickerView? modelPickerView = null;

        if (selectionItem.ModelPickerViews.Count > 0)
        {
            // we only need one for non langauge models for now
            modelPickerView = selectionItem.ModelPickerViews.First().Value;
        }

        if (modelPickerView == null)
        {
            modelPickerView = new OnnxPickerView();
            modelPickerView.SelectedModelChanged += ModelPickerView_SelectedModelChanged;
            modelPickerView.Load(selectionItem.ModelTypes);
            selectionItem.ModelPickerViews.Add("ModelPickerView", modelPickerView);
        }

        modelsGrid.Children.Add(modelPickerView);
    }

    private void OnSave_Clicked(object sender, RoutedEventArgs e)
    {
        this.Visibility = Visibility.Collapsed;

        var selectedModels = modelSelectionItems
            .Select(item => item.SelectedModel)
            .ToList();

        OnSelectedModelsChanged(this, selectedModels);
    }

    private void MyLLMTypeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selectedItem = sender.SelectedItem;
        modelsGrid.Children.Clear();

        BaseModelPickerView? modelPickerView = null;

        var modelSelectionItem = SelectedModelsItemsView.SelectedItem as ModelSelectionItem;

        if (modelSelectionItem == null)
        {
            return;
        }

        if (selectedItem?.Tag is string tag
            && LLMModelPickers.LLMModelPickerTypes.TryGetValue(tag, out var type))
        {
            if (!modelSelectionItem.ModelPickerViews.TryGetValue(tag, out modelPickerView))
            {
                modelPickerView = (BaseModelPickerView?)Activator.CreateInstance(type);
                modelPickerView.SelectedModelChanged += ModelPickerView_SelectedModelChanged;
                modelPickerView!.Load(modelSelectionItem.ModelTypes);
                modelSelectionItem.ModelPickerViews[tag] = modelPickerView!;
            }

            if (modelPickerView != null)
            {
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