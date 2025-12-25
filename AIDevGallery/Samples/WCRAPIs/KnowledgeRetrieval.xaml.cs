// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Knowledge Retrieval (RAG)",
    Model1Types = [ModelType.KnowledgeRetrieval, ModelType.PhiSilica],
    Scenario = ScenarioType.TextRetrievalAugmentedGeneration,
    Id = "6a526fdd-359f-4eac-9aa6-f01db11ae542",
    SharedCode = [
        SharedCodeEnum.DataItems,
        SharedCodeEnum.Message,
        SharedCodeEnum.ChatTemplateSelector
    ],
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.Extensions.AI",
        "Microsoft.WindowsAppSDK"
    ],
    Icon = "\uEE6F")]

internal sealed partial class KnowledgeRetrieval : BaseSamplePage
{
    private ObservableCollection<TextDataItem> TextDataItems { get; } = new();
    public ObservableCollection<Message> Messages { get; } = [];

    private IChatClient? _model;
    private ScrollViewer? _scrollViewer;
    private bool _isImeActive = true;

    // Markers for the assistant's think area (displayed in a dedicated UI region).
    private static readonly string[] ThinkTagOpens = new[] { "<think>", "<thought>", "<reasoning>" };
    private static readonly string[] ThinkTagCloses = new[] { "</think>", "</thought>", "</reasoning>" };
    private static readonly int MaxOpenThinkMarkerLength = ThinkTagOpens.Max(s => s.Length);

    // This is some text data that we want to add to the index:
    private Dictionary<string, string> simpleTextData = new Dictionary<string, string>
    {
        { "item1", "Preparing a hearty vegetable stew begins with chopping fresh carrots, onions, and celery. Sauté them in olive oil until fragrant, then add diced tomatoes, herbs, and vegetable broth. Simmer gently for an hour, allowing flavors to meld into a comforting dish perfect for cold evenings." },
        { "item2", "Modern exhibition design combines narrative flow with spatial strategy. Lighting emphasizes focal objects while circulation paths avoid bottlenecks. Materials complement artifacts without visual competition. Interactive elements invite engagement but remain intuitive. Environmental controls protect sensitive works. Success balances scholarship, aesthetics, and visitor experience through thoughtful, cohesive design choices." },
        { "item3", "Domestic cats communicate through posture, tail flicks, and vocalizations. Play mimics hunting behaviors like stalking and pouncing, supporting agility and mental stimulation. Scratching maintains claws and marks territory, so provide sturdy posts. Balanced diets, hydration, and routine veterinary care sustain health. Safe retreats and vertical spaces reduce stress and encourage exploration." },
        { "item4", "Snowboarding across pristine slopes combines agility, balance, and speed. Riders carve smooth turns on powder, adjust stance for control, and master jumps in terrain parks. Essential gear includes boots, bindings, and helmets for safety. Embrace crisp alpine air while perfecting tricks and enjoying the thrill of winter adventure." },
        { "item5", "Urban beekeeping thrives with diverse forage across seasons. Rooftop hives benefit from trees, herbs, and staggered blooms. Provide shallow water sources and shade to counter heat stress. Prevent swarms through timely inspections and splits. Monitor mites with sugar rolls and rotate treatments. Honey reflects city terroir with surprising floral complexity." }
    };

    private AppContentIndexer? _indexer;
    private CancellationTokenSource? cts = new();

    public KnowledgeRetrieval()
    {
        this.InitializeComponent();
        this.Unloaded += (s, e) =>
        {
            CleanUp();
        };
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>

        PopulateTextData();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await Task.Run(async () =>
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
                    loadingCanceledToken: CancellationToken.None);

                _model = await ragSampleParams.GetIChatClientAsync();
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }

            // Load AppContentIndexer
            var result = AppContentIndexer.GetOrCreateIndex("knowledgeRetrievalIndex");

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
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

            sampleParams.NotifyCompletion();
        });

        IndexAll();
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
        _indexer?.RemoveAll();
        _indexer?.Dispose();
        _indexer = null;
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

    private async Task<List<string>> BuildContextFromUserPrompt(string queryText)
    {
        if (_indexer == null)
        {
            return new List<string>();
        }

        var queryPrompts = await Task.Run(() =>
        {
            // We execute a query against the index using the user's prompt string as the query text.
            AppIndexTextQuery query = _indexer.CreateTextQuery(queryText);

            IReadOnlyList<TextQueryMatch> textMatches = query.GetNextMatches(5);

            List<string> contextSnippets = new List<string>();
            StringBuilder promptStringBuilder = new StringBuilder();
            string refIds = string.Empty;
            promptStringBuilder.AppendLine("You are a helpful assistant. Please only refer to the following pieces of information when responding to the user's prompt:");

            // For each of the matches found, we include the relevant snippets of the text files in the augmented query that we send to the language model
            foreach (var match in textMatches)
            {
                Debug.WriteLine(match.ContentId);
                if (match.ContentKind == QueryMatchContentKind.AppManagedText)
                {
                    AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;
                    string matchingData = simpleTextData[match.ContentId];
                    int offset = textResult.TextOffset;
                    int length = textResult.TextLength;
                    string matchingString;

                    if (offset >= 0 && offset < matchingData.Length && length > 0 && offset + length <= matchingData.Length)
                    {
                        // Find the substring within the loaded text that contains the match:
                        matchingString = matchingData.Substring(offset, length);
                    }
                    else
                    {
                        matchingString = matchingData;
                    }

                    promptStringBuilder.AppendLine(matchingString);
                    promptStringBuilder.AppendLine();

                    refIds += string.IsNullOrEmpty(refIds) ? match.ContentId : ", " + match.ContentId;
                }
            }

            promptStringBuilder.AppendLine("Please provide a short response of less than 50 words to the following user prompt:");
            promptStringBuilder.AppendLine(queryText);

            contextSnippets.Add(refIds);
            contextSnippets.Add(promptStringBuilder.ToString());

            return contextSnippets;
        });

        return queryPrompts;
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
            });

            cts = new CancellationTokenSource();

            // Use AppContentIndexer query here.
            var userPrompt = await BuildContextFromUserPrompt(text);

            history.Insert(0, new ChatMessage(ChatRole.System, userPrompt[1]));

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

                responseMessage.Content += "\n\n" + "Referenced items: " + userPrompt[0];
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
            string? id = textBox.Tag as string;
            string value = textBox.Text;

            // Update local dictionary and observable collection
            var item = TextDataItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.Value = value;
            }

            if (id != null)
            {
                if (simpleTextData.ContainsKey(id))
                {
                    simpleTextData[id] = value;
                }

                IndexingMessage.IsOpen = true;
                await Task.Run(() =>
                {
                    IndexTextData(id, value);
                });
            }

            IndexingMessage.IsOpen = false;
        }
    }

    private async void AddTextDataButton_Click(object sender, RoutedEventArgs e)
    {
        // Find the lowest unused id in the form itemN
        int nextIndex = 1;
        string newId;
        var existingIds = new HashSet<string>(simpleTextData.Keys.Concat(TextDataItems.Select(x => x.Id).Where(id => id != null)).Cast<string>());
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

    private async void IndexAll()
    {
        IndexingMessage.IsOpen = true;

        await Task.Run(() =>
        {
            foreach (var kvp in simpleTextData)
            {
                IndexTextData(kvp.Key, kvp.Value);
            }
        });

        IndexingMessage.IsOpen = false;
    }

    private void IndexAllButton_Click(object sender, RoutedEventArgs e)
    {
        IndexAll();
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

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }
}