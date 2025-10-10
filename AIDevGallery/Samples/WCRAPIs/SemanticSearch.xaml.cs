// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using ColorCode.Compilation.Languages;
using CommunityToolkit.WinUI;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Graphics.Imaging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    Name = "Semantic Search",
    Model1Types = [ModelType.SemanticSearch],
    Scenario = ScenarioType.TextSemanticSearch,
    Id = "F8465A45-8E23-4485-8C16-9909E96EACF6",
    AssetFilenames = [
        "OCR.png",
        "Enhance.png",
        "Road.png",
    ],
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]


internal sealed partial class SemanticSearch : BaseSamplePage
{
    ObservableCollection<TextDataItem> TextDataItems { get; } = new();
    ObservableCollection<ImageDataItem> ImageDataItems { get; } = new();

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
        //var result = AppContentIndexer.GetOrCreateIndex("myIndex");

        //if (!result.Succeeded)
        //{
        //    throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
        //}

        //// If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
        //if (result.Status == GetOrCreateIndexStatus.CreatedNew)
        //{
        //    Console.WriteLine("Created a new index");
        //}
        //else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
        //{
        //    Console.WriteLine("Opened an existing index");
        //}

        //_indexer = result.Indexer;
        //var isIdle = await _indexer.WaitForIndexingIdleAsync(50000);

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        TextDataTabView.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        _indexer?.Dispose();
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

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        string results = "";
        CancellationToken ct = CancelGenerationAndGetNewToken();

        Task.Run(() =>
        {
            // We search the index using a semantic query:
            AppIndexQuery queryCursor = _indexer.CreateQuery(SearchTextBox.Text);
            IReadOnlyList<TextQueryMatch> textMatches = queryCursor.GetNextTextMatches(5);

            // Nothing in the index exactly matches what we queried but item1 is similar to the query so we expect
            // that to be the first match.
            foreach (var match in textMatches)
            {
                Console.WriteLine(match.ContentId);
                if (match.ContentKind == QueryMatchContentKind.AppManagedText)
                {
                    AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;

                    // Only part of the original string may match the query. So we can use TextOffset and TextLength to extract the match.
                    // In this example, we might imagine that the substring "Cats are cute and fluffy" from "item1" is the top match for the query.
                    string matchingData = simpleTextData[match.ContentId];
                    string matchingString = matchingData.Substring(textResult.TextOffset, textResult.TextLength);

                    results += matchingString + "\n\n";
                }
            }
        },
        ct);

        DispatcherQueue.TryEnqueue(() =>
        {
            this.OutputProgressBar.Visibility = Visibility.Visible;
            this.ResultsGrid.Visibility = Visibility.Visible;

            ResultsTextBlock.Text = results;
        });
    }

    private async void DataTabView_AddTabButtonClick(TabView sender, object args)
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

        (sender as TabView).SelectedIndex = TextDataItems.Count - 1;

        IndexingMessage.IsOpen = true;
        await Task.Run(async () =>
        { 
           await IndexTextData(newId, defaultValue);
        });
        IndexingMessage.IsOpen = false;
    }

    private async void DataTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is TextDataItem item)
        {
            TextDataItems.Remove(item);

            if (simpleTextData.ContainsKey(item.Id))
            {
                simpleTextData.Remove(item.Id);
            }

            RemovedItemMessage.IsOpen = true;
            RemovedItemMessage.Message = $"Removed {item.Id} from index";
            await Task.Run(async () =>
            {
                await RemoveItemFromIndex(item.Id);
            });
            RemovedItemMessage.IsOpen = false;
        }
    }

    private async Task RemoveItemFromIndex(string id)
    {
        // Remove item from index
        //_indexer.Remove(id);
        await Task.Delay(2000);
    }

    private async Task IndexTextData(string id, string value)
    {
        // Index Textbox content
        //IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, value);
        //_indexer.AddOrUpdate(textContent);

        //var isIdle = await _indexer.WaitForIndexingIdleAsync(50000); 

        await Task.Delay(2000);
    }
    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
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
}
internal record class TextDataItem
{
    public string Id { get; set; }
    public string Value { get; set; }

}

internal record class ImageDataItem
{
    public string Id { get; set; }
    public string ImageSource { get; set; }

}