// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Converters;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;

internal sealed partial class AppSearchBox : UserControl
{
    private AppContentIndexer? _indexer;
    private CancellationTokenSource? _searchCts;

    public AppSearchBox()
    {
        this.InitializeComponent();
        this.Loaded += AppSearchBox_Loaded;

        // Set the template selector for the AutoSuggestBox
        var scenarioTemplate = (DataTemplate)Resources["ScenarioTemplate"];
        var modelTemplate = (DataTemplate)Resources["ModelTemplate"];
        var templateSelector = new SearchResultTemplateSelector
        {
            ScenarioTemplate = scenarioTemplate,
            ModelTemplate = modelTemplate
        };
        SearchBox.ItemTemplateSelector = templateSelector;
    }

    private async void AppSearchBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsIndexingEnabled)
        {
            await LoadAppSearchIndexAsync();
        }
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(AppSearchBox),
            new PropertyMetadata("Search..."));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty IsIndexingEnabledProperty =
        DependencyProperty.Register(
            nameof(IsIndexingEnabled),
            typeof(bool),
            typeof(AppSearchBox),
            new PropertyMetadata(false, OnIsIndexingEnabledChanged));

    public bool IsIndexingEnabled
    {
        get => (bool)GetValue(IsIndexingEnabledProperty);
        set => SetValue(IsIndexingEnabledProperty, value);
    }

    private static async void OnIsIndexingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AppSearchBox)d;
        if ((bool)e.NewValue)
        {
            await control.LoadAppSearchIndexAsync();
        }
        else
        {
            control.SetSearchBoxACSDisabled();
        }
    }

    public static readonly DependencyProperty SearchIndexProperty =
        DependencyProperty.Register(
            nameof(SearchIndex),
            typeof(IReadOnlyList<SearchResult>),
            typeof(AppSearchBox),
            new PropertyMetadata(null));

    public IReadOnlyList<SearchResult>? SearchIndex
    {
        get => (IReadOnlyList<SearchResult>?)GetValue(SearchIndexProperty);
        set => SetValue(SearchIndexProperty, value);
    }

    public static readonly DependencyProperty IsIndexCompletedProperty =
        DependencyProperty.Register(
            nameof(IsIndexCompleted),
            typeof(bool),
            typeof(AppSearchBox),
            new PropertyMetadata(false));

    public bool IsIndexCompleted
    {
        get => (bool)GetValue(IsIndexCompletedProperty);
        set => SetValue(IsIndexCompletedProperty, value);
    }

    public event EventHandler<SearchResult>? SearchResultSelected;

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
                if (_indexer != null && IsIndexingEnabled)
                {
                    // Use AppContentIndexer to search
                    var query = _indexer.CreateTextQuery(searchText);
                    IReadOnlyList<TextQueryMatch>? matches = await Task.Run(() => query.GetNextMatches(5), token);

                    if (!token.IsCancellationRequested && SearchIndex != null)
                    {
                        foreach (var match in matches)
                        {
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            var sr = SearchIndex.FirstOrDefault(s => s.Label == match.ContentId);
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
                    if (SearchIndex != null)
                    {
                        var filteredSearchResults = SearchIndex.Where(sr => sr.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
                        orderedResults = filteredSearchResults
                            .OrderByDescending(i => i.Label.StartsWith(searchText, StringComparison.CurrentCultureIgnoreCase))
                            .ThenBy(i => i.Label)
                            .ToList();
                    }
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
            SearchResultSelected?.Invoke(this, result);
        }

        SearchBox.Text = string.Empty;
    }

    private async Task LoadAppSearchIndexAsync()
    {
        var result = AppContentIndexer.GetOrCreateIndex("AIDevGallerySearchIndex");

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
        }

        _indexer = result.Indexer;
        await _indexer.WaitForIndexCapabilitiesAsync();

        // If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
        if (result.Status == GetOrCreateIndexStatus.CreatedNew || !IsIndexCompleted)
        {
            Debug.WriteLine("Created a new index");
            await IndexContentsWithAppContentSearchAsync();
        }
        else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
        {
            Debug.WriteLine("Opened an existing index");
            SetSearchBoxIndexingCompleted();
        }
    }

    public async Task IndexContentsWithAppContentSearchAsync()
    {
        if (_indexer == null || SearchIndex == null)
        {
            SetSearchBoxACSDisabled();
            return;
        }

        await Task.Run(() =>
        {
            foreach (var item in SearchIndex)
            {
                string id = item.Label;
                string value = $"{item.Label}\n{item.Description}";
                IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, value);
                _indexer.AddOrUpdate(textContent);
            }
        });

        await _indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
        SetSearchBoxIndexingCompleted();

        IsIndexCompleted = true;
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
}