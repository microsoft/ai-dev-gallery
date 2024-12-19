// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace AIDevGallery.UnitTests;

internal sealed partial class UnitTestAppWindow : Window
{
    public UnitTestAppWindow()
    {
        this.InitializeComponent();
    }

    public void SetRootGridContent(UIElement content)
    {
        this.RootGrid.Children.Clear();
        this.RootGrid.Children.Add(content);
    }
}