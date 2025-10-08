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
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
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
    Name = "Index Capabilities",
    Model1Types = [ModelType.SemanticSearch],
    Scenario = ScenarioType.TextSemanticSearch,
    Id = "3EDB639A-A7CA-4885-BC95-5F1DDD29B2C3",
    AssetFilenames = [
        "OCR.png"
    ],
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]

internal sealed partial class IndexCapability : BaseSamplePage
{
    public IndexCapability()
    {
        this.InitializeComponent();
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

        //using AppContentIndexer indexer = result.Indexer;
        //var isIdle = await indexer.WaitForIndexingIdleAsync(50000);

        sampleParams.NotifyCompletion();
    }

    private void SemanticTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        this.SearchButton.IsEnabled = !string.IsNullOrEmpty(this.SearchTextBox.Text) && !string.IsNullOrEmpty(this.SourceTextBox.Text);
        ErrorMessage.IsOpen = string.IsNullOrEmpty(this.SearchTextBox.Text) || string.IsNullOrEmpty(this.SourceTextBox.Text);
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var sourceText = this.SourceTextBox.Text;
        var searchText = this.SearchTextBox.Text;

        if (!string.IsNullOrEmpty(sourceText) && !string.IsNullOrEmpty(searchText))
        {
            this.OutputProgressBar.Visibility = Visibility.Visible;
            this.ResultsGrid.Visibility = Visibility.Visible;
            Search(sourceText, searchText);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && SearchTextBox.Text.Length > 0)
        {
            var sourceText = this.SourceTextBox.Text;
            var searchText = this.SearchTextBox.Text;

            if (!string.IsNullOrEmpty(sourceText) && !string.IsNullOrEmpty(searchText))
            {
                this.OutputProgressBar.Visibility = Visibility.Visible;
                this.ResultsGrid.Visibility = Visibility.Visible;
                SearchTextBox.IsEnabled = false;
                Search(sourceText, searchText);
            }
        }
    }

    public void Search(string sourceText, string searchText)
    {
        //CancellationToken ct = CancelGenerationAndGetNewToken();

        
    }

}