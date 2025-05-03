// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Pages;
using AIDevGallery.Utils;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Windows.System;
using WinUIEx;

namespace AIDevGallery;

internal sealed partial class MainWindow : WindowEx
{
    private ObservableCollection<SearchResult> searchResults = [];

    public MainWindow(object? obj = null)
    {
        this.InitializeComponent();
        SetTitleBar();
        App.ModelCache.DownloadQueue.ModelsChanged += DownloadQueue_ModelsChanged;

        this.NavView.Loaded += (sender, args) =>
        {
            NavigateToPage(obj);
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

    public void NavigateToPage(object? obj)
    {
        if (obj is Scenario)
        {
            Navigate("Samples", obj);
        }
        else if (obj is ModelType modelType)
        {
            NavigateToApiOrModelPage(modelType);
        }
        else if (obj is List<ModelType> modelTypes && modelTypes.Count > 0)
        {
            NavigateToApiOrModelPage(modelTypes[0]);
        }
        else if (obj is ModelDetails)
        {
            Navigate("Models", obj);
        }
        else
        {
            Navigate("Home");
        }
    }

    private void NavigateToApiOrModelPage(ModelType modelType)
    {
        if (ModelDetailsHelper.EqualOrParent(modelType, ModelType.WCRAPIs))
        {
            Navigate("APIs", modelType);
        }
        else
        {
            Navigate("Models", modelType);
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(args.InvokedItemContainer.Tag.ToString()!);
    }

    public void Navigate(string Tag, object? obj = null)
    {
        Tag = Tag.ToLower(CultureInfo.CurrentCulture);

        switch (Tag)
        {
            case "home":
                Navigate(typeof(HomePage));
                break;
            case "samples":
                Navigate(typeof(ScenarioSelectionPage), obj);
                break;
            case "models":
                Navigate(typeof(ModelSelectionPage), obj);
                break;
            case "apis":
                Navigate(typeof(APISelectionPage), obj);
                break;
            case "contribute":
                _ = Launcher.LaunchUriAsync(new Uri("https://aka.ms/ai-dev-gallery-repo"));
                break;
            case "settings":
                Navigate(typeof(SettingsPage), obj);
                break;
        }
    }

    private void Navigate(Type page, object? param = null)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (page == typeof(APISelectionPage) && NavFrame.Content is APISelectionPage apiPage && param != null)
            {
                // No need to navigate to the APISelectionPage again, we just want to navigate to the right subpage
                apiPage.SetSelectedApiInMenu((ModelType)param);
            }
            else if (page == typeof(ScenarioSelectionPage) && NavFrame.Content is ScenarioSelectionPage scenarioPage && param != null)
            {
                // No need to navigate to the ScenarioSelectionPage again, we just want to navigate to the right subpage
                scenarioPage.HandleNavigation(param);
            }
            else
            {
                if (param == null && NavFrame.Content != null && NavFrame.Content.GetType() == page)
                {
                    if (NavFrame.Content is ScenarioSelectionPage scenario)
                    {
                        scenario.ShowHideNavPane();
                    }
                    else if (NavFrame.Content is ModelSelectionPage model)
                    {
                        model.ShowHideNavPane();
                    }
                    else if (NavFrame.Content is APISelectionPage api)
                    {
                        api.ShowHideNavPane();
                    }

                    return;
                }
                else
                {
                    NavFrame.Navigate(page, param);
                }
            }
        });
    }

    public void Navigate(MostRecentlyUsedItem mru)
    {
        if (mru.Type == MostRecentlyUsedItemType.Model)
        {
            Navigate("models", mru);
        }
        else
        {
            Navigate("samples", mru);
        }
    }

    public void Navigate(Sample sample)
    {
        Navigate("samples", sample);
    }

    public void Navigate(SearchResult result)
    {
        if (result.Tag is Scenario scenario)
        {
            Navigate("samples", scenario);
        }
        else if (result.Tag is ModelType modelType)
        {
            Navigate("models", modelType);
        }
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
        NavFrame.Navigate(typeof(SettingsPage), "ModelManagement");
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var filteredSearchResults = App.SearchIndex.Where(sr => sr.Label.Contains(sender.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            searchResults.Clear();
            foreach (var result in filteredSearchResults.OrderByDescending(i => i.Label.StartsWith(sender.Text, StringComparison.CurrentCultureIgnoreCase)).ThenBy(i => i.Label))
            {
                searchResults.Add(result);
            }
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchResult result)
        {
            Navigate(result);
        }

        SearchBox.Text = string.Empty;
    }

    private void NavFrame_Navigating(object sender, Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
    {
        if (e.SourcePageType == typeof(ScenarioSelectionPage))
        {
            NavView.SelectedItem = NavView.MenuItems[1];
        }
        else if (e.SourcePageType == typeof(ModelSelectionPage))
        {
            NavView.SelectedItem = NavView.MenuItems[2];
        }
        else if (e.SourcePageType == typeof(APISelectionPage))
        {
            NavView.SelectedItem = NavView.MenuItems[3];
        }
        else if (e.SourcePageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.FooterMenuItems[1];
        }
        else
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
    }

    private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }
}