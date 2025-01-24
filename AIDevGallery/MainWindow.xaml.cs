// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Models;
using AIDevGallery.Pages;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.System;
using WinUIEx;

namespace AIDevGallery;

internal sealed partial class MainWindow : WindowEx
{
    private readonly Stack<object?> _navItemHistory = new();
    private NavigationViewItem? _currentSelectedNavItem;
    private bool IsInnerNavViewPaneVisible => InnerNavView.OpenPaneLength > 0;

    public MainWindow(object? obj = null)
    {
        this.InitializeComponent();
        SetTitleBar();
        App.ModelCache.DownloadQueue.ModelsChanged += DownloadQueue_ModelsChanged;

        NavView.ItemInvoked += NavView_ItemInvoked;
        NavView.Loaded += (sender, args) =>
        {
            if (obj is List<ModelType> list)
            {
                NavigateToPage(list.FirstOrDefault());
            }
            else
            {
                NavigateToPage(obj);
            }
        };

        Closed += async (sender, args) =>
        {
            if (SampleContainer.AnySamplesLoading())
            {
                this.Hide();
                args.Handled = true;
                await SampleContainer.WaitUnloadAllAsync();
                Close();
            }
        };
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(args.InvokedItem.ToString()!);
        UpdateNavViewSelectedItem();
    }

    public void Navigate(string Tag, object? obj = null)
    {
        Tag = Tag.ToLower(CultureInfo.CurrentCulture);

        switch (Tag)
        {
            case "home":
                if (NavFrame.SourcePageType != typeof(HomePage))
                {
                    NavFrame.Navigate(typeof(HomePage));
                    _navItemHistory.Push(null);
                }

                HideInnerPane();
                break;
            case "samples":
                ShowScenariosClicked(obj);
                break;
            case "models":
                ShowModelsClicked(obj);
                break;
            case "feedback":
                _ = Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/ai-dev-gallery/issues"));
                break;
            case "settings":
                if (NavFrame.SourcePageType != typeof(SettingsPage))
                {
                    NavFrame.Navigate(typeof(SettingsPage));
                    _navItemHistory.Push(null);
                }

                HideInnerPane();
                break;
        }
    }

    public void NavigateToPage(object? obj)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (obj is Scenario)
            {
                Navigate("Samples", obj);
            }
            else if (obj is ModelType)
            {
                Navigate("Models", obj);
            }
            else
            {
                Navigate("Home");
            }
        });
    }

    private void SetTitleBar()
    {
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(titleBar);
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.AppWindow.SetIcon("Assets/AppIcon/Icon.ico");

        this.Title = Windows.ApplicationModel.Package.Current.DisplayName;

        if (this.Title.EndsWith("Dev", StringComparison.InvariantCulture))
        {
            titleBar.Subtitle = "Dev";
        }
        else if (this.Title.EndsWith("Preview", StringComparison.InvariantCulture))
        {
            titleBar.Subtitle = "Preview";
        }
    }

    private void DownloadQueue_ModelsChanged(ModelDownloadQueue sender)
    {
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressRing.IsActive = sender.GetDownloads().Count > 0;
        DownloadFlyout.ShowAt(DownloadBtn);
    }

    private void ManageModelsClicked(object sender, RoutedEventArgs e)
    {
        Navigate("settings", "ModelManagement");
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var filteredSearchResults = App.SearchIndex.Where(sr => sr.Label.Contains(sender.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            SearchBox.ItemsSource = filteredSearchResults.OrderByDescending(i => i.Label.StartsWith(sender.Text, StringComparison.CurrentCultureIgnoreCase)).ThenBy(i => i.Label);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchResult result)
        {
            NavigateToPage(result.Tag);
        }

        SearchBox.Text = string.Empty;
    }

    private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            _navItemHistory.Pop();
            NavFrame.GoBack();
        }
    }

    private void HideInnerPane()
    {
        // InnerNavView.IsPaneVisible = false; throws exception https://github.com/microsoft/microsoft-ui-xaml/issues/6715
        InnerNavView.OpenPaneLength = 0d;
    }

    private void ShowInnerPane()
    {
        // InnerNavView.IsPaneVisible = true; throws exception randomly https://github.com/microsoft/microsoft-ui-xaml/issues/6715
        InnerNavView.OpenPaneLength = 248d;
    }

    private void NavFrame_Navigating(object sender, Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
    {
        UpdateNavViewSelectedItem(e.SourcePageType);
        if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.Back)
        {
            if (e.SourcePageType == typeof(ScenarioPage))
            {
                ShowScenariosClicked(null);
            }
            else if (e.SourcePageType == typeof(ModelPage) || e.SourcePageType == typeof(AddModelPage))
            {
                ShowModelsClicked(null);
            }
            else
            {
                HideInnerPane();
            }
        }
    }

    private void UpdateNavViewSelectedItem(Type? page = null)
    {
        NavView.ItemInvoked -= NavView_ItemInvoked;
        page ??= NavFrame.SourcePageType;

        if (IsInnerNavViewPaneVisible && NavViewInnerHeader.Text == "Samples")
        {
            NavView.SelectedItem = SamplesNavItem;
        }
        else if (IsInnerNavViewPaneVisible && NavViewInnerHeader.Text == "Models")
        {
            NavView.SelectedItem = ModelsNavItem;
        }
        else
        {
            if (page == typeof(ScenarioPage))
            {
                NavView.SelectedItem = SamplesNavItem;
            }
            else if (page == typeof(ModelPage) || page == typeof(AddModelPage))
            {
                NavView.SelectedItem = ModelsNavItem;
            }
            else if (page == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.FooterMenuItems[1];
            }
            else
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        }

        _currentSelectedNavItem = NavView.SelectedItem as NavigationViewItem;
        NavView.ItemInvoked += NavView_ItemInvoked;
    }

    private void ShowScenariosClicked(object? obj)
    {
        if (_currentSelectedNavItem == SamplesNavItem && IsInnerNavViewPaneVisible && obj == null)
        {
            HideInnerPane();
            return;
        }

        if (obj is Scenario scenario)
        {
            NavigateToScenario(scenario: scenario);
        }
        else if (obj is SampleNavigationArgs samplesArgs)
        {
            NavigateToScenario(sampleArgs: samplesArgs);
        }

        if (NavViewInnerHeader.Text != "Samples")
        {
            ModelsFooter.Visibility = Visibility.Collapsed;
            InnerNavView.MenuItems.Clear();
            NavViewInnerHeader.Text = "Samples";

            foreach (var scenarioCategory in ScenarioCategoryHelpers.AllScenarioCategories)
            {
                var categoryMenu = new NavigationViewItem()
                {
                    Content = scenarioCategory.Name,
                    Icon = new FontIcon() { Glyph = scenarioCategory.Icon },
                    Tag = scenarioCategory,
                    IsExpanded = true
                };
                foreach (var sc in scenarioCategory.Scenarios)
                {
                    categoryMenu.MenuItems.Add(new NavigationViewItem() { Content = sc.Name, Tag = sc });
                }

                categoryMenu.SelectsOnInvoked = false;
                InnerNavView.MenuItems.Add(categoryMenu);
            }
        }

        ShowInnerPane();
        if (!SetSelectedScenarioInInnerMenu())
        {
            InnerNavView.SelectedItem = null;
        }
    }

    private void ShowModelsClicked(object? obj)
    {
        if (_currentSelectedNavItem == ModelsNavItem && IsInnerNavViewPaneVisible && obj == null)
        {
            HideInnerPane();
            return;
        }

        if (obj is ModelType modelType)
        {
            NavigateToModelView(modelType: modelType);
        }
        else if (obj is ModelDetails modelDetails)
        {
            NavigateToModelView(modelDetails: modelDetails);
        }

        if (NavViewInnerHeader.Text != "Models")
        {
            InnerNavView.MenuItems.Clear();
            NavViewInnerHeader.Text = "Models";

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
                var navItem = CreateFromItem(key, ModelTypeHelpers.ModelGroupDetails.ContainsKey(key));
                navItem.IsExpanded = true;
                InnerNavView.MenuItems.Add(navItem);

                if (key == ModelType.LanguageModels)
                {
                    languageModelsNavItem = navItem;
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

                languageModelsNavItem.MenuItems.Add(new NavigationViewItem
                {
                    Content = "+ Add Language Model",
                    Tag = "AddModel"
                });
            }

            ModelsFooter.Visibility = Visibility.Visible;
        }

        ShowInnerPane();
        if (!SetSelectedModelInInnerMenu())
        {
            InnerNavView.SelectedItem = null;
        }
    }

    private static NavigationViewItem CreateFromItem(ModelType key, bool includeChildren)
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
                name = modelFamily.Name ?? key.ToString();
            }
            else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(key, out var apiDefinition))
            {
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
                        item.MenuItems.Add(CreateFromItem(childNavigationItem, false));
                    }
                }
            }
        }

        return item;
    }

    private void InnerNavViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag is Scenario scenario && !(_navItemHistory.TryPeek(out object? currentNavItem) && currentNavItem is Scenario sc && sc == scenario))
            {
                NavigateToScenario(scenario);
            }
            else if (item.Tag is ModelType modelType && !(_navItemHistory.TryPeek(out object? currentNavItem2) && currentNavItem2 is ModelType mt && mt == modelType))
            {
                NavigateToModelView(modelType: modelType);
            }
            else if (item.Tag is ModelDetails modelDetails && !(_navItemHistory.TryPeek(out object? currentNavItem3) && currentNavItem3 is ModelDetails md && md == modelDetails))
            {
                NavigateToModelView(modelDetails: modelDetails);
            }
            else if (item.Tag is string str && str == "AddModel" && NavFrame.SourcePageType != typeof(AddModelPage))
            {
                NavFrame.Navigate(typeof(AddModelPage));
                _navItemHistory.Push("AddModel");
            }
        }
    }

    private void NavigateToScenario(Scenario? scenario = null, SampleNavigationArgs? sampleArgs = null)
    {
        if (sampleArgs != null)
        {
            if (!(_navItemHistory.TryPeek(out object? currentNavItem) && currentNavItem is SampleNavigationArgs args && args == sampleArgs))
            {
                NavFrame.Navigate(typeof(ScenarioPage), sampleArgs);
                scenario ??= ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
                _navItemHistory.Push(scenario);
            }
        }
        else if (scenario != null)
        {
            if (!(_navItemHistory.TryPeek(out object? currentNavItem) && currentNavItem is Scenario sc && sc == scenario))
            {
                NavFrame.Navigate(typeof(ScenarioPage), scenario);
                _navItemHistory.Push(scenario);
            }
        }
    }

    private void NavigateToModelView(ModelType? modelType = null, ModelDetails? modelDetails = null)
    {
        if (modelDetails != null)
        {
            NavFrame.Navigate(typeof(ModelPage), modelDetails);
            _navItemHistory.Push(modelDetails);
        }
        else if (modelType != null)
        {
            NavFrame.Navigate(typeof(ModelPage), modelType);
            _navItemHistory.Push(modelType);
        }
    }

    private bool SetSelectedScenarioInInnerMenu(NavigationViewItem? item = null, Scenario? scenario = null)
    {
        if (scenario == null && _navItemHistory.TryPeek(out var currentNavItem) && currentNavItem is Scenario sc)
        {
            scenario = sc;
        }
        else if (scenario == null)
        {
            return false;
        }

        if (item != null && item.Tag is Scenario modelSample && modelSample.Id.Equals(scenario.Id, System.StringComparison.OrdinalIgnoreCase))
        {
            InnerNavView.SelectedItem = item;
            return true;
        }

        List<NavigationViewItem> items;

        if (item == null)
        {
            items = InnerNavView.MenuItems.Cast<NavigationViewItem>().ToList();
        }
        else
        {
            items = item.MenuItems.Cast<NavigationViewItem>().ToList();
        }

        foreach (var menuItem in items)
        {
            if (menuItem is NavigationViewItem navItem)
            {
                if (SetSelectedScenarioInInnerMenu(navItem, scenario))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool SetSelectedModelInInnerMenu(NavigationViewItem? item = null, ModelType? modelType = null, ModelDetails? modelDetails = null)
    {
        if (_navItemHistory.TryPeek(out var currentNavItem) && currentNavItem is string str && str == "AddModel" && item != null && item.Tag is string str2 && str2 == "AddModel")
        {
            InnerNavView.SelectedItem = item;
            return true;
        }

        if (modelType == null && _navItemHistory.TryPeek(out var currentNavItem2) && currentNavItem2 is ModelType mt)
        {
            modelType = mt;
        }

        if (item != null && item.Tag is ModelType itemModelType && modelType != null && itemModelType == modelType)
        {
            InnerNavView.SelectedItem = item;
            return true;
        }

        if (modelDetails == null && _navItemHistory.TryPeek(out var currentNavItem3) && currentNavItem3 is ModelDetails md)
        {
            modelDetails = md;
        }

        if (item != null && modelDetails != null && item.Tag is ModelDetails md2 && modelDetails.Id == md2.Id)
        {
            InnerNavView.SelectedItem = item;
            return true;
        }

        var items = item == null ? InnerNavView.MenuItems : item.MenuItems;

        foreach (var menuItem in items)
        {
            if (menuItem is NavigationViewItem navItem)
            {
                if (SetSelectedModelInInnerMenu(navItem, modelType, modelDetails))
                {
                    return true;
                }
            }
        }

        return false;
    }
}