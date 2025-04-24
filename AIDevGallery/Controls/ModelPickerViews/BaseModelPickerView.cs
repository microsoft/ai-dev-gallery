// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace AIDevGallery.Controls.ModelPickerViews;

internal abstract partial class BaseModelPickerView : UserControl
{
    public delegate void SelectedModelChangedEventHandler(object sender, ModelDetails? modelDetails);
    public event SelectedModelChangedEventHandler? SelectedModelChanged;
    public abstract void Load(List<ModelType> types);

    protected void OnSelectedModelChanged(object sender, ModelDetails? args)
    {
        if (args != null)
        {
            SelectedModelChanged?.Invoke(sender, args);
        }
    }

    public abstract void SelectModel(ModelDetails? modelDetails);
}