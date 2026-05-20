// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Search.AppContentIndex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Semantic Search",
    Model1Types = [ModelType.SemanticSearch],
    Scenario = ScenarioType.TextSemanticSearch,
    Id = "f8465a45-8e23-4485-8c16-9909e96eacf6",
    SharedCode = [
        SharedCodeEnum.DataItems
    ],
    AssetFilenames = [
        "InteriorDesign.png",
        "TofuBowlRecipe.png",
        "ShakshukaRecipe.png"
    ],
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]

internal sealed partial class SemanticSearch : BaseSamplePage
{
    private const double ImageResultDisplayWidth = 150;

    private ObservableCollection<TextDataItem> TextDataItems { get; } = new();
    private ObservableCollection<ImageDataItem> ImageDataItems { get; } = new();

    // This is some text data that we want to add to the index:
    private readonly Dictionary<string, string> simpleTextData = new Dictionary<string, string>
    {
        { "item1", "Preparing a hearty vegetable stew begins with chopping fresh carrots, onions, and celery. Sauté them in olive oil until fragrant, then add diced tomatoes, herbs, and vegetable broth. Simmer gently for an hour, allowing flavors to meld into a comforting dish perfect for cold evenings." },
        { "item2", "Modern exhibition design combines narrative flow with spatial strategy. Lighting emphasizes focal objects while circulation paths avoid bottlenecks. Materials complement artifacts without visual competition. Interactive elements invite engagement but remain intuitive. Environmental controls protect sensitive works. Success balances scholarship, aesthetics, and visitor experience through thoughtful, cohesive design choices." },
        { "item3", "Domestic cats communicate through posture, tail flicks, and vocalizations. Play mimics hunting behaviors like stalking and pouncing, supporting agility and mental stimulation. Scratching maintains claws and marks territory, so provide sturdy posts. Balanced diets, hydration, and routine veterinary care sustain health. Safe retreats and vertical spaces reduce stress and encourage exploration." },
        { "item4", "Snowboarding across pristine slopes combines agility, balance, and speed. Riders carve smooth turns on powder, adjust stance for control, and master jumps in terrain parks. Essential gear includes boots, bindings, and helmets for safety. Embrace crisp alpine air while perfecting tricks and enjoying the thrill of winter adventure." },
        { "item5", "Urban beekeeping thrives with diverse forage across seasons. Rooftop hives benefit from trees, herbs, and staggered blooms. Provide shallow water sources and shade to counter heat stress. Prevent swarms through timely inspections and splits. Monitor mites with sugar rolls and rotate treatments. Honey reflects city terroir with surprising floral complexity." }
    };

    private readonly Dictionary<string, string> simpleImageData = new Dictionary<string, string>
    {
        { "image1", "ms-appx:///Assets/InteriorDesign.png" },
        { "image2", "ms-appx:///Assets/TofuBowlRecipe.png" },
        { "image3", "ms-appx:///Assets/ShakshukaRecipe.png" },
    };

    private readonly Dictionary<string, ImageDimensions> _imageDimensions = new();
    private AppContentIndexer? _indexer;
    private AppIndexTextQuerySession? _suggestionTextQuerySession;
    private AppIndexImageQuerySession? _suggestionImageQuerySession;
    private CancellationTokenSource? _querySubmittedCts = new();
    private string? _suggestionTextQueryOptionsSignature;
    private string? _suggestionImageQueryOptionsSignature;
    private string? _currentSuggestionQueryText;
    private List<SearchSuggestionItem> _lastTextSuggestionItems = new();
    private List<SearchSuggestionItem> _lastImageSuggestionItems = new();

    public SemanticSearch()
    {
        this.InitializeComponent();
        this.Unloaded += (s, e) =>
        {
            CleanUp();
        };
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>

        PopulateTextData();
        PopulateImageData();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await Task.Run(async () =>
        {
            var result = AppContentIndexer.GetOrCreateIndex("semanticSearchIndex");

            if (!result.Succeeded)
            {
                ShowException(null, $"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
                return;
            }

            // If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
            if (result.Status == GetOrCreateIndexStatus.CreatedNew)
            {
                Debug.WriteLine("Created a new index");
            }
            else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
            {
                Debug.WriteLine("Opened an existing index");
            }

            _indexer = result.Indexer;
            await _indexer.WaitForIndexCapabilitiesAsync();

            _indexer.Listener.IndexCapabilitiesChanged += Listener_IndexCapabilitiesChanged;
            LoadAppIndexCapabilities();

            sampleParams.NotifyCompletion();
        });

        IndexAll();
    }

    // <exclude>
    private void Page_Loaded()
    {
        textDataItemsView.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        StopSuggestionQuerySession();
        CancelQuerySubmitted();

        if (_indexer != null)
        {
            _indexer.Listener.IndexCapabilitiesChanged -= Listener_IndexCapabilitiesChanged;
            _indexer.RemoveAllContentItems();
            _indexer.Dispose();
            _indexer = null;
        }
    }

    // Update and index local test text data on TextBox text changed
    private async void SemanticTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            string? id = textBox.Tag as string;
            string value = textBox.Text;

            if (id != null)
            {
                // Update local dictionary and observable collection
                var item = TextDataItems.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    item.Value = value;
                }

                if (simpleTextData.ContainsKey(id))
                {
                    simpleTextData[id] = value;
                }

                // Index text data
                IndexingMessage.IsOpen = true;
                await Task.Run(async () =>
                {
                    IndexTextData(id, value);
                    var indexer = _indexer;
                    if (indexer != null)
                    {
                        await indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
                    }
                });
            }

            IndexingMessage.IsOpen = false;
        }
    }

    // Update and index local test image data on image opened
    private async void ImageData_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Image image)
        {
            string? id = image.Tag as string;
            string uriString = string.Empty;
            string fileName = string.Empty;

            if (image.Source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
            {
                uriString = bitmapImage.UriSource.ToString();
            }

            SoftwareBitmap? bitmap = null;
            if (!string.IsNullOrEmpty(uriString))
            {
                bitmap = await LoadBitmap(uriString);
            }

            if (id != null && bitmap != null)
            {
                // Update local dictionary and observable collection
                var item = ImageDataItems.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    item.ImageSource = uriString;
                }

                string imageVal = uriString.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase) ? fileName : uriString;

                if (!simpleImageData.TryAdd(id, uriString))
                {
                    simpleImageData[id] = uriString;
                }

                IndexingMessage.IsOpen = true;
                await Task.Run(async () =>
                {
                    IndexImageData(id, bitmap);
                    var indexer = _indexer;
                    if (indexer != null)
                    {
                        await indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
                    }
                });
            }

            IndexingMessage.IsOpen = false;
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var indexer = _indexer;
        if (indexer == null)
        {
            SetSearchSuggestions([]);
            return;
        }

        string searchText = sender.Text;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _currentSuggestionQueryText = null;
            _lastTextSuggestionItems.Clear();
            _lastImageSuggestionItems.Clear();
            SetSearchSuggestions([]);
            StopSuggestionQuerySession();
            return;
        }

        try
        {
            _currentSuggestionQueryText = searchText;
            _lastTextSuggestionItems.Clear();
            _lastImageSuggestionItems.Clear();

            TextQueryOptions textQueryOptions = CreateTextQueryOptions();
            ImageQueryOptions imageQueryOptions = CreateImageQueryOptions();

            AppIndexTextQuerySession textQuerySession = GetOrCreateSuggestionTextQuerySession(indexer, textQueryOptions);
            AppIndexImageQuerySession imageQuerySession = GetOrCreateSuggestionImageQuerySession(indexer, imageQueryOptions);

            SetSearchSuggestions([SearchSuggestionItem.Searching(searchText)]);

            textQuerySession.UpdateQueryPhrase(searchText);
            imageQuerySession.UpdateQueryPhrase(searchText);
        }
        catch (Exception ex)
        {
            SetSearchSuggestions([SearchSuggestionItem.Error(searchText)]);
            ResultStatusTextBlock.Text = "Unable to update search suggestions.";
            ShowException(ex, "Failed to update search suggestions.");
        }
    }

    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string searchText = SearchBox.Text;
        if (args.ChosenSuggestion is SearchSuggestionItem chosenSuggestion && !chosenSuggestion.IsPlaceholder)
        {
            searchText = chosenSuggestion.QueryText;
            SearchBox.Text = searchText;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            Debug.WriteLine("Search text is empty.");
            return;
        }

        if (_indexer == null)
        {
            ResultStatusTextBlock.Text = "Indexer is unavailable.";
            return;
        }

        AppContentIndexer indexer = _indexer;
        ResultsGrid.Visibility = Visibility.Visible;
        ResultStatusTextBlock.Text = "Searching...";
        ResultsTextBlock.Text = string.Empty;
        ResultsTextBlock.Visibility = Visibility.Collapsed;
        ImageResultsBox.ItemsSource = null;
        ImageResultsBox.Visibility = Visibility.Collapsed;

        // Snapshot query options on the UI thread (they read from ComboBoxes/TextBoxes).
        TextQueryOptions textQueryOptions = CreateTextQueryOptions();
        ImageQueryOptions imageQueryOptions = CreateImageQueryOptions();
        CancellationToken ct = CancelGenerationAndGetNewToken();

        // Do NOT stop the suggestion sessions here. Disposing the suggestion sessions
        // immediately before invoking the one-shot CreateTextQuery / CreateImageQuery on the
        // same indexer was observed to leave the indexer in a state that caused the one-shot
        // queries to throw ("Failed to search indexed content."). The suggestion sessions are
        // designed to run independently; they will be recycled the next time the user types
        // or when the page is unloaded via CleanUp().
        try
        {
            var matches = await Task.Run(
                () =>
                {
                    ct.ThrowIfCancellationRequested();

                    AppIndexTextQuery textQuery = indexer.CreateTextQuery(searchText, textQueryOptions);

                    // Materialize the IVectorView into a plain List on the worker thread so we
                    // never enumerate the COM-projected collection from a different apartment.
                    List<TextQueryMatch> textMatches = textQuery.GetNextMatches(5).ToList();

                    ct.ThrowIfCancellationRequested();

                    AppIndexImageQuery imageQuery = indexer.CreateImageQuery(searchText, imageQueryOptions);
                    List<ImageQueryMatch> imageMatches = imageQuery.GetNextMatches(5).ToList();

                    return (TextMatches: (IReadOnlyList<TextQueryMatch>)textMatches, ImageMatches: (IReadOnlyList<ImageQueryMatch>)imageMatches);
                },
                ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            List<string> textResults = GetTextMatchDisplayItems(matches.TextMatches);
            List<SearchImageResult> imageResults = GetImageMatchDisplayItems(matches.ImageMatches, matches.TextMatches);

            if (textResults.Count == 0 && imageResults.Count == 0)
            {
                ResultStatusTextBlock.Text = "No results found.";
            }
            else
            {
                ResultStatusTextBlock.Text = "Search Results:";
            }

            if (textResults.Count > 0)
            {
                ResultsTextBlock.Visibility = Visibility.Visible;
                ResultsTextBlock.Text = string.Join("\n\n", textResults);
            }
            else
            {
                ResultsTextBlock.Visibility = Visibility.Collapsed;
            }

            if (imageResults.Count > 0)
            {
                ImageResultsBox.ItemsSource = imageResults;
                ImageResultsBox.Visibility = Visibility.Visible;
            }
            else
            {
                ImageResultsBox.ItemsSource = null;
                ImageResultsBox.Visibility = Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // Surface the actual exception message so the failure is diagnosable in the UI,
            // not just a generic "Failed to search indexed content." string.
            ResultStatusTextBlock.Text = $"Search failed: {ex.Message}";
            ResultsTextBlock.Visibility = Visibility.Collapsed;
            ImageResultsBox.ItemsSource = null;
            ImageResultsBox.Visibility = Visibility.Collapsed;
            ShowException(ex);
        }
    }

    private async void AddTextDataButton_Click(object sender, RoutedEventArgs e)
    {
        // Find the lowest unused id in the form itemN
        int nextIndex = 1;
        string newId;
        var existingIds = new HashSet<string>(simpleTextData.Keys.Concat(TextDataItems.Select(x => x.Id ?? string.Empty)).Where(id => !string.IsNullOrEmpty(id)));
        do
        {
            newId = $"item{nextIndex}";
            nextIndex++;
        }
        while (existingIds.Contains(newId));

        string defaultValue = "New item text...";

        // Add to dictionary
        simpleTextData[newId] = defaultValue;

        // Add to observable collection
        var newItem = new TextDataItem { Id = newId, Value = defaultValue };
        TextDataItems.Add(newItem);

        IndexingMessage.IsOpen = true;
        await Task.Run(() =>
        {
            IndexTextData(newId, defaultValue);
        });
        IndexingMessage.IsOpen = false;

        textDataItemsView.StartBringItemIntoView(TextDataItems.Count - 1, new BringIntoViewOptions());
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            if (button.Tag is TextDataItem textItem)
            {
                TextDataItems.Remove(textItem);

                if (!string.IsNullOrEmpty(textItem.Id))
                {
                    if (simpleTextData.ContainsKey(textItem.Id))
                    {
                        simpleTextData.Remove(textItem.Id);
                    }

                    RemovedItemMessage.IsOpen = true;
                    RemovedItemMessage.Message = $"Removed {textItem.Id} from index";
                    await Task.Run(() =>
                    {
                        RemoveItemFromIndex(textItem.Id);
                    });
                }

                RemovedItemMessage.IsOpen = false;
            }
            else if (button.Tag is ImageDataItem imageItem)
            {
                ImageDataItems.Remove(imageItem);

                if (!string.IsNullOrEmpty(imageItem.Id))
                {
                    if (simpleImageData.ContainsKey(imageItem.Id))
                    {
                        simpleImageData.Remove(imageItem.Id);
                    }

                    RemovedItemMessage.IsOpen = true;
                    RemovedItemMessage.Message = $"Removed {imageItem.Id} from index";
                    await Task.Run(() =>
                    {
                        RemoveItemFromIndex(imageItem.Id);
                    });
                }

                RemovedItemMessage.IsOpen = false;
            }
        }
    }

    private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
    {
        SendSampleInteractedEvent("LoadImageClicked");
        var window = new Window();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        var picker = new FileOpenPicker();

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".jpg");

        picker.ViewMode = PickerViewMode.Thumbnail;

        StorageFile file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            // Generate a unique id for the new image
            int nextIndex = 1;
            string newId;
            do
            {
                newId = $"image{ImageDataItems.Count + nextIndex}";
                nextIndex++;
            }
            while (ImageDataItems.Any(i => i.Id == newId));

            // Create a ms-appx URI for the image (or use file path for local images)
            var imageUri = file.Path;

            // Add to collection and dictionary
            ImageDataItems.Add(new ImageDataItem { Id = newId, ImageSource = imageUri });
            simpleImageData[newId] = imageUri;

            ImageDataItemsView.StartBringItemIntoView(ImageDataItems.Count - 1, new BringIntoViewOptions());
        }
    }

    private void RemoveItemFromIndex(string id)
    {
        if (_indexer == null)
        {
            return;
        }

        // Remove item from index
        _indexer.RemoveContentItem(id);
    }

    private void IndexTextData(string id, string value)
    {
        if (_indexer == null)
        {
            return;
        }

        // Index Textbox content
        IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, value);
        _indexer.AddOrUpdate(textContent);
    }

    private void IndexImageData(string id, SoftwareBitmap bitmap)
    {
        if (_indexer == null)
        {
            return;
        }

        _imageDimensions[id] = new ImageDimensions(bitmap.PixelWidth, bitmap.PixelHeight);

        // Index image content
        IndexableAppContent imageContent = AppManagedIndexableAppContent.CreateFromBitmap(id, bitmap);
        _indexer.AddOrUpdate(imageContent);
    }

    private async void IndexAll()
    {
        IndexingMessage.IsOpen = true;

        var textDataSnapshot = simpleTextData.ToList();
        var imageDataSnapshot = simpleImageData.ToList();

        await Task.Run(async () =>
        {
            foreach (var kvp in textDataSnapshot)
            {
                IndexTextData(kvp.Key, kvp.Value);
            }

            foreach (var kvp in imageDataSnapshot)
            {
                SoftwareBitmap? bitmap = await LoadBitmap(kvp.Value);
                if (bitmap != null)
                {
                    IndexImageData(kvp.Key, bitmap);
                }
            }

            var indexer = _indexer;
            if (indexer != null)
            {
                await indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
            }
        });

        IndexingMessage.IsOpen = false;
    }

    private void IndexAllButton_Click(object sender, RoutedEventArgs e)
    {
        IndexAll();
    }

    private async void LoadAppIndexCapabilities()
    {
        if (_indexer == null)
        {
            return;
        }

        IndexCapabilities capabilities = await Task.Run(() =>
        {
            return _indexer.GetIndexCapabilities();
        });

        DispatcherQueue.TryEnqueue(() =>
        {
            bool textLexicalAvailable =
                capabilities.GetCapabilityState(IndexCapability.TextLexical).InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
            bool textSemanticAvailable =
                capabilities.GetCapabilityState(IndexCapability.TextSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
            bool imageSemanticAvailable =
                capabilities.GetCapabilityState(IndexCapability.ImageSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
            bool imageOcrAvailable =
                capabilities.GetCapabilityState(IndexCapability.ImageOcr).InitializationStatus == IndexCapabilityInitializationStatus.Initialized;

            // Disable text sample if both text capabilities are unavailable
            textDataItemsView.IsEnabled = textLexicalAvailable || textSemanticAvailable;
            uploadTextButton.IsEnabled = textLexicalAvailable || textSemanticAvailable;

            // Disable image sample if both image capabilities are unavailable
            ImageDataItemsView.IsEnabled = imageSemanticAvailable || imageOcrAvailable;
            uploadImageButton.IsEnabled = imageSemanticAvailable || imageOcrAvailable;

            var unavailable = new List<string>();
            if (!textLexicalAvailable)
            {
                unavailable.Add("TextLexical");
            }

            if (!textSemanticAvailable)
            {
                unavailable.Add("TextSemantic");
            }

            if (!imageSemanticAvailable)
            {
                unavailable.Add("ImageSemantic");
            }

            if (!imageOcrAvailable)
            {
                unavailable.Add("ImageOcr");
            }

            if (unavailable.Count > 0)
            {
                IndexCapabilitiesMessage.Message = $"Unavailable: {string.Join(", ", unavailable)}";
                IndexCapabilitiesMessage.IsOpen = true;
                AllIndexAvailableTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                // All capabilities are available
                IndexCapabilitiesMessage.IsOpen = false;
                AllIndexAvailableTextBlock.Visibility = Visibility.Visible;
            }
        });
    }

    private void Listener_IndexCapabilitiesChanged(AppContentIndexer indexer, IndexCapabilities statusResult)
    {
        LoadAppIndexCapabilities();
    }

    private async Task<SoftwareBitmap?> LoadBitmap(string uriString)
    {
        try
        {
            StorageFile file;
            if (uriString.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase))
            {
                file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uriString));
            }
            else
            {
                // Assume it's a file path for user-uploaded images
                file = await StorageFile.GetFileFromPathAsync(uriString);
            }

            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);

            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
        }

        return null;
    }

    private void PopulateTextData()
    {
        foreach (var kvp in simpleTextData)
        {
            TextDataItems.Add(new TextDataItem { Id = kvp.Key, Value = kvp.Value });
        }
    }

    private void PopulateImageData()
    {
        foreach (var kvp in simpleImageData)
        {
            ImageDataItems.Add(new ImageDataItem { Id = kvp.Key, ImageSource = kvp.Value });
        }
    }

    private AppIndexTextQuerySession GetOrCreateSuggestionTextQuerySession(AppContentIndexer indexer, TextQueryOptions textQueryOptions)
    {
        string optionsSignature = GetTextQueryOptionsSignature();
        if (_suggestionTextQuerySession == null || _suggestionTextQueryOptionsSignature != optionsSignature)
        {
            StopSuggestionTextQuerySession();

            _suggestionTextQuerySession = indexer.CreateTextQuerySession();
            _suggestionTextQuerySession.DesiredMatchesPerResult = Math.Min(5, AppIndexTextQuerySession.MaxMatchesPerResult);
            _suggestionTextQuerySession.ResultChanged += SuggestionTextQuerySession_ResultChanged;
            _suggestionTextQuerySession.Start(textQueryOptions);
            _suggestionTextQueryOptionsSignature = optionsSignature;
        }

        return _suggestionTextQuerySession;
    }

    private AppIndexImageQuerySession GetOrCreateSuggestionImageQuerySession(AppContentIndexer indexer, ImageQueryOptions imageQueryOptions)
    {
        string optionsSignature = GetImageQueryOptionsSignature();
        if (_suggestionImageQuerySession == null || _suggestionImageQueryOptionsSignature != optionsSignature)
        {
            StopSuggestionImageQuerySession();

            _suggestionImageQuerySession = indexer.CreateImageQuerySession();
            _suggestionImageQuerySession.DesiredMatchesPerResult = Math.Min(5, AppIndexImageQuerySession.MaxMatchesPerResult);
            _suggestionImageQuerySession.ResultChanged += SuggestionImageQuerySession_ResultChanged;
            _suggestionImageQuerySession.Start(imageQueryOptions);
            _suggestionImageQueryOptionsSignature = optionsSignature;
        }

        return _suggestionImageQuerySession;
    }

    private void SuggestionTextQuerySession_ResultChanged(AppIndexTextQuerySession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(_suggestionTextQuerySession, sender))
            {
                return;
            }

            try
            {
                TextQuerySessionResult result = sender.GetResult();
                if (!result.IsValid ||
                    !string.Equals(_currentSuggestionQueryText, result.QueryPhrase, StringComparison.Ordinal) ||
                    !string.Equals(SearchBox.Text, result.QueryPhrase, StringComparison.Ordinal))
                {
                    return;
                }

                _lastTextSuggestionItems = GetTextMatchSuggestionItems(result.Matches, result.QueryPhrase);
                UpdateCombinedSuggestions();
            }
            catch (Exception ex)
            {
                SearchBox.ItemsSource = null;
                ResultStatusTextBlock.Text = "Unable to update search suggestions.";
                ShowException(ex, "Failed to update search suggestions.");
            }
        });
    }

    private void SuggestionImageQuerySession_ResultChanged(AppIndexImageQuerySession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(_suggestionImageQuerySession, sender))
            {
                return;
            }

            try
            {
                ImageQuerySessionResult result = sender.GetResult();
                if (!result.IsValid ||
                    !string.Equals(_currentSuggestionQueryText, result.QueryPhrase, StringComparison.Ordinal) ||
                    !string.Equals(SearchBox.Text, result.QueryPhrase, StringComparison.Ordinal))
                {
                    return;
                }

                _lastImageSuggestionItems = GetImageMatchSuggestionItems(result.Matches, result.QueryPhrase);
                UpdateCombinedSuggestions();
            }
            catch (Exception ex)
            {
                SearchBox.ItemsSource = null;
                ResultStatusTextBlock.Text = "Unable to update search suggestions.";
                ShowException(ex, "Failed to update search suggestions.");
            }
        });
    }

    private void UpdateCombinedSuggestions()
    {
        // Merge the latest text-session and image-session suggestions into a single dropdown.
        // The AutoSuggestBox dropdown is refreshed every time either session completes a query,
        // so the user sees image (and semantic-image) hits alongside text/OCR hits as they type.
        var combined = new List<SearchSuggestionItem>(_lastTextSuggestionItems.Count + _lastImageSuggestionItems.Count);
        combined.AddRange(_lastTextSuggestionItems);
        combined.AddRange(_lastImageSuggestionItems);
        SetSearchSuggestions(combined);
    }

    private void StopSuggestionQuerySession()
    {
        StopSuggestionTextQuerySession();
        StopSuggestionImageQuerySession();
    }

    private void StopSuggestionTextQuerySession()
    {
        if (_suggestionTextQuerySession == null)
        {
            return;
        }

        _suggestionTextQuerySession.ResultChanged -= SuggestionTextQuerySession_ResultChanged;
        _suggestionTextQuerySession.Stop();
        _suggestionTextQuerySession.Dispose();
        _suggestionTextQuerySession = null;
        _suggestionTextQueryOptionsSignature = null;
    }

    private void StopSuggestionImageQuerySession()
    {
        if (_suggestionImageQuerySession == null)
        {
            return;
        }

        _suggestionImageQuerySession.ResultChanged -= SuggestionImageQuerySession_ResultChanged;
        _suggestionImageQuerySession.Stop();
        _suggestionImageQuerySession.Dispose();
        _suggestionImageQuerySession = null;
        _suggestionImageQueryOptionsSignature = null;
    }

    private TextQueryOptions CreateTextQueryOptions()
    {
        var textQueryOptions = new TextQueryOptions
        {
            MatchScope = (QueryMatchScope)TextMatchScopeComboBox.SelectedIndex,
            TextMatchType = (TextLexicalMatchType)TextMatchTypeComboBox.SelectedIndex,
        };

        string queryLanguage = QueryLanguageTextBox.Text;
        if (!string.IsNullOrWhiteSpace(queryLanguage))
        {
            textQueryOptions.Language = queryLanguage;
        }

        return textQueryOptions;
    }

    private ImageQueryOptions CreateImageQueryOptions()
    {
        var imageQueryOptions = new ImageQueryOptions
        {
            MatchScope = (QueryMatchScope)ImageMatchScopeComboBox.SelectedIndex
        };

        string queryLanguage = QueryLanguageTextBox.Text;
        if (!string.IsNullOrWhiteSpace(queryLanguage))
        {
            imageQueryOptions.Language = queryLanguage;
        }

        return imageQueryOptions;
    }

    private string GetTextQueryOptionsSignature()
    {
        return $"{TextMatchScopeComboBox.SelectedIndex}|{TextMatchTypeComboBox.SelectedIndex}|{QueryLanguageTextBox.Text.Trim()}";
    }

    private string GetImageQueryOptionsSignature()
    {
        return $"{ImageMatchScopeComboBox.SelectedIndex}|{QueryLanguageTextBox.Text.Trim()}";
    }

    private void SetSearchSuggestions(List<SearchSuggestionItem> suggestions)
    {
        SearchBox.ItemsSource = suggestions.Count == 0 ? null : suggestions;
        SearchBox.IsSuggestionListOpen = suggestions.Count > 0 && !string.IsNullOrWhiteSpace(SearchBox.Text);
    }

    private List<SearchSuggestionItem> GetTextMatchSuggestionItems(IReadOnlyList<TextQueryMatch> matches, string queryText)
    {
        // Text + OCR suggestions only. Semantic-image hits are streamed separately by the
        // AppIndexImageQuerySession and merged into the same dropdown via UpdateCombinedSuggestions,
        // so an empty list here does not mean "no results overall" -- it just means the text session
        // had no matches for the current query phrase yet.
        return GetTextMatchDisplayItems(matches)
            .Select(text => new SearchSuggestionItem(text, queryText))
            .ToList();
    }

    private List<SearchSuggestionItem> GetImageMatchSuggestionItems(IReadOnlyList<ImageQueryMatch> matches, string queryText)
    {
        var suggestions = new List<SearchSuggestionItem>();
        foreach (var match in matches)
        {
            if (match.ContentKind == QueryMatchContentKind.AppManagedImage &&
                match is AppManagedImageQueryMatch imageResult &&
                simpleImageData.ContainsKey(match.ContentId))
            {
                string label = imageResult.RegionOfInterest.HasValue
                    ? $"{match.ContentId}: image region match"
                    : $"{match.ContentId}: image match";
                suggestions.Add(new SearchSuggestionItem(label, queryText));
            }
        }

        return suggestions;
    }

    private List<string> GetTextMatchDisplayItems(IReadOnlyList<TextQueryMatch> matches)
    {
        var displayItems = new List<string>();
        foreach (var match in matches)
        {
            Debug.WriteLine(match.ContentId);
            string? displayText = GetTextMatchDisplayText(match);
            if (!string.IsNullOrWhiteSpace(displayText))
            {
                displayItems.Add(displayText);
            }
        }

        return displayItems;
    }

    private string? GetTextMatchDisplayText(TextQueryMatch match)
    {
        if (match.ContentKind == QueryMatchContentKind.AppManagedText &&
            match is AppManagedTextQueryMatch textResult &&
            simpleTextData.TryGetValue(match.ContentId, out var fullText))
        {
            return $"{match.ContentId}: {CreateSnippet(fullText, textResult.TextOffset, textResult.TextLength)}";
        }

        return null;
    }

    private List<SearchImageResult> GetImageMatchDisplayItems(IReadOnlyList<ImageQueryMatch> imageMatches, IReadOnlyList<TextQueryMatch> textMatches)
    {
        var displayItems = new Dictionary<string, SearchImageResult>(StringComparer.Ordinal);
        foreach (var match in imageMatches)
        {
            Debug.WriteLine(match.ContentId);
            if (match.ContentKind == QueryMatchContentKind.AppManagedImage &&
                match is AppManagedImageQueryMatch imageResult &&
                simpleImageData.TryGetValue(match.ContentId, out var imagePath))
            {
                string caption = imageResult.RegionOfInterest.HasValue
                    ? $"{match.ContentId}: image region match"
                    : $"{match.ContentId}: image match";
                displayItems[match.ContentId] = CreateImageResult(match.ContentId, imagePath, imageResult.RegionOfInterest, caption);
            }
        }

        foreach (var match in textMatches)
        {
            if (match.ContentKind == QueryMatchContentKind.AppManagedOcrText &&
                match is AppManagedOcrTextQueryMatch ocrResult &&
                simpleImageData.TryGetValue(match.ContentId, out var imagePath))
            {
                string ocrCaption = $"{match.ContentId}: OCR match - {CreateSnippet(ocrResult.Fragment, 0, ocrResult.Fragment.Length, 80)}";
                SearchImageResult ocrImageResult = CreateImageResult(match.ContentId, imagePath, ocrResult.Subregion, ocrCaption);

                if (displayItems.TryGetValue(match.ContentId, out SearchImageResult? existingResult))
                {
                    string mergedCaption = $"{match.ContentId}: image and OCR match - {CreateSnippet(ocrResult.Fragment, 0, ocrResult.Fragment.Length, 80)}";
                    existingResult.Caption = mergedCaption;
                    if (ocrImageResult.HasRegion || !existingResult.HasRegion)
                    {
                        ocrImageResult.Caption = mergedCaption;
                        displayItems[match.ContentId] = ocrImageResult;
                    }
                }
                else
                {
                    displayItems[match.ContentId] = ocrImageResult;
                }
            }
        }

        return displayItems.Values.ToList();
    }

    private SearchImageResult CreateImageResult(string contentId, string imagePath, Windows.Foundation.Rect? sourceRegion, string caption)
    {
        double displayHeight = ImageResultDisplayWidth;
        var result = new SearchImageResult
        {
            ImageSource = imagePath,
            Caption = caption,
            DisplayWidth = ImageResultDisplayWidth,
            DisplayHeight = displayHeight,
            RegionVisibility = Visibility.Collapsed
        };

        if (!_imageDimensions.TryGetValue(contentId, out ImageDimensions dimensions) ||
            dimensions.Width <= 0 ||
            dimensions.Height <= 0)
        {
            return result;
        }

        displayHeight = ImageResultDisplayWidth * dimensions.Height / dimensions.Width;
        result.DisplayHeight = displayHeight;

        if (!sourceRegion.HasValue)
        {
            return result;
        }

        Windows.Foundation.Rect region = sourceRegion.Value;
        Windows.Foundation.Rect pixelRegion = ToPixelRegion(region, dimensions);
        double left = Math.Clamp(pixelRegion.X, 0, dimensions.Width);
        double top = Math.Clamp(pixelRegion.Y, 0, dimensions.Height);
        double right = Math.Clamp(pixelRegion.X + pixelRegion.Width, 0, dimensions.Width);
        double bottom = Math.Clamp(pixelRegion.Y + pixelRegion.Height, 0, dimensions.Height);

        if (right <= left || bottom <= top)
        {
            return result;
        }

        result.RegionLeft = left * ImageResultDisplayWidth / dimensions.Width;
        result.RegionTop = top * displayHeight / dimensions.Height;
        result.RegionWidth = (right - left) * ImageResultDisplayWidth / dimensions.Width;
        result.RegionHeight = (bottom - top) * displayHeight / dimensions.Height;
        result.RegionVisibility = Visibility.Visible;

        return result;
    }

    private static Windows.Foundation.Rect ToPixelRegion(Windows.Foundation.Rect region, ImageDimensions dimensions)
    {
        bool isNormalized =
            region.X >= 0 &&
            region.Y >= 0 &&
            region.X <= 1 &&
            region.Y <= 1 &&
            region.Width > 0 &&
            region.Height > 0 &&
            region.Width <= 1 &&
            region.Height <= 1;

        return isNormalized
            ? new Windows.Foundation.Rect(
                region.X * dimensions.Width,
                region.Y * dimensions.Height,
                region.Width * dimensions.Width,
                region.Height * dimensions.Height)
            : region;
    }

    private static string CreateSnippet(string text, int offset, int length, int maxLength = 180)
    {
        if (offset >= 0 &&
            length > 0 &&
            offset <= text.Length &&
            length <= text.Length - offset)
        {
            int contextLength = Math.Max(20, (maxLength - Math.Min(length, maxLength)) / 2);
            int start = Math.Max(0, offset - contextLength);
            int end = Math.Min(text.Length, offset + length + contextLength);
            string snippet = text.Substring(start, end - start);

            if (start > 0)
            {
                snippet = "..." + snippet;
            }

            if (end < text.Length)
            {
                snippet += "...";
            }

            return TruncateForDisplay(snippet, maxLength);
        }

        return TruncateForDisplay(text, maxLength);
    }

    private static string TruncateForDisplay(string text, int maxLength)
    {
        string normalized = text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();

        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }

    public sealed class SearchSuggestionItem
    {
        public SearchSuggestionItem(string text, string queryText, bool isPlaceholder = false)
        {
            Text = text;
            QueryText = queryText;
            IsPlaceholder = isPlaceholder;
        }

        public string Text { get; }

        public string QueryText { get; }

        public bool IsPlaceholder { get; }

        public static SearchSuggestionItem Searching(string queryText)
        {
            return new SearchSuggestionItem("Searching...", queryText, true);
        }

        public static SearchSuggestionItem Error(string queryText)
        {
            return new SearchSuggestionItem("Search suggestions unavailable.", queryText, true);
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public sealed class SearchImageResult
    {
        public string ImageSource { get; init; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public double DisplayWidth { get; set; }

        public double DisplayHeight { get; set; }

        public double RegionLeft { get; set; }

        public double RegionTop { get; set; }

        public double RegionWidth { get; set; }

        public double RegionHeight { get; set; }

        public Visibility RegionVisibility { get; set; } = Visibility.Collapsed;

        public bool HasRegion => RegionVisibility == Visibility.Visible;
    }

    private readonly record struct ImageDimensions(int Width, int Height);

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        _querySubmittedCts?.Cancel();
        _querySubmittedCts?.Dispose();
        _querySubmittedCts = new CancellationTokenSource();
        return _querySubmittedCts.Token;
    }

    private void CancelQuerySubmitted()
    {
        _querySubmittedCts?.Cancel();
        _querySubmittedCts?.Dispose();
        _querySubmittedCts = null;
    }
}