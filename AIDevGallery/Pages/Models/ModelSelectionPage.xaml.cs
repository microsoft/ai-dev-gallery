// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Pages;

internal record LastInternalNavigation(Type PageType, object? Parameter = null);

internal sealed partial class ModelSelectionPage : Page
{
    private static LastInternalNavigation? lastInternalNavigation;

    public ModelSelectionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        NavigatedToPageEvent.Log(nameof(ModelSelectionPage));

        SetUpModels();
        NavView.Loaded += (sender, args) =>
        {
            List<ModelType>? modelTypes = null;
            ModelDetails? details = null;
            object? parameter = e.Parameter;

            if (e.Parameter == null && lastInternalNavigation != null)
            {
                parameter = lastInternalNavigation.Parameter;
            }

            if (parameter is ModelType sample)
            {
                modelTypes = [sample];
            }
            else if (parameter is List<ModelType> samples)
            {
                modelTypes = samples;
            }
            else if (parameter is MostRecentlyUsedItem mru)
            {
                modelTypes = App.FindSampleItemById(mru.ItemId);
            }
            else if (parameter is string modelOrApiId)
            {
                modelTypes = GetFamilyModelType(App.FindSampleItemById(modelOrApiId));
            }
            else if (parameter is ModelDetails modelDetails)
            {
                details = modelDetails;
                modelTypes = GetFamilyModelType(App.FindSampleItemById(details.Id));
            }

            if (modelTypes != null || details != null)
            {
                foreach (NavigationViewItem item in NavView.MenuItems)
                {
                    SetSelectedSampleInMenu(item, modelTypes, details);
                }
            }
            else
            {
                if (NavView.MenuItems.FirstOrDefault() is NavigationViewItem item)
                {
                    if (item.MenuItems != null && item.MenuItems.Count > 0)
                    {
                        item.IsExpanded = true;
                        NavView.SelectedItem = item.MenuItems[0];
                    }
                    else
                    {
                        NavView.SelectedItem = item;
                    }
                }
            }

            static List<ModelType>? GetFamilyModelType(List<ModelType>? modelTypes)
            {
                if (modelTypes != null && modelTypes.Count > 0)
                {
                    var modelType = modelTypes.First();
                    if (ModelTypeHelpers.ModelDetails.ContainsKey(modelType))
                    {
                        var parent = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(modelType));
                        modelTypes = [parent.Key];
                    }
                }

                return modelTypes;
            }
        };

        base.OnNavigatedTo(e);
    }

    public void ShowHideNavPane()
    {
        NavView.OpenPaneLength = NavView.OpenPaneLength == 0 ? 224 : 0;
    }

    private void SetUpModels()
    {
        List<ModelType> rootModels = [.. ModelTypeHelpers.ModelGroupDetails.Keys];
        rootModels.AddRange(ModelTypeHelpers.ModelFamilyDetails.Keys);
        foreach (var key in ModelTypeHelpers.ModelFamilyDetails)
        {
            foreach (var mapping in ModelTypeHelpers.ParentMapping)
            {
                foreach (var key2 in mapping.Value)
                {
                    if (key.Key == key2)
                    {
                        rootModels.Remove(key.Key);
                    }
                }
            }
        }

        NavigationViewItem? languageModelsNavItem = null;

        foreach (var key in rootModels.OrderBy(ModelTypeHelpers.GetModelOrder))
        {
            if (key != ModelType.WCRAPIs)
            {
                var navItem = CreateFromItem(key, ModelTypeHelpers.ModelGroupDetails.ContainsKey(key));
                if (navItem != null)
                {
                    NavView.MenuItems.Add(navItem);

                    if (key == ModelType.LanguageModels)
                    {
                        languageModelsNavItem = navItem;
                    }
                }
            }
        }

        if (languageModelsNavItem != null)
        {
            var userAddedModels = App.ModelCache.Models.Where(m => m.Details.IsUserAdded).ToList();

            foreach (var cachedModel in userAddedModels)
            {
                languageModelsNavItem.MenuItems.Add(new NavigationViewItem
                {
                    Content = cachedModel.Details.Name.Split('/').Last(),
                    Tag = cachedModel.Details,
                });
            }
        }
    }

    private static NavigationViewItem? CreateFromItem(ModelType key, bool includeChildren)
    {
        string name;
        string? icon = null;
        if (ModelTypeHelpers.ModelGroupDetails.TryGetValue(key, out var modelGroup))
        {
            name = modelGroup.Name;
            icon = modelGroup.Icon;
        }
        else
        {
            if (ModelTypeHelpers.ModelFamilyDetails.TryGetValue(key, out var modelFamily))
            {
                ModelTypeHelpers.ParentMapping.TryGetValue(key, out List<ModelType>? modelTypes);
                bool hasCompattibleModel = false;

                if (modelTypes != null)
                {
                    foreach (var modelType in modelTypes)
                    {
                        if (ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out var modelDetails) && modelDetails.Compatibility.CompatibilityState != ModelCompatibilityState.NotCompatible)
                        {
                            hasCompattibleModel = true;
                            break;
                        }
                    }

                    if (!hasCompattibleModel)
                    {
                        return null;
                    }
                }

                name = modelFamily.Name ?? key.ToString();
            }
            else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(key, out var apiDefinition))
            {
                // get details from apiDefinition
                ModelDetails details = ModelDetailsHelper.GetModelDetailsFromApiDefinition(key, apiDefinition);

                if (details.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
                {
                    return null;
                }

                name = apiDefinition.Name ?? key.ToString();
            }
            else
            {
                name = key.ToString();
            }
        }

        NavigationViewItem item = new()
        {
            Content = name,
            Tag = key
        };

        if (!string.IsNullOrWhiteSpace(icon))
        {
            item.Icon = new FontIcon() { Glyph = icon };
        }

        if (ModelTypeHelpers.ParentMapping.TryGetValue(key, out List<ModelType>? innerItems))
        {
            if (innerItems?.Count > 0)
            {
                if (includeChildren)
                {
                    item.SelectsOnInvoked = false;
                    foreach (var childNavigationItem in innerItems)
                    {
                        var child = CreateFromItem(childNavigationItem, false);
                        if (child != null)
                        {
                            item.MenuItems.Add(child);
                        }
                    }
                }
            }
        }

        return item;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type pageType = typeof(ModelPage);
        object? parameter = null;

        if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag is ModelType modelType)
            {
                parameter = modelType;
            }
            else if (item.Tag is string tag && tag == "ModelManagement")
            {
                pageType = typeof(ModelSelectionPage);
            }
            else if (item.Tag is ModelDetails details)
            {
                parameter = details;
            }

            lastInternalNavigation = new LastInternalNavigation(pageType, parameter);
            NavFrame.Navigate(pageType, parameter);
        }
    }

    private void SetSelectedSampleInMenu(NavigationViewItem item, List<ModelType>? selectedSample = null, ModelDetails? details = null)
    {
        if (selectedSample == null && details == null)
        {
            return;
        }

        if (item.Tag is ModelType mt && selectedSample != null && selectedSample.Contains(mt))
        {
            NavView.SelectedItem = item;
            return;
        }

        foreach (var menuItem in item.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem)
            {
                if ((navItem.Tag is ModelType modelType && selectedSample != null && selectedSample.Contains(modelType)) ||
                    (navItem.Tag is ModelDetails modelDetails && details != null && modelDetails.Url == details.Url))
                {
                    item.IsExpanded = true;
                    NavView.SelectedItem = navItem;
                    return;
                }
                else if (navItem.MenuItems.Count > 0)
                {
                    SetSelectedSampleInMenu(navItem, selectedSample, details);
                }
            }
        }
    }

    private void AddModelClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavView.SelectedItem = null;
        NavFrame.Navigate(typeof(AddModelPage));
    }

    private void ManageModelsClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindow.Navigate("settings", "ModelManagement");
    }
}