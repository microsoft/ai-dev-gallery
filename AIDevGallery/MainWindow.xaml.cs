// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Controls.ModelPickerViews;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Pages;
using AIDevGallery.Utils;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.System;
using Windows.UI.ViewManagement;
using WinUIEx;

namespace AIDevGallery;

internal sealed partial class MainWindow : WindowEx
{
    public ModelOrApiPicker ModelPicker => modelOrApiPicker;
    private UISettings uiSettings;

    // Properties to expose App static members for binding
    public AppData AppSettings => App.AppData;
    public IReadOnlyList<SearchResult> SearchIndex => App.SearchIndex;

    public MainWindow(object? obj = null)
    {
        this.InitializeComponent();
        SetTitleBar();
        App.ModelDownloadQueue.ModelsChanged += DownloadQueue_ModelsChanged;

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

        uiSettings = new UISettings();
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        uiSettings.ColorValuesChanged += Accessibility_HighContrastChanged;
        UpdateResources();
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
        else if (obj is ModelDetails modelDetails)
        {
            // Try to find the ModelType from the ModelDetails.Id
            var modelTypeList = App.FindSampleItemById(modelDetails.Id);
            if (modelTypeList.Count > 0)
            {
                NavigateToApiOrModelPage(modelTypeList[0]);
            }
            else
            {
                // Fallback to Models page if we can't determine the type
                Navigate("Models", obj);
            }
        }
        else if (obj is SampleNavigationArgs)
        {
            Navigate("Samples", obj);
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
            ModelPicker.Hide();

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
            // Try to find the ModelType from the ItemId to determine if it's an API
            var modelTypeList = App.FindSampleItemById(mru.ItemId);
            if (modelTypeList.Count > 0)
            {
                NavigateToApiOrModelPage(modelTypeList[0]);
            }
            else
            {
                // Fallback to models page if we can't determine the type
                Navigate("models", mru);
            }
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
            NavigateToApiOrModelPage(modelType);
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

    private void AppSearchBox_SearchResultSelected(object sender, SearchResult result)
    {
        Navigate(result);
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
            ModelPicker.Hide();
            NavFrame.GoBack();
        }
    }

    private void NavFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // Workaround for using the LeftHeader instead of Icon
        if (titleBar.IsBackButtonVisible)
        {
            // Check if the back button is shown, update the margin of the icon.
            titleBarIcon.Margin = new Thickness(0, 0, 8, 0);
        }
        else
        {
            titleBarIcon.Margin = new Thickness(16, 0, 0, 0);
        }
    }

    public static async void IndexAppSearchIndexStatic()
    {
        var mainWindow = (MainWindow)App.MainWindow;
        if (mainWindow?.AppSearchBox != null)
        {
            await mainWindow.AppSearchBox.IndexContentsWithAppContentSearchAsync();
            App.AppData.IsAppContentIndexCompleted = true;
            await App.AppData.SaveAsync();
        }
    }

    private void Accessibility_HighContrastChanged(object sender, object e)
    {
        UpdateResources();
    }

    private void UpdateResources()
    {
        var dispatcherQueue = this.DispatcherQueue;
        dispatcherQueue.TryEnqueue(() =>
        {
            var appResources = Application.Current.Resources;

            if (appResources["GitHubIconImage"] is Microsoft.UI.Xaml.Media.Imaging.SvgImageSource svg)
            {
                svg.UriSource = new Uri($"ms-appx:///Assets/ModelIcons/GitHub{AppUtils.GetThemeAssetSuffix()}.svg");
            }
            else
            {
                appResources["GitHubIconImage"] =
                    new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(
                        new Uri($"ms-appx:///Assets/ModelIcons/GitHub{AppUtils.GetThemeAssetSuffix()}.svg"));
            }

            ModelPickerDefinition.Definitions["onnx"].Icon = $"ms-appx:///Assets/ModelIcons/CustomModel{AppUtils.GetThemeAssetSuffix()}.png";
            ModelPickerDefinition.Definitions["ollama"].Icon = $"ms-appx:///Assets/ModelIcons/Ollama{AppUtils.GetThemeAssetSuffix()}.png";
            ModelPickerDefinition.Definitions["openai"].Icon = $"ms-appx:///Assets/ModelIcons/OpenAI{AppUtils.GetThemeAssetSuffix()}.png";
        });
    }
}