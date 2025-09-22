// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using AIDevGallery.Utils;

namespace AIDevGallery.Controls;

internal sealed partial class GettingStartedSection : UserControl
{
    public GettingStartedSection()
    {
        this.InitializeComponent();
    }

    public string AiLanguageModelToken => LimitedAccessFeaturesHelper.GetAiLanguageModelToken();

    private void APIButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindow.Navigate("APIs");
    }

    private void ModelsBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindow.Navigate("Models");
    }

    private void SamplesBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindow.Navigate("Samples");
    }
}