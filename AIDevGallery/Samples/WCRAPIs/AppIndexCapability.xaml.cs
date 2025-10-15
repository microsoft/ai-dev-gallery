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
    Model1Types = [ModelType.AppIndexCapability],
    Scenario = ScenarioType.TextSemanticSearch,
    Id = "3EDB639A-A7CA-4885-BC95-5F1DDD29B2C3",
    NugetPackageReferences = [
        "Microsoft.Extensions.AI",
        "Microsoft.WindowsAppSDK"
    ],
    Icon = "\uEE6F")]

internal sealed partial class AppIndexCapability : BaseSamplePage
{
    private AppContentIndexer _indexer;

    public AppIndexCapability()
    {
        this.InitializeComponent();
        this.Unloaded += (s, e) =>
        {
            CleanUp();
        };
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await Task.Run(async () =>
        {
            var result = AppContentIndexer.GetOrCreateIndex("indexCapabilityIndex");

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

            await _indexer.WaitForIndexCapabilitiesAsync();
            await _indexer.WaitForIndexingIdleAsync(50000);

            _indexer.Listener.IndexCapabilitiesChanged += Listener_IndexCapabilitiesChanged;

            LoadAppIndexCapabilities();
            LoadSystemCapabilities();

            sampleParams.NotifyCompletion();
        });
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        _indexer?.Dispose();
        _indexer = null;
    }

    private async void LoadSystemCapabilities()
    {
        IndexCapabilitiesOfCurrentSystem capabilities = await Task.Run(() =>
        {
            return AppContentIndexer.GetIndexCapabilitiesOfCurrentSystem();
        });

        DispatcherQueue.TryEnqueue(() =>
        {
            // Status is one of: Ready, NotReady, DisabledByPolicy or NotSupported.
            lexicalCapabilityResultText.Text = capabilities.GetIndexCapabilityStatus(IndexCapability.TextLexical).ToString();
            semanticCapabilityResultText.Text = capabilities.GetIndexCapabilityStatus(IndexCapability.TextSemantic).ToString();
            oCRCapabilityResultText.Text = capabilities.GetIndexCapabilityStatus(IndexCapability.ImageOcr).ToString();
            semanticImageCapabilityResultText.Text = capabilities.GetIndexCapabilityStatus(IndexCapability.ImageSemantic).ToString();
        });
    }

    private async void LoadAppIndexCapabilities()
    {
        if (_indexer == null) return;

        IndexCapabilities capabilities = await Task.Run(() =>
        {
            return _indexer.GetIndexCapabilities();
        });

        DispatcherQueue.TryEnqueue(() =>
        {
            // Each status will be one of: Unknown, Initialized, Initializing, Suppressed, Unsupported, DisabledByPolicy, InitializationError
            // If status is Initialized, that capability is ready for use
            if (capabilities.GetCapabilityState(IndexCapability.TextLexical).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
            {
                indexLexicalCapabilityResultText.Text = "Available";
            }
            else
            {
                indexLexicalCapabilityResultText.Text = "Unavailable";
            }

            if (capabilities.GetCapabilityState(IndexCapability.TextSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
            {
                indexSemanticCapabilityResultText.Text = "Available";
            }
            else
            {
                indexSemanticCapabilityResultText.Text = "Unavailable";
            }

            if (capabilities.GetCapabilityState(IndexCapability.ImageSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
            {
                indexOCRCapabilityResultText.Text = "Available";
            }
            else
            {
                indexOCRCapabilityResultText.Text = "Unavailable";
            }

            if (capabilities.GetCapabilityState(IndexCapability.ImageOcr).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
            {
                indexSemanticImageCapabilityResultText.Text = "Available";
            }
            else
            {
                indexSemanticImageCapabilityResultText.Text = "Unavailable";
            }
        });
    }

    private void Listener_IndexCapabilitiesChanged(AppContentIndexer indexer, IndexCapabilities statusResult)
    {
        LoadAppIndexCapabilities();
    }
}