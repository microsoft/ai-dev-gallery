// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls.LanguageModelPickerViews;
using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace AIDevGallery.Controls;
internal sealed partial class ModelOrApiPicker : UserControl
{
    public ModelOrApiPicker()
    {
        this.InitializeComponent();
    }

    public void Show()
    {
        this.Visibility = Visibility.Visible;
    }

    public async Task Load(List<ModelType> modelOrApiTypes, List<ModelType>? modelOrApiTypes2)
    {
        modelOrApiTypes = modelOrApiTypes.Distinct().ToList();
        modelOrApiTypes2 = modelOrApiTypes2?.Distinct().ToList();

        // TODO: handle multiple models

        // if language model - load one experience
        // ortherwise load the second experience

        if (modelOrApiTypes.Contains(ModelType.LanguageModels))
        {
            await LoadLanguageModels();
        }
        else
        {
            await LoadNonLanguageModels();
        }
    }

    private async Task LoadLanguageModels()
    {
        myLLMTypeSelector.Items.Add(new SelectorBarItem() { Text = "Onnx Models", Tag = "onnx", IsSelected = true });
        myLLMTypeSelector.Items.Add(new SelectorBarItem() { Text = "Ollama", Tag = "ollama" });
    }

    private async Task LoadNonLanguageModels()
    {

    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        this.Visibility = Visibility.Collapsed;
    }

    private void myLLMTypeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var selectedItem = sender.SelectedItem as SelectorBarItem;
        modelsGrid.Children.Clear();
        BaseModelPickerView modelPickerView = null;

        switch (selectedItem?.Tag)
        {
            case "onnx":
                modelPickerView = new OnnxLLMPickerView();
                break;
            case "ollama":
                modelPickerView = new OllamaLLMPickerView();
                break;
            default:
                break;
        }

        if (modelPickerView != null)
        {
            modelsGrid.Children.Add(modelPickerView);
            modelPickerView.Load();
        }
    }
}