// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal sealed partial class ModelPage : Page
{
    public ModelFamily? ModelFamily { get; set; }
    private ModelType? modelFamilyType;
    private string? readme;

    public ModelPage()
    {
        this.InitializeComponent();
        this.Unloaded += ModelPage_Unloaded;
        this.ActualThemeChanged += APIPage_ActualThemeChanged;
    }

    private void APIPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (ModelFamily != null)
        {
            RenderReadme(readme);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        bool isLocalModel = false;

        if (e.Parameter is MostRecentlyUsedItem mru)
        {
            var modelFamilyId = mru.ItemId;
        }
        else if (e.Parameter is ModelType modelType && ModelTypeHelpers.ModelFamilyDetails.TryGetValue(modelType, out var modelFamilyDetails))
        {
            modelFamilyType = modelType;
            ModelFamily = modelFamilyDetails;

            modelSelectionControl.SetModels(GetAllSampleDetails().ToList());
        }
        else if (e.Parameter is ModelDetails details)
        {
            // this is likely user added model
            modelSelectionControl.SetModels([details]);

            ModelFamily = new ModelFamily
            {
                Id = details.Id,
                DocsUrl = details.ReadmeUrl ?? string.Empty,
                ReadmeUrl = details.ReadmeUrl ?? string.Empty,
                Name = details.Name
            };

            isLocalModel = details.Url.StartsWith("local", StringComparison.InvariantCultureIgnoreCase);
        }
        else
        {
            throw new InvalidOperationException("Invalid navigation parameter");
        }

        if (ModelFamily != null && !string.IsNullOrWhiteSpace(ModelFamily.ReadmeUrl))
        {
            var loadReadme = LoadReadme(ModelFamily.ReadmeUrl);
        }
        else if (isLocalModel)
        {
            markdownTextBlock.Text = "This model was added by you.";
            readmeProgressRing.IsActive = false;
        }
        else
        {
            DocumentationCard.Visibility = Visibility.Collapsed;
        }

        EnableSampleListIfModelIsDownloaded();
        App.ModelCache.CacheStore.ModelsChanged += CacheStore_ModelsChanged;
    }

    private void ModelPage_Unloaded(object sender, RoutedEventArgs e)
    {
        App.ModelCache.CacheStore.ModelsChanged -= CacheStore_ModelsChanged;
    }

    private void CacheStore_ModelsChanged(ModelCacheStore sender)
    {
        EnableSampleListIfModelIsDownloaded();
    }

    private void EnableSampleListIfModelIsDownloaded()
    {
        if (modelSelectionControl.Models != null && modelSelectionControl.Models.Count > 0)
        {
            foreach (var model in modelSelectionControl.Models)
            {
                if (App.ModelCache.GetCachedModel(model.Url) != null || model.Size == 0)
                {
                    SampleList.IsEnabled = true;
                }
            }
        }
    }

    private async Task LoadReadme(string url)
    {
        string readmeContents = string.Empty;

        if (url.StartsWith("https://github.com", StringComparison.InvariantCultureIgnoreCase))
        {
            readmeContents = await GithubApi.GetContentsOfTextFile(url);
        }
        else if (url.StartsWith("https://huggingface.co", StringComparison.InvariantCultureIgnoreCase))
        {
            readmeContents = await HuggingFaceApi.GetContentsOfTextFile(url);
        }

        readme = readmeContents;
        RenderReadme(readmeContents);
    }

    private void RenderReadme(string? readmeContents)
    {
        markdownTextBlock.Text = string.Empty;

        if (!string.IsNullOrWhiteSpace(readmeContents))
        {
            readmeContents = MarkdownHelper.PreprocessMarkdown(readmeContents);
            markdownTextBlock.Config = MarkdownHelper.GetMarkdownConfig();
            markdownTextBlock.Text = readmeContents;
        }

        readmeProgressRing.IsActive = false;
    }

    private IEnumerable<ModelDetails> GetAllSampleDetails()
    {
        if (!modelFamilyType.HasValue || !ModelTypeHelpers.ParentMapping.TryGetValue(modelFamilyType.Value, out List<ModelType>? modelTypes))
        {
            yield break;
        }

        foreach (var modelType in modelTypes)
        {
            if (ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out var modelDetails))
            {
                yield return modelDetails;
            }
        }
    }

    private void ModelSelectionControl_SelectedModelChanged(object sender, ModelDetails? modelDetails)
    {
        // if we don't have a modelType, we are in a user added language model, use same samples as Phi
        var modelType = modelFamilyType ?? ModelType.Phi3Mini;

        var samples = SampleDetails.Samples.Where(s => s.Model1Types.Contains(modelType) || s.Model2Types?.Contains(modelType) == true).ToList();
        if (ModelTypeHelpers.ParentMapping.Values.Any(parent => parent.Contains(modelType)))
        {
            var parent = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(modelType)).Key;
            samples.AddRange(SampleDetails.Samples.Where(s => s.Model1Types.Contains(parent) || s.Model2Types?.Contains(parent) == true));
        }

        SampleList.ItemsSource = samples;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelFamily == null || ModelFamily.Id == null)
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText($"aidevgallery://models/{ModelFamily.Id}");
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void MarkdownTextBlock_OnLinkClicked(object sender, CommunityToolkit.Labs.WinUI.MarkdownTextBlock.LinkClickedEventArgs e)
    {
        string link = e.Url;

        ModelDetailsLinkClickedEvent.Log(link);
        Process.Start(new ProcessStartInfo()
        {
            FileName = link,
            UseShellExecute = true
        });
    }

    private void SampleList_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is Sample sample)
        {
            var availableModel = modelSelectionControl.DownloadedModels.FirstOrDefault();
            App.MainWindow.Navigate("Samples", new SampleNavigationArgs(sample, availableModel));
        }
    }

    public static Uri GetSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new Uri("https://aka.ms/ai-dev-gallery-repo");
        }

        return new Uri(url);
    }
}