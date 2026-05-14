// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Search.AppContentIndex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.WCRAPIs;

/// <summary>
/// View model for a single segment of the index statistics bar chart.
/// </summary>
internal sealed class BarChartSegment : INotifyPropertyChanged
{
    private double _width;
    private string _count = string.Empty;
    private string _tooltip = string.Empty;
    private double _pixelWidth;

    public string Type { get; set; } = string.Empty;

    public Brush? Background { get; set; }

    public QueryContentItemsFilterFlags? FilterFlags { get; set; }

    public string Title { get; set; } = string.Empty;

    public double Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }

    public string Count
    {
        get => _count;
        set => SetField(ref _count, value);
    }

    public string Tooltip
    {
        get => _tooltip;
        set => SetField(ref _tooltip, value);
    }

    public double PixelWidth
    {
        get => _pixelWidth;
        set => SetField(ref _pixelWidth, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

[GallerySample(
    Name = "Index Statistics",
    Model1Types = [ModelType.IndexStatistics],
    Scenario = ScenarioType.TextSemanticSearch,
    Id = "7e2f4a89-3b61-4c2d-9e8f-a5d1b3c7e2f4",
    NugetPackageReferences = [
        "Microsoft.WindowsAppSDK"
    ],
    Icon = "\uE9D9")]
internal sealed partial class AppIndexStatistics : BaseSamplePage
{
    private const int MaxContentItems = 1000;
    private const string AppSearchIndexName = "aidevgallerysearchindex";

    private AppContentIndexer? _indexer;

    private static readonly string[] BrushResourceKeys =
    [
        "SystemFillColorSuccessBrush",
        "SystemFillColorNeutralBrush",
        "SystemFillColorAttentionBrush",
        "SystemFillColorCriticalBrush",
        "SystemFillColorCautionBrush",
        "StatReindexingFillBrush",
    ];

    public ObservableCollection<BarChartSegment> BarChartSegments { get; } = new();

    private bool _suppressSelectionChanged;

    public AppIndexStatistics()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.InitializeComponent();
        this.ActualThemeChanged += OnActualThemeChanged;
        InitializeBarChartSegments();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            var indexNames = await Task.Run(DiscoverExistingIndexNames);

            _suppressSelectionChanged = true;
            IndexSelectorComboBox.ItemsSource = indexNames;

            if (indexNames.Count > 0)
            {
                IndexSelectorComboBox.SelectedIndex = 0;
            }

            _suppressSelectionChanged = false;

            if (indexNames.Count > 0)
            {
                await OpenIndexAsync(indexNames[0]);
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to discover indices.");
        }

        sampleParams.NotifyCompletion();
    }

    private static List<string> DiscoverExistingIndexNames()
    {
        return [.. AppContentIndexer.GetExistingIndexes()];
    }

    private async Task OpenIndexAsync(string indexName)
    {
        DetachIndexer();
        SetLoadingState(true);

        try
        {
            var indexer = await Task.Run(async () =>
            {
                var result = AppContentIndexer.GetOrCreateIndex(indexName);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to open index '{indexName}'. Status = '{result.Status}', Error = '{result.ExtendedError}'");
                }

                await result.Indexer.WaitForIndexCapabilitiesAsync();
                return result.Indexer;
            });

            _indexer = indexer;
            _indexer.Listener.IndexStatisticsChanged += Listener_IndexStatisticsChanged;
            _indexer.Listener.ContentItemStatusChanged += Listener_ContentItemStatusChanged;

            ReindexButton.IsEnabled = string.Equals(indexName, AppSearchIndexName, StringComparison.Ordinal);
            ContentItemsPanel.Visibility = Visibility.Collapsed;
            await LoadIndexStatisticsAsync();
        }
        catch (Exception ex)
        {
            ShowException(ex, $"Failed to open index '{indexName}'.");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        BarChartLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        BarChartItems.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        totalItemCountText.Text = isLoading ? "-" : "0";
        indexingInProgressText.Text = isLoading ? "-" : "-";
    }

    private void DetachIndexer()
    {
        if (_indexer != null)
        {
            _indexer.Listener.IndexStatisticsChanged -= Listener_IndexStatisticsChanged;
            _indexer.Listener.ContentItemStatusChanged -= Listener_ContentItemStatusChanged;
            _indexer.Dispose();
            _indexer = null;
        }
    }

    private async void IndexSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (IndexSelectorComboBox.SelectedItem is string selectedIndex)
        {
            SendSampleInteractedEvent("SelectIndex");
            await OpenIndexAsync(selectedIndex);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        DetachIndexer();
    }

    private static Brush GetThemeBrush(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    private void InitializeBarChartSegments()
    {
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "Completed",
            Background = GetThemeBrush(BrushResourceKeys[0]),
            FilterFlags = QueryContentItemsFilterFlags.Completed,
            Title = "Completed Items"
        });
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "NotStarted",
            Background = GetThemeBrush(BrushResourceKeys[1]),
            FilterFlags = QueryContentItemsFilterFlags.NotStarted,
            Title = "Not Started Items"
        });
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "InProgress",
            Background = GetThemeBrush(BrushResourceKeys[2]),
            FilterFlags = QueryContentItemsFilterFlags.InProgress,
            Title = "In Progress Items"
        });
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "Errors",
            Background = GetThemeBrush(BrushResourceKeys[3]),
            FilterFlags = QueryContentItemsFilterFlags.WithErrors,
            Title = "Error Items"
        });
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "PendingDeletion",
            Background = GetThemeBrush(BrushResourceKeys[4]),
            FilterFlags = QueryContentItemsFilterFlags.PendingDeletion,
            Title = "Pending Deletion Items"
        });
        BarChartSegments.Add(new BarChartSegment
        {
            Type = "RequiringReindexing",
            Background = GetThemeBrush(BrushResourceKeys[5]),
            FilterFlags = QueryContentItemsFilterFlags.RequiringReindexing,
            Title = "Requiring Reindexing Items"
        });
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        for (int i = 0; i < BarChartSegments.Count && i < BrushResourceKeys.Length; i++)
        {
            BarChartSegments[i].Background = GetThemeBrush(BrushResourceKeys[i]);
        }
    }

    private void Listener_IndexStatisticsChanged(AppContentIndexer sender, IndexStatistics args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateBarChart(args);
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => UpdateBarChart(args));
        }
    }

    private void Listener_ContentItemStatusChanged(AppContentIndexer sender, IReadOnlyDictionary<string, ContentItemStatusResult> args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentItemsPanel.Visibility == Visibility.Visible)
            {
                var currentTitle = ContentItemsTitle.Text;
                foreach (var segment in BarChartSegments)
                {
                    if (currentTitle.StartsWith(segment.Title, StringComparison.Ordinal))
                    {
                        _ = LoadAndDisplayContentItemsAsync(segment.FilterFlags, segment.Title);
                        break;
                    }
                }
            }
        });
    }

    private async Task LoadIndexStatisticsAsync()
    {
        if (_indexer == null)
        {
            return;
        }

        try
        {
            var indexer = _indexer;
            var indexStatistics = await Task.Run(() => indexer.GetIndexStatistics());

            DispatcherQueue.TryEnqueue(() => UpdateBarChart(indexStatistics));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppIndexStatistics - LoadIndexStatisticsAsync: {ex.Message}");
        }
    }

    private void UpdateBarChart(IndexStatistics stats)
    {
        indexingInProgressText.Text = stats.IndexingInProgress ? "Yes" : "No";
        totalItemCountText.Text = $"{stats.ItemCount}";

        var total = stats.ItemCount;

        var statsByType = new Dictionary<string, long>
        {
            ["Completed"] = stats.CompletedCount,
            ["NotStarted"] = stats.NotStartedCount,
            ["InProgress"] = stats.InProgressCount,
            ["Errors"] = stats.ErrorsCount,
            ["PendingDeletion"] = stats.PendingDeletionCount,
            ["RequiringReindexing"] = stats.RequiringReindexingCount
        };

        foreach (var segment in BarChartSegments)
        {
            if (statsByType.TryGetValue(segment.Type, out var count))
            {
                segment.Width = total > 0 ? count : 0;
                segment.Count = count > 0 ? count.ToString() : string.Empty;
                segment.Tooltip = $"{segment.Title.Replace(" Items", string.Empty)}: {count}";
            }
        }

        RecalculatePixelWidths();
    }

    private void RecalculatePixelWidths()
    {
        var containerWidth = BarChartBorder.ActualWidth;
        if (containerWidth <= 0)
        {
            return;
        }

        var totalWeight = 0.0;
        foreach (var s in BarChartSegments)
        {
            totalWeight += s.Width;
        }

        if (totalWeight <= 0)
        {
            foreach (var s in BarChartSegments)
            {
                s.PixelWidth = 0;
            }

            return;
        }

        var remaining = containerWidth;
        for (int i = 0; i < BarChartSegments.Count - 1; i++)
        {
            var pw = Math.Floor(BarChartSegments[i].Width / totalWeight * containerWidth);
            BarChartSegments[i].PixelWidth = pw;
            remaining -= pw;
        }

        BarChartSegments[^1].PixelWidth = Math.Max(0, remaining);
    }

    private void BarChartBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RecalculatePixelWidths();
    }

    private void BarChartButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is BarChartSegment segment)
        {
            _ = LoadAndDisplayContentItemsAsync(segment.FilterFlags, segment.Title);
        }
    }

    private async Task LoadAndDisplayContentItemsAsync(QueryContentItemsFilterFlags? filterFlags, string titlePrefix)
    {
        if (_indexer == null)
        {
            return;
        }

        try
        {
            var indexer = _indexer;
            var (items, wasCapped) = await Task.Run(() =>
            {
                var result = new List<string>();
                bool capped = false;
                ContentItemReader itemsCursor = filterFlags.HasValue
                    ? indexer.GetContentItems(filterFlags.Value)
                    : indexer.GetContentItems();

                IReadOnlyList<string> itemsBatch = itemsCursor.GetNextItems(100);
                while (itemsBatch.Count > 0)
                {
                    foreach (string contentId in itemsBatch)
                    {
                        result.Add(contentId);
                    }

                    if (result.Count >= MaxContentItems)
                    {
                        capped = true;
                        break;
                    }

                    itemsBatch = itemsCursor.GetNextItems(100);
                }

                return (result, capped);
            });

            var suffix = wasCapped ? $" (showing first {MaxContentItems})" : string.Empty;
            ContentItemsTitle.Text = $"{titlePrefix} ({items.Count}{suffix})";
            ContentItemsView.ItemsSource = items;
            ContentItemsPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppIndexStatistics - LoadAndDisplayContentItemsAsync: {ex.Message}");
        }
    }

    private void CloseContentItems_Click(object sender, RoutedEventArgs e)
    {
        ContentItemsPanel.Visibility = Visibility.Collapsed;
    }

    private void ReindexButton_Click(object sender, RoutedEventArgs e)
    {
        SendSampleInteractedEvent("ReindexAppContent");
        MainWindow.IndexAppSearchIndexStatic();
    }

    private async void ContentItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (_indexer == null || args.InvokedItem is not string contentId || string.IsNullOrEmpty(contentId))
        {
            return;
        }

        SendSampleInteractedEvent("ViewContentItemStatus");

        try
        {
            var indexer = _indexer;
            var statusResult = await Task.Run(() => indexer.GetContentItemStatus(contentId));

            ContentItemStatusTextBox.Text =
                $"Content ID: {contentId}\n\n"
                + $"=== Status ===\n"
                + $"Status: {statusResult.Status}\n\n"
                + $"=== Error Information ===\n"
                + $"Extended Error: {statusResult.ExtendedError?.Message ?? "None"}\n"
                + $"HRESULT: 0x{(statusResult.ExtendedError != null ? statusResult.ExtendedError.HResult.ToString("X8") : "00000000")}\n\n"
                + $"Error Detail: {statusResult.ErrorDetail}\n\n\n"
                + $"=== Reindexing Status ===\n"
                + $"Status: {statusResult.ReindexingStatus}\n\n";
        }
        catch (Exception ex)
        {
            ContentItemStatusTextBox.Text = $"Content ID: {contentId}\n\nError: {ex.Message}";
        }
    }
}
