// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace AIDevGallery.Controls.LanguageModelPickerViews;

internal abstract partial class BaseModelPickerView : UserControl
{
    public delegate void SelectedModelChangedEventHandler(object sender, ModelDetails? modelDetails);
    public event SelectedModelChangedEventHandler? SelectedModelChanged;
    public abstract void Load(List<ModelType> types);

    protected void OnSelectedModelChanged(object sender, ModelDetails? args)
    {
        SelectedModelChanged?.Invoke(sender, args);
    }
}