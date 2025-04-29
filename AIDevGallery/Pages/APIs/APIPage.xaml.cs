// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using ColorCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal sealed partial class APIPage : Page
{
    public ModelFamily? ModelFamily { get; set; }
    private ModelType? modelFamilyType;
    private ModelDetails? modelDetails;
    private Sample? sample;
    private string? readmeContents;
    private string? codeSnippet;

    public APIPage()
    {
        this.InitializeComponent();
        this.ActualThemeChanged += APIPage_ActualThemeChanged;
    }

    private void APIPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (ModelFamily != null)
        {
            _ = LoadReadme(ModelFamily.ReadmeUrl);
            LoadCodeSnippet(codeSnippet);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MostRecentlyUsedItem mru)
        {
            var modelFamilyId = mru.ItemId;
        }
        else if (e.Parameter is ModelType modelType && ModelTypeHelpers.ModelFamilyDetails.TryGetValue(modelType, out var modelFamilyDetails))
        {
            modelFamilyType = modelType;
            ModelFamily = modelFamilyDetails;
            ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out modelDetails);
        }
        else if (e.Parameter is ModelDetails details)
        {
            ModelFamily = new ModelFamily
            {
                Id = details.Id,
                DocsUrl = details.ReadmeUrl ?? string.Empty,
                ReadmeUrl = details.ReadmeUrl ?? string.Empty,
                Name = details.Name
            };
            modelDetails = details;
        }
        else if (e.Parameter is ModelType apiType && ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(apiType, out var apiDefinition))
        {
            // API
            modelFamilyType = apiType;
            modelDetails = ModelDetailsHelper.GetModelDetailsFromApiDefinition(apiType, apiDefinition);

            ModelFamily = new ModelFamily
            {
                Id = apiDefinition.Id,
                ReadmeUrl = apiDefinition.ReadmeUrl,
                DocsUrl = apiDefinition.ReadmeUrl,
                Name = apiDefinition.Name,
            };

            if (!string.IsNullOrWhiteSpace(apiDefinition.SampleIdToShowInDocs))
            {
                sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == apiDefinition.SampleIdToShowInDocs);
                if (sample != null)
                {
                    _ = sampleContainer.LoadSampleAsync(sample, [modelDetails]);
                }
            }
            else
            {
                sampleContainerRoot.Visibility = Visibility.Collapsed;
            }

            WcrApiCodeSnippet.Snippets.TryGetValue(apiType, out var snippet);
            codeSnippet = snippet;
            LoadCodeSnippet(snippet);
        }
        else
        {
            throw new InvalidOperationException("Invalid navigation parameter");
        }

        if (ModelFamily != null && !string.IsNullOrWhiteSpace(ModelFamily.ReadmeUrl))
        {
            var loadReadme = LoadReadme(ModelFamily.ReadmeUrl);
        }
        else
        {
            DocumentationCard.Visibility = Visibility.Collapsed;
        }

        GetSamples();
    }

    private void GetSamples()
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

    private void LoadCodeSnippet(string? snippet)
    {
        CodeSampleTextBlock.Blocks.Clear();

        if (snippet != null)
        {
            var codeFormatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(ActualTheme));
            codeFormatter.FormatRichTextBlock(snippet, Languages.CSharp, CodeSampleTextBlock);
        }
        else
        {
            CodeCard.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadReadme(string url)
    {
        readmeProgressRing.IsActive = true;
        markdownTextBlock.Text = string.Empty;

        readmeContents = readmeContents ?? await GithubApi.GetContentsOfTextFile(url);
        if (!string.IsNullOrWhiteSpace(readmeContents))
        {
            readmeContents = MarkdownHelper.PreprocessMarkdown(readmeContents);

            markdownTextBlock.Config = MarkdownHelper.GetMarkdownConfig();
            markdownTextBlock.Text = readmeContents;
        }

        readmeProgressRing.IsActive = false;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelFamily == null || ModelFamily.Id == null)
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText($"aidevgallery://apis/{ModelFamily.Id}");
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void MarkdownTextBlock_OnLinkClicked(object sender, CommunityToolkit.Labs.WinUI.MarkdownTextBlock.LinkClickedEventArgs e)
    {
        string link = e.Url;

        if (!URLHelper.IsValidUrl(link))
        {
            link = URLHelper.FixWcrReadmeLink(link);
        }

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
            App.MainWindow.Navigate("Samples", new SampleNavigationArgs(sample, modelDetails));
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        BackgroundShadow.Receivers.Add(ShadowCastGrid);
    }

    private void ExportSampleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || sample == null || modelDetails == null)
        {
            return;
        }

        _ = Generator.AskGenerateAndOpenAsync(sample, [modelDetails], this.XamlRoot);
    }
}