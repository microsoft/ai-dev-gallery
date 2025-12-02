// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls.ModelPickerViews;
using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;

internal sealed partial class ModelOrApiPicker : UserControl
{
    private ObservableCollection<ModelSelectionItem> modelSelectionItems = new ObservableCollection<ModelSelectionItem>();
    private int selectedModelSelectionIndex = -1;

    public delegate void SelectedModelsChangedEventHandler(object sender, List<ModelDetails?> modelDetails);
    public event SelectedModelsChangedEventHandler? SelectedModelsChanged;
    public event EventHandler? Closed;

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

        ValidateSaveButton();
        this.Visibility = Visibility.Visible;
        CancelButton.Focus(FocusState.Programmatic);
    }

    public void Hide()
    {
        this.Visibility = Visibility.Collapsed;
        OnClosed();
    }

    private void OnClosed()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<List<ModelDetails?>> Load(List<List<ModelType>> modelOrApiTypes, ModelDetails? initialModelToLoad = null)
    {
        List<ModelDetails?> selectedModels = [];
        modelSelectionItems.Clear();

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

            // include only cached models if onnx
            models = models.Where(m => !m.IsOnnxModel() || (m.IsOnnxModel() && App.ModelCache.IsModelCached(m.Url))).ToList();

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
                        modelToPreselect = matchedModels.FirstOrDefault(m => m.HardwareAccelerators.Contains(modelOrApiUsageHistory.HardwareAccelerator.Value));
                    }

                    if (modelToPreselect == null)
                    {
                        modelToPreselect = matchedModels[0];
                    }
                }
            }

            selectedModels.Add(modelToPreselect);
        }

        // Initialize selection (first item) for repeater
        if (modelSelectionItems.Count > 0)
        {
            SetSelectedIndex(0);
        }

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
            models.AddRange(await ExternalModelHelper.GetAllModelsAsync() ?? []);
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

    private void SetSelectedIndex(int index)
    {
        if (index < 0 || index >= modelSelectionItems.Count)
        {
            return;
        }

        selectedModelSelectionIndex = index;
        _ = LoadModels(modelSelectionItems[index].ModelTypes);
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
                pickers.Add(ModelPickerDefinition.Definitions["winai"]);
            }
        }

        foreach (var def in pickers)
        {
            if (await def.IsAvailable())
            {
                modelTypeSelector.Items.Add(def);
            }
        }

        modelTypeSelector.SelectedItem = modelTypeSelector.Items[0];
        if (modelTypeSelector.Items.Count > 1)
        {
            VisualStateManager.GoToState(this, "SidePaneVisible", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "SidePaneCollapsed", true);
        }
    }

    private void OnSave_Clicked(object sender, RoutedEventArgs e)
    {
        var selectedModels = modelSelectionItems
            .Select(item => item.SelectedModel)
            .ToList();

        OnSelectedModelsChanged(this, selectedModels);
        Hide();
    }

    private void OnCancel_Clicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ModelTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView listView && listView.SelectedItem is ModelPickerDefinition pickerDefinition)
        {
            modelsGrid.Children.Clear();

            BaseModelPickerView? modelPickerView = null;

            var modelSelectionItem = GetCurrentSelection();

            if (modelSelectionItem == null)
            {
                return;
            }

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

                Implicit.SetShowAnimations(modelPickerView, (ImplicitAnimationSet)Application.Current.Resources["DefaultShowAnimationsSet"]);
                modelsGrid.Children.Add(modelPickerView);
            }
        }
    }

    private void ModelPickerView_SelectedModelChanged(object sender, ModelDetails? modelDetails)
    {
        var modelSelectionItem = GetCurrentSelection();

        if (modelSelectionItem == null)
        {
            return;
        }

        modelSelectionItem.SelectedModel = modelDetails;

        ValidateSaveButton();
    }

    private void ValidateSaveButton()
    {
        bool isEnabled = modelSelectionItems.All(ms => ms.SelectedModel != null);

        SaveButton.IsEnabled = isEnabled;
    }

    private void ShadowGrid_Loaded(object sender, RoutedEventArgs e)
    {
        DialogShadow.Receivers.Add(sender as UIElement);
    }

    private void ShadowGrid_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        Hide();
    }

    private ModelSelectionItem? GetCurrentSelection()
    {
        if (selectedModelSelectionIndex >= 0 && selectedModelSelectionIndex < modelSelectionItems.Count)
        {
            return modelSelectionItems[selectedModelSelectionIndex];
        }

        return null;
    }

    private void SelectedModelItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ModelSelectionItem msi)
        {
            var index = modelSelectionItems.IndexOf(msi);
            if (index >= 0)
            {
                SetSelectedIndex(index);
            }
        }
    }

    private void SelectedModelItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ModelSelectionItem msi)
        {
            var index = modelSelectionItems.IndexOf(msi);
            if (index < 0)
            {
                return;
            }

            // Activate selection with Enter or Space.
            if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Space)
            {
                SetSelectedIndex(index);
                e.Handled = true;
                return;
            }

            // Horizontal navigation with Left/Right arrows.
            if (e.Key == Windows.System.VirtualKey.Right)
            {
                var next = index + 1;
                if (next < modelSelectionItems.Count)
                {
                    SetSelectedIndex(next);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Left)
            {
                var prev = index - 1;
                if (prev >= 0)
                {
                    SetSelectedIndex(prev);
                    e.Handled = true;
                }
            }
        }
    }
}

internal class ModelSelectionItem : ObservableObject
{
    private ModelDetails? selectedModel;
    public ModelDetails? SelectedModel
    {
        get => selectedModel;
        set
        {
            if (SetProperty(ref selectedModel, value))
            {
                OnPropertyChanged(nameof(AccessibleName));
            }
        }
    }

    public List<ModelType> ModelTypes { get; set; }

    public ModelSelectionItem(List<ModelType> modelTypes)
    {
        ModelTypes = modelTypes;
    }

    public string AccessibleName => SelectedModel?.Name ?? "No model selected";

    public Dictionary<string, BaseModelPickerView> ModelPickerViews { get; private set; } = new();
}