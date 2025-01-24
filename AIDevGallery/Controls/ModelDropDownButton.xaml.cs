// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Controls;

internal sealed partial class ModelDropDownButton : UserControl
{
    public ModelDropDownButton()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty FlyoutContentProperty = DependencyProperty.Register(nameof(FlyoutContent), typeof(object), typeof(ModelDropDownButton), new PropertyMetadata(defaultValue: null));

    public object FlyoutContent
    {
        get => (object)GetValue(FlyoutContentProperty);
        set => SetValue(FlyoutContentProperty, value);
    }

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(ModelDetails), typeof(ModelDropDownButton), new PropertyMetadata(defaultValue: null));

    public ModelDetails? Model
    {
        get => (ModelDetails)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public void HideFlyout()
    {
        DropDown.Flyout.Hide();
    }
}