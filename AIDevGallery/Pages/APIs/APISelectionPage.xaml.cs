// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Linq;

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
                SetSelectedApiInMenu(type);
            }
            else if (e.Parameter is ModelDetails details &&
                    ModelTypeHelpers.ApiDefinitionDetails.Any(md => md.Value.Id == details.Id))
            {
                var apiType = ModelTypeHelpers.ApiDefinitionDetails.FirstOrDefault(md => md.Value.Id == details.Id).Key;
                SetSelectedApiInMenu(apiType);
            }
            else
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        };
    }

    private static string GetContentText(object? content)
    {
        return content switch
        {
            string s => s,
            TextBlock tb => tb.Text,
            _ => string.Empty
        };
    }

    private static TextBlock CreateWrappedText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            TextTrimming = TextTrimming.None,
            Margin = new Thickness(0, 0, 8, 0)
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
                    if (!string.IsNullOrWhiteSpace(apiDefinition.Category))
                    {
                        NavigationViewItem? existingItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => GetContentText(i.Content) == apiDefinition.Category);

                        if (existingItem == null)
                        {
                            existingItem = new NavigationViewItem() { Content = CreateWrappedText(apiDefinition.Category), Icon = new FontIcon() { Glyph = "\uF0E2" }, SelectsOnInvoked = false, IsExpanded = true };
                            NavView.MenuItems.Add(existingItem);
                        }

                        existingItem.MenuItems.Add(new NavigationViewItem() { Content = CreateWrappedText(apiDefinition.Name), Icon = new FontIcon() { Glyph = apiDefinition.IconGlyph }, Tag = item });
                    }
                    else
                    {
                        NavView.MenuItems.Add(new NavigationViewItem() { Content = CreateWrappedText(apiDefinition.Name), Icon = new FontIcon() { Glyph = apiDefinition.IconGlyph }, Tag = item });
                    }
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
                NavFrame.Navigate(typeof(APIPage), type);
            }
            else
            {
                NavFrame.Navigate(typeof(APIOverview));
            }
        }
    }

    public void SetSelectedApiInMenu(ModelType selectedType)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem)
            {
                if (navItem.Tag is ModelType mt && mt == selectedType)
                {
                    NavView.SelectedItem = navItem;
                    return;
                }
                else if (navItem.MenuItems != null && navItem.MenuItems.Count > 0)
                {
                    foreach (var subItem in navItem.MenuItems.OfType<NavigationViewItem>())
                    {
                        if (subItem.Tag is ModelType subType && subType == selectedType)
                        {
                            NavView.SelectedItem = subItem;
                            return;
                        }
                    }
                }
            }
        }
    }

    public void ShowHideNavPane()
    {
        NavView.OpenPaneLength = NavView.OpenPaneLength == 0 ? 276 : 0;
    }
}