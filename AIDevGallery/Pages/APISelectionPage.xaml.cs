// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Pages;

internal sealed partial class APISelectionPage : Page
{
    public APISelectionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        NavigatedToPageEvent.Log(nameof(APISelectionPage));
        SetupAPIs();
        NavView.Loaded += (sender, args) =>
        {
            if (e.Parameter is ModelType type)
            {
                SetSelectedAPIInMenu(type);
            }
            else
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        };
    }

    private void SetupAPIs()
    {
        if (ModelTypeHelpers.ParentMapping.TryGetValue(ModelType.WCRAPIs, out List<ModelType>? innerItems))
        {
            foreach (var item in innerItems)
            {
                if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(item, out var apiDefinition))
                {
                    NavView.MenuItems.Add(new NavigationViewItem() { Content = apiDefinition.Name, Icon = new FontIcon() { Glyph = apiDefinition.IconGlyph }, Tag = item });
                }
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag is ModelType type)
            {
                NavFrame.Navigate(typeof(ModelPage), type);
            }
            else
            {
                NavFrame.Navigate(typeof(WCROverview));
            }
        }
    }

    private void SetSelectedAPIInMenu(ModelType selectedType)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag is ModelType mt && mt == selectedType)
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }
    }
}