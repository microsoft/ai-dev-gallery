// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
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
    private ObservableCollection<TextDataItem> TextDataItems { get; } = new();
    private ObservableCollection<ImageDataItem> ImageDataItems { get; } = new();

    // This is some text data that we want to add to the index:
    private Dictionary<string, string> simpleTextData = new Dictionary<string, string>
    {
        { "item1", "Preparing a hearty vegetable stew begins with chopping fresh carrots, onions, and celery. Sauté them in olive oil until fragrant, then add diced tomatoes, herbs, and vegetable broth. Simmer gently for an hour, allowing flavors to meld into a comforting dish perfect for cold evenings." },
        { "item2", "Modern exhibition design combines narrative flow with spatial strategy. Lighting emphasizes focal objects while circulation paths avoid bottlenecks. Materials complement artifacts without visual competition. Interactive elements invite engagement but remain intuitive. Environmental controls protect sensitive works. Success balances scholarship, aesthetics, and visitor experience through thoughtful, cohesive design choices." },
        { "item3", "Domestic cats communicate through posture, tail flicks, and vocalizations. Play mimics hunting behaviors like stalking and pouncing, supporting agility and mental stimulation. Scratching maintains claws and marks territory, so provide sturdy posts. Balanced diets, hydration, and routine veterinary care sustain health. Safe retreats and vertical spaces reduce stress and encourage exploration." },
        { "item4", "Snowboarding across pristine slopes combines agility, balance, and speed. Riders carve smooth turns on powder, adjust stance for control, and master jumps in terrain parks. Essential gear includes boots, bindings, and helmets for safety. Embrace crisp alpine air while perfecting tricks and enjoying the thrill of winter adventure." },
        { "item5", "Urban beekeeping thrives with diverse forage across seasons. Rooftop hives benefit from trees, herbs, and staggered blooms. Provide shallow water sources and shade to counter heat stress. Prevent swarms through timely inspections and splits. Monitor mites with sugar rolls and rotate treatments. Honey reflects city terroir with surprising floral complexity." }
    };

    private Dictionary<string, string> simpleImageData = new Dictionary<string, string>
    {
        { "image1", "ms-appx:///Assets/InteriorDesign.png" },
        { "image2", "ms-appx:///Assets/TofuBowlRecipe.png" },
        { "image3", "ms-appx:///Assets/ShakshukaRecipe.png" },
    };

    private AppContentIndexer? _indexer;
    private CancellationTokenSource cts = new();

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
        _indexer?.RemoveAll();
        _indexer?.Dispose();
        _indexer = null;
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
                    var isIdle = await _indexer?.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
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
                    var isIdle = await _indexer?.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
                });
            }

            IndexingMessage.IsOpen = false;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string searchText = SearchBox.Text;
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

        ResultsGrid.Visibility = Visibility.Visible;
        ResultStatusTextBlock.Text = "Searching...";

        // Create text query options
        TextQueryOptions textQueryOptions = new TextQueryOptions();

        // Set language if provided
        string queryLanguage = QueryLanguageTextBox.Text;
        if (!string.IsNullOrWhiteSpace(queryLanguage))
        {
            textQueryOptions.Language = queryLanguage;
        }

        // text query options
        textQueryOptions.MatchScope = (QueryMatchScope)TextMatchScopeComboBox.SelectedIndex;
        textQueryOptions.TextMatchType = (TextLexicalMatchType)TextMatchTypeComboBox.SelectedIndex;

        // Create image match options
        ImageQueryOptions imageQueryOptions = new ImageQueryOptions
        {
            MatchScope = (QueryMatchScope)ImageMatchScopeComboBox.SelectedIndex,
            ImageOcrTextMatchType = (TextLexicalMatchType)ImageOcrTextMatchTypeComboBox.SelectedIndex
        };

        CancellationToken ct = CancelGenerationAndGetNewToken();

        string textResults = string.Empty;
        var imageResults = new List<string>();

        Task.Run(
            () =>
            {
                // Create text query
                AppIndexTextQuery textQuery = _indexer.CreateTextQuery(searchText, textQueryOptions);

                // Get text matches
                IReadOnlyList<TextQueryMatch> textMatches = textQuery.GetNextMatches(5);

                foreach (var match in textMatches)
                {
                    Debug.WriteLine(match.ContentId);
                    if (match.ContentKind == QueryMatchContentKind.AppManagedText)
                    {
                        AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;
                        string matchingData = simpleTextData[match.ContentId];
                        int offset = textResult.TextOffset;
                        int length = textResult.TextLength;
                        string matchingString = matchingData.Substring(offset, length);
                        textResults += matchingString + "\n\n";
                    }
                }

                // Create text query
                AppIndexImageQuery imageQuery = _indexer.CreateImageQuery(searchText, imageQueryOptions);

                // Get image matches
                IReadOnlyList<ImageQueryMatch> imageMatches = imageQuery.GetNextMatches(5);

                foreach (var match in imageMatches)
                {
                    Debug.WriteLine(match.ContentId);
                    if (match.ContentKind == QueryMatchContentKind.AppManagedImage)
                    {
                        AppManagedImageQueryMatch imageResult = (AppManagedImageQueryMatch)match;

                        if (simpleImageData.TryGetValue(imageResult.ContentId, out var imagePath))
                        {
                            imageResults.Add(imagePath);
                        }
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (textMatches.Count == 0 && imageResults.Count == 0)
                    {
                        ResultStatusTextBlock.Text = "No results found.";
                    }
                    else
                    {
                        ResultStatusTextBlock.Text = "Search Results:";
                    }

                    if (textMatches.Count > 0)
                    {
                        ResultsTextBlock.Visibility = Visibility.Visible;
                        ResultsTextBlock.Text = textResults;
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
                });
            },
            ct);
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
        _indexer.Remove(id);
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

        // Index image content
        IndexableAppContent imageContent = AppManagedIndexableAppContent.CreateFromBitmap(id, bitmap);
        _indexer.AddOrUpdate(imageContent);
    }

    private async void IndexAll()
    {
        IndexingMessage.IsOpen = true;

        await Task.Run(async () =>
        {
            foreach (var kvp in simpleTextData)
            {
                IndexTextData(kvp.Key, kvp.Value);
            }

            foreach (var kvp in simpleImageData)
            {
                SoftwareBitmap? bitmap = await LoadBitmap(kvp.Value);
                if (bitmap != null)
                {
                    IndexImageData(kvp.Key, bitmap);
                }
            }

            var isIdle = await _indexer?.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));
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
            uploadTextButton.IsEnabled = imageSemanticAvailable || imageOcrAvailable;

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

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }
}