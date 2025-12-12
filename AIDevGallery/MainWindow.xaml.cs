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
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.ViewManagement;
using WinUIEx;

namespace AIDevGallery;

internal sealed partial class MainWindow : WindowEx
{
    private AppContentIndexer? _indexer;
    private CancellationTokenSource? _searchCts; // Added for search cancellation
    public ModelOrApiPicker ModelPicker => modelOrApiPicker;
    private UISettings uiSettings;

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

        if (App.AppData.IsAppContentSearchEnabled)
        {
            Task.Run(async () =>
            {
                // Load AppContentSearch
                await LoadAppSearchIndex();
            });
        }

        App.AppData.PropertyChanged += AppData_PropertyChanged;
        uiSettings = new UISettings();
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        uiSettings.ColorValuesChanged += Accessibility_HighContrastChanged;
        UpdateResources();
    }

    private async Task LoadAppSearchIndex()
    {
        var result = AppContentIndexer.GetOrCreateIndex("AIDevGallerySearchIndex");

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
        }

        _indexer = result.Indexer;
        await _indexer.WaitForIndexCapabilitiesAsync();

        // If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
        if (result.Status == GetOrCreateIndexStatus.CreatedNew || !App.AppData.IsAppContentIndexCompleted)
        {
            Debug.WriteLine("Created a new index");
            IndexContentsWithAppContentSearch();
        }
        else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
        {
            Debug.WriteLine("Opened an existing index");
            SetSearchBoxIndexingCompleted();
        }
    }

    private void AppData_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppData.IsAppContentSearchEnabled))
        {
            if (App.AppData.IsAppContentSearchEnabled)
            {
                Task.Run(async () =>
                {
                    // Load AppContentSearch
                    await LoadAppSearchIndex();
                });
            }
            else
            {
                SetSearchBoxACSDisabled();
            }
        }
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

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Cancel previous search if running
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            var searchText = sender.Text;
            List<SearchResult> orderedResults = new();

            try
            {
                if (_indexer != null && App.AppData.IsAppContentSearchEnabled)
                {
                    // Use AppContentIndexer to search
                    var query = _indexer.CreateTextQuery(searchText);
                    IReadOnlyList<TextQueryMatch>? matches = await Task.Run(() => query.GetNextMatches(5), token);

                    if (!token.IsCancellationRequested)
                    {
                        foreach (var match in matches)
                        {
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            var sr = App.SearchIndex.FirstOrDefault(s => s.Label == match.ContentId);
                            if (sr != null)
                            {
                                orderedResults.Add(sr);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to in-memory search
                    var filteredSearchResults = App.SearchIndex.Where(sr => sr.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                    orderedResults = filteredSearchResults
                        .OrderByDescending(i => i.Label.StartsWith(searchText, StringComparison.CurrentCultureIgnoreCase))
                        .ThenBy(i => i.Label)
                        .ToList();
                }

                if (!token.IsCancellationRequested)
                {
                    SearchBox.ItemsSource = orderedResults;
                    var resultCount = orderedResults.Count;
                    string announcement = $"Searching for '{searchText}', {resultCount} search result{(resultCount == 1 ? string.Empty : "s")} found";
                    NarratorHelper.Announce(SearchBox, announcement, "searchSuggestionsActivityId");
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, do nothing
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

    private void SetSearchBoxIndexingCompleted()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SearchBoxQueryIcon.Foreground = Application.Current.Resources["AIAccentGradientBrush"] as Brush;
            SearchBoxQueryIcon.Glyph = "\uED37";
        });
    }

    private void SetSearchBoxACSDisabled()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SearchBoxQueryIcon.Foreground = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
            SearchBoxQueryIcon.Glyph = "\uE721";
        });
    }

    private async void IndexContentsWithAppContentSearch()
    {
        if (_indexer == null || App.SearchIndex == null)
        {
            SetSearchBoxACSDisabled();
            return;
        }

        await Task.Run(() =>
        {
            foreach (var item in App.SearchIndex)
            {
                string id = item.Label;
                string value = $"{item.Label}\n{item.Description}";
                IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, value);
                _indexer.AddOrUpdate(textContent);
            }
        });

        await _indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
        SetSearchBoxIndexingCompleted();

        // Adding a check here since if the user closes in the middle of the indexing loop, we will never fully finish indexing.
        // The next app launch will open the existing index and consider everything done.
        App.AppData.IsAppContentIndexCompleted = true;
        await App.AppData.SaveAsync();
    }

    public static void IndexAppSearchIndexStatic()
    {
        var mainWindow = (MainWindow)App.MainWindow;
        mainWindow?.IndexContentsWithAppContentSearch();
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