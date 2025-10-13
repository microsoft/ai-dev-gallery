// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Graphics.Imaging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using OllamaSharp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Knowledge Retrieval (RAG)",
    Model1Types = [ModelType.KnowledgeRetrieval, ModelType.PhiSilica],
    Scenario = ScenarioType.TextRetrievalAugmentedGeneration,
    Id = "6A526FDD-359F-4EAC-9AA6-F01DB11AE542",
    SharedCode = [
        SharedCodeEnum.DataItems,
        SharedCodeEnum.Message,
        SharedCodeEnum.ChatTemplateSelector
    ],
    AssetFilenames = [
        "OCR.png",
        "Enhance.png",
        "Road.png",
    ],
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.Extensions.AI",
        "Microsoft.WindowsAppSDK"
    ],
    Icon = "\uEE6F")]

internal sealed partial class KnowledgeRetrieval : BaseSamplePage
{
    ObservableCollection<TextDataItem> TextDataItems { get; } = new();
    ObservableCollection<ImageDataItem> ImageDataItems { get; } = new();
    public ObservableCollection<Message> Messages { get; } = [];

    private IChatClient? _model;
    private ScrollViewer? _scrollViewer;
    private bool _isImeActive = true;

    // Markers for the assistant's think area (displayed in a dedicated UI region).
    private static readonly string[] ThinkTagOpens = new[] { "<think>", "<thought>", "<reasoning>" };
    private static readonly string[] ThinkTagCloses = new[] { "</think>", "</thought>", "</reasoning>" };
    private static readonly int MaxOpenThinkMarkerLength = ThinkTagOpens.Max(s => s.Length);

    // This is some text data that we want to add to the index:
    Dictionary<string, string> simpleTextData = new Dictionary<string, string>
    {
        {"item1", "Here is some information about Cats: Cats are cute and fluffy. Young cats are very playful." },
        {"item2", "Dogs are loyal and affectionate animals known for their companionship, intelligence, and diverse breeds." },
        {"item3", "Fish are aquatic creatures that breathe through gills and come in a vast variety of shapes, sizes, and colors." },
        {"item4", "Broccoli is a nutritious green vegetable rich in vitamins, fiber, and antioxidants." },
        {"item5", "Computers are powerful electronic devices that process information, perform calculations, and enable communication worldwide." },
        {"item6", "Music is a universal language that expresses emotions, tells stories, and connects people through rhythm and melody." },
    };

    Dictionary<string, string> simpleImageData = new Dictionary<string, string>
    {
        {"image1", "Enhance.png" },
        {"image2", "OCR.png" },
        {"image3", "Road.png" },
    };

    private AppContentIndexer _indexer;
    private CancellationTokenSource cts = new();

    public KnowledgeRetrieval()
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
        // Load chat client
        try
        {
            var ragSampleParams = new SampleNavigationParameters(
                sampleId: "6A526FDD-359F-4EAC-9AA6-F01DB11AE542",
                modelId: "PhiSilica",
                modelPath: $"file://{ModelType.PhiSilica}",
                hardwareAccelerator: HardwareAccelerator.CPU,
                promptTemplate: null,
                sampleLoadedCompletionSource: new TaskCompletionSource(),
                winMlSampleOptions: null,
                loadingCanceledToken: CancellationToken.None
            );

            _model = await ragSampleParams.GetIChatClientAsync();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }

        // Load AppContentIndexer
        var result = AppContentIndexer.GetOrCreateIndex("myIndex");

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
        }

        // If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
        if (result.Status == GetOrCreateIndexStatus.CreatedNew)
        {
            Console.WriteLine("Created a new index");
        }
        else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
        {
            Console.WriteLine("Opened an existing index");
        }

        _indexer = result.Indexer;
        var isIdle = await _indexer.WaitForIndexingIdleAsync(50000);

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        InputBox.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        CancelResponse();
        _model?.Dispose();
        _indexer?.Dispose();
    }

    private void CancelResponse()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        SendBtn.Visibility = Visibility.Visible;
        EnableInputBoxWithPlaceholder();
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter &&
            !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) &&
            sender is TextBox &&
            !string.IsNullOrWhiteSpace(InputBox.Text) &&
            _isImeActive == false)
        {
            var cursorPosition = InputBox.SelectionStart;
            var text = InputBox.Text;
            if (cursorPosition > 0 && (text[cursorPosition - 1] == '\n' || text[cursorPosition - 1] == '\r'))
            {
                text = text.Remove(cursorPosition - 1, 1);
                InputBox.Text = text;
            }

            InputBox.SelectionStart = cursorPosition - 1;

            SendMessage();
        }
        else
        {
            _isImeActive = true;
        }
    }

    private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _isImeActive = false;
    }

    private void SendMessage()
    {
        if (InputBox.Text.Length > 0)
        {
            AddMessage(InputBox.Text);
            InputBox.Text = string.Empty;
            SendBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void AddMessage(string text)
    {
        if (_model == null)
        {
            return;
        }

        Messages.Add(new Message(text.Trim(), DateTime.Now, ChatRole.User));
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputBox, "Generating response, please wait.", "ChatWaitAnnouncementActivityId"); // <exclude-line>>
        SendSampleInteractedEvent("AddMessage"); // <exclude-line>

        Task.Run(async () =>
        {
            var history = Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            var responseMessage = new Message(string.Empty, DateTime.Now, ChatRole.Assistant)
            {
                IsPending = true
            };

            DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(responseMessage);
                StopBtn.Visibility = Visibility.Visible;
                InputBox.IsEnabled = false;
                InputBox.PlaceholderText = "Please wait for the response to complete before entering a new prompt";
            });

            cts = new CancellationTokenSource();

            history.Insert(0, new ChatMessage(ChatRole.System, "You are a helpful assistant"));

            // <exclude>
            ShowDebugInfo(null);
            var swEnd = Stopwatch.StartNew();
            var swTtft = Stopwatch.StartNew();
            int outputTokens = 0;

            // </exclude>
            int currentThinkTagIndex = -1; // -1 means not inside any think/auxiliary section
            string rolling = string.Empty;

            await foreach (var messagePart in _model.GetStreamingResponseAsync(history, null, cts.Token))
            {
                // <exclude>
                if (outputTokens == 0)
                {
                    swTtft.Stop();
                }

                outputTokens++;
                double currentTps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
                ShowDebugInfo($"{Math.Round(currentTps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

                // </exclude>
                var part = messagePart;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (responseMessage.IsPending)
                    {
                        responseMessage.IsPending = false;
                    }

                    // Parse character by character/fragment to identify think tags (e.g., <think>...</think>, <thought>...</thought>)
                    rolling += part;

                    while (!string.IsNullOrEmpty(rolling))
                    {
                        if (currentThinkTagIndex == -1)
                        {
                            // Find the earliest occurring open marker among supported think tags
                            int earliestIdx = -1;
                            int foundTagIndex = -1;
                            for (int i = 0; i < ThinkTagOpens.Length; i++)
                            {
                                int idx = rolling.IndexOf(ThinkTagOpens[i], StringComparison.Ordinal);
                                if (idx >= 0 && (earliestIdx == -1 || idx < earliestIdx))
                                {
                                    earliestIdx = idx;
                                    foundTagIndex = i;
                                }
                            }

                            if (earliestIdx >= 0)
                            {
                                // Output safe content before the start marker
                                if (earliestIdx > 0)
                                {
                                    responseMessage.Content = string.Concat(responseMessage.Content, rolling.AsSpan(0, earliestIdx));
                                }

                                // Enter think mode, discard the marker text itself
                                rolling = rolling.Substring(earliestIdx + ThinkTagOpens[foundTagIndex].Length);
                                currentThinkTagIndex = foundTagIndex;
                                continue;
                            }
                            else
                            {
                                // Start marker not found: only flush safe parts, keep the tail that might form a marker
                                int keep = MaxOpenThinkMarkerLength - 1;
                                if (rolling.Length > keep)
                                {
                                    int flushLen = rolling.Length - keep;
                                    responseMessage.Content = string.Concat(responseMessage.Content.TrimStart(), rolling.AsSpan(0, flushLen));
                                    rolling = rolling.Substring(flushLen);
                                }

                                break;
                            }
                        }
                        else
                        {
                            string closeMarker = ThinkTagCloses[currentThinkTagIndex];
                            int closeIdx = rolling.IndexOf(closeMarker, StringComparison.Ordinal);
                            if (closeIdx >= 0)
                            {
                                // Append content before the closing marker to the think box
                                if (closeIdx > 0)
                                {
                                    responseMessage.ThinkContent = string.Concat(responseMessage.ThinkContent, rolling.AsSpan(0, closeIdx));
                                }

                                // Exit think mode, discard the closing marker
                                rolling = rolling.Substring(closeIdx + closeMarker.Length);
                                currentThinkTagIndex = -1;
                                continue;
                            }
                            else
                            {
                                // Closing marker not found: only flush safe parts, keep the tail that might form a marker
                                int keep = closeMarker.Length - 1;
                                if (rolling.Length > keep)
                                {
                                    int flushLen = rolling.Length - keep;
                                    responseMessage.ThinkContent = string.Concat(responseMessage.ThinkContent, rolling.AsSpan(0, flushLen));
                                    rolling = rolling.Substring(flushLen);
                                }

                                break;
                            }
                        }
                    }

                    // <exclude>
                    if (!contentStartedBeingGenerated)
                    {
                        NarratorHelper.Announce(InputBox, "Response has started generating.", "ChatResponseAnnouncementActivityId");
                        contentStartedBeingGenerated = true;
                    }

                    // </exclude>
                });
            }

            // Flush remaining tail content (if any)
            DispatcherQueue.TryEnqueue(() =>
            {
                responseMessage.IsPending = false;
                if (!string.IsNullOrEmpty(rolling))
                {
                    if (currentThinkTagIndex != -1)
                    {
                        responseMessage.ThinkContent += rolling;
                    }
                    else
                    {
                        responseMessage.Content = responseMessage.Content.TrimStart() + rolling;
                    }
                }
            });

            // <exclude>
            swEnd.Stop();
            double tps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
            ShowDebugInfo($"{Math.Round(tps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

            // </exclude>
            cts?.Dispose();
            cts = null;

            DispatcherQueue.TryEnqueue(() =>
            {
                NarratorHelper.Announce(InputBox, "Content has finished generating.", "ChatDoneAnnouncementActivityId"); // <exclude-line>
                StopBtn.Visibility = Visibility.Collapsed;
                SendBtn.Visibility = Visibility.Visible;
                EnableInputBoxWithPlaceholder();
            });
        });
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelResponse();
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SendBtn.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);
    }

    private void EnableInputBoxWithPlaceholder()
    {
        InputBox.IsEnabled = true;
        InputBox.PlaceholderText = "Enter your prompt (Press Shift + Enter to insert a newline)";
    }

    private void InvertedListView_Loaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer = FindElement<ScrollViewer>(InvertedListView);

        ItemsStackPanel? itemsStackPanel = FindElement<ItemsStackPanel>(InvertedListView);
        if (itemsStackPanel != null)
        {
            itemsStackPanel.SizeChanged += ItemsStackPanel_SizeChanged;
        }
    }

    private void ItemsStackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_scrollViewer != null)
        {
            bool isScrollbarVisible = _scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;

            if (isScrollbarVisible)
            {
                InvertedListView.Padding = new Thickness(-12, 0, 12, 24);
            }
            else
            {
                InvertedListView.Padding = new Thickness(-12, 0, -12, 24);
            }
        }
    }

    private T? FindElement<T>(DependencyObject element)
        where T : DependencyObject
    {
        if (element is T targetElement)
        {
            return targetElement;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = FindElement<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private async void SemanticTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            string id = textBox.Tag as string;
            string value = textBox.Text;

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

            IndexingMessage.IsOpen = true;
            await Task.Run(async () =>
            {
                await IndexTextData(id, value);
            });
            IndexingMessage.IsOpen = false;
        }
    }

    private async void ImageData_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Image image)
        {
            string id = image.Tag as string;
            string uriString = null;

            if (image.Source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
            {
                uriString = bitmapImage.UriSource.ToString();
            }

            SoftwareBitmap bitmap = null;
            if (!string.IsNullOrEmpty(uriString))
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
                    bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                }
            }

            // Update local dictionary and observable collection
            var item = ImageDataItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                var fileName = Path.GetFileName(uriString);
                item.ImageSource = fileName;
            }

            if (simpleImageData.ContainsKey(id))
            {
                simpleImageData[id] = uriString;
            }
            else if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(uriString))
            {
                simpleImageData.Add(id, uriString);
            }

            IndexingMessage.IsOpen = true;
            await Task.Run(async () =>
            {
                await IndexImageData(id, bitmap);
            });
            IndexingMessage.IsOpen = false;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string searchText = InputBox.Text;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            Console.WriteLine("Search text is empty.");
            return;
        }

        if (_indexer == null) return;

        // Create query options
        AppIndexQueryOptions queryOptions = new AppIndexQueryOptions();

        // Set language if provided
        //string queryLanguage = QueryLanguageTextBox.Text;
        //if (!string.IsNullOrWhiteSpace(queryLanguage))
        //{
        //    queryOptions.Language = queryLanguage;
        //}

        //// Create text match options
        //TextMatchOptions textMatchOptions = new TextMatchOptions
        //{
        //    MatchScope = (QueryMatchScope)TextMatchScopeComboBox.SelectedIndex,
        //    TextMatchType = (TextLexicalMatchType)TextMatchTypeComboBox.SelectedIndex
        //};

        //// Create image match options
        //ImageMatchOptions imageMatchOptions = new ImageMatchOptions
        //{
        //    MatchScope = (QueryMatchScope)ImageMatchScopeComboBox.SelectedIndex,
        //    ImageOcrTextMatchType = (TextLexicalMatchType)ImageOcrTextMatchTypeComboBox.SelectedIndex
        //};

        CancellationToken ct = CancelGenerationAndGetNewToken();

        string textResults = "";
        var imageResults = new List<string>();

        Task.Run(
            async () =>
            {
                // Create query
                AppIndexQuery query = await Task.Run(() =>
                {
                    return _indexer.CreateQuery(searchText, queryOptions);
                });

                // Get text matches
                IReadOnlyList<TextQueryMatch> textMatches = await Task.Run(() =>
                {
                    return query.GetNextTextMatches(5);
                });

                if (textMatches != null && textMatches.Count > 0)
                {
                    foreach (var match in textMatches)
                    {
                        Console.WriteLine(match.ContentId);
                        if (match.ContentKind == QueryMatchContentKind.AppManagedText)
                        {
                            AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;
                            string matchingData = simpleTextData[match.ContentId];
                            string matchingString = matchingData.Substring(textResult.TextOffset, textResult.TextLength);
                            textResults += matchingString + "\n\n";
                        }
                    }
                }

                // Get image matches
                IReadOnlyList<ImageQueryMatch> imageMatches = await Task.Run(() =>
                {
                    return query.GetNextImageMatches(5);
                });


                if (imageMatches != null && imageMatches.Count > 0)
                {
                    foreach (var match in imageMatches)
                    {
                        if (simpleImageData.TryGetValue(match.ContentId, out var imagePath))
                        {
                            // If imagePath is just the file name, prepend the ms-appx URI
                            var uri = imagePath.StartsWith("ms-appx") ? imagePath : $"ms-appx:///Assets/{imagePath}";
                            imageResults.Add(uri);
                        }
                    }
                }
            },
            ct);
    }

    private async void AddTextDataButton_Click(object sender, RoutedEventArgs e)
    {
        // Generate a unique id for the new item
        int nextIndex = 1;
        string newId;
        do
        {
            newId = $"item{TextDataItems.Count + nextIndex}";
            nextIndex++;
        } while (simpleTextData.ContainsKey(newId));

        string defaultValue = "New item text...";

        // Add to dictionary
        simpleTextData[newId] = defaultValue;

        // Add to observable collection
        var newItem = new TextDataItem { Id = newId, Value = defaultValue };
        TextDataItems.Add(newItem);

        IndexingMessage.IsOpen = true;
        await Task.Run(async () =>
        {
            await IndexTextData(newId, defaultValue);
        });
        IndexingMessage.IsOpen = false;
    }

    private async void closeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            if (button.Tag is TextDataItem textItem)
            {
                TextDataItems.Remove(textItem);

                if (simpleTextData.ContainsKey(textItem.Id))
                {
                    simpleTextData.Remove(textItem.Id);
                }

                RemovedItemMessage.IsOpen = true;
                RemovedItemMessage.Message = $"Removed {textItem.Id} from index";
                await Task.Run(async () =>
                {
                    await RemoveItemFromIndex(textItem.Id);
                });
                RemovedItemMessage.IsOpen = false;
            }
            else if (button.Tag is ImageDataItem imageItem)
            {
                ImageDataItems.Remove(imageItem);

                if (simpleImageData.ContainsKey(imageItem.Id))
                {
                    simpleImageData.Remove(imageItem.Id);
                }

                RemovedItemMessage.IsOpen = true;
                RemovedItemMessage.Message = $"Removed {imageItem.Id} from index";
                await Task.Run(async () =>
                {
                    await RemoveItemFromIndex(imageItem.Id);
                });
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
            } while (ImageDataItems.Any(i => i.Id == newId));

            // Create a ms-appx URI for the image (or use file path for local images)
            var imageUri = file.Path;

            // Add to collection and dictionary
            ImageDataItems.Add(new ImageDataItem { Id = newId, ImageSource = imageUri });
            simpleImageData[newId] = imageUri;
        }
    }

    private async Task RemoveItemFromIndex(string id)
    {
        // Remove item from index
        _indexer.Remove(id);
    }

    private async Task IndexTextData(string id, string value)
    {
        if (_indexer == null) return;

        // Index Textbox content
        IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, value);
        _indexer.AddOrUpdate(textContent);

        var isIdle = await _indexer.WaitForIndexingIdleAsync(50000);
    }

    private async Task IndexImageData(string id, SoftwareBitmap bitmap)
    {
        if (_indexer == null) return;

        // Index inage content
        IndexableAppContent imageContent = AppManagedIndexableAppContent.CreateFromBitmap(id, bitmap);
        _indexer.AddOrUpdate(imageContent);

        var isIdle = await _indexer.WaitForIndexingIdleAsync(50000);
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
            var uri = $"ms-appx:///Assets/{kvp.Value}";
            ImageDataItems.Add(new ImageDataItem { Id = kvp.Key, ImageSource = uri });
        }
    }

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }

    private static bool IsImageFile(string fileName)
    {
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        return imageExtensions.Contains(System.IO.Path.GetExtension(fileName)?.ToLowerInvariant());
    }
}

