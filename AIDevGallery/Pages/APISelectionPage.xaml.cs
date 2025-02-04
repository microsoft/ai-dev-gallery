// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery;
using AIDevGallery.Models;
using AIDevGallery.Pages;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AIDevGallery.Pages;

public sealed partial class APISelectionPage : Page
{
    public APISelectionPage()
    {
        this.InitializeComponent();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type pageType = typeof(ModelPage);
        object? parameter = null;


        if (args.SelectedItem is NavigationViewItem item)
        {

            if (item.Tag is string param)
            {
                if (param == "Overview")
                {
                    pageType = typeof(WCROverview);
                }
                else
                {
                    switch (param)
                    {
                        case "PhiSilica":
                            parameter = ModelType.PhiSilica;
                            break;
                        case "ImageScaler":
                            parameter = ModelType.ImageScaler;
                            break;
                        case "OCR":
                            parameter = ModelType.TextRecognitionOCR;
                            break;
                        case "BackgroundRemover":
                            parameter = ModelType.BackgroundRemover;
                            break;
                        case "ImageDescription":
                            parameter = ModelType.ImageDescription;
                            break;
                    }
                }
            }

            NavFrame.Navigate(pageType, parameter);
        }
    }

    private void NavView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];
    }
}