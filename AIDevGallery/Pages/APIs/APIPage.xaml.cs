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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal sealed partial class APIPage : Page
{
    public ModelFamily? ModelFamily { get; set; }
    private ModelType? modelFamilyType;

    public APIPage()
    {
        this.InitializeComponent();
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
        }
        else if (e.Parameter is ModelType apiType && ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(apiType, out var apiDefinition))
        {
            // API
            modelFamilyType = apiType;

            ModelFamily = new ModelFamily
            {
                Id = apiDefinition.Id,
                ReadmeUrl = apiDefinition.ReadmeUrl,
                DocsUrl = apiDefinition.ReadmeUrl,
                Name = apiDefinition.Name,
            };

            if (!string.IsNullOrWhiteSpace(apiDefinition.SampleIdToShowInDocs))
            {
                var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == apiDefinition.SampleIdToShowInDocs);
                if (sample != null)
                {
                    _ = sampleContainer.LoadSampleAsync(sample, [ModelDetailsHelper.GetModelDetailsFromApiDefinition(apiType, apiDefinition)]);
                }
            }
            else
            {
                sampleContainerRoot.Visibility = Visibility.Collapsed;
            }

            WcrApiCodeSnippet.Snippets.TryGetValue(apiType, out var snippet);
            if (snippet != null)
            {
                CodeSampleTextBlock.Text = $"```csharp\r\n{snippet}\r\n```";
            }
            else
            {
                codeSampleRoot.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            throw new InvalidOperationException("Invalid navigation parameter");
        }

        if (ModelFamily != null && !string.IsNullOrWhiteSpace(ModelFamily.ReadmeUrl))
        {
            var loadReadme = LoadReadme(ModelFamily.ReadmeUrl);
            //CodeSampleTextBlock.Text = "```csharp\r\nusing Microsoft.Windows.AI.Generative; \r\n \r\nif (!LanguageModel.IsAvailable()) \r\n{ \r\n   var op = await LanguageModel.MakeAvailableAsync(); \r\n} \r\n \r\nusing LanguageModel languageModel = await LanguageModel.CreateAsync(); \r\n \r\nstring prompt = \"Provide the molecular formula for glucose.\"; \r\n \r\nvar result = await languageModel.GenerateResponseAsync(prompt); \r\n \r\nConsole.WriteLine(result.Response); \r\n```";
        }
        else
        {
            summaryGrid.Visibility = Visibility.Collapsed;
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

    private async Task LoadReadme(string url)
    {
        string readmeContents = await GithubApi.GetContentsOfTextFile(url);
        if (!string.IsNullOrWhiteSpace(readmeContents))
        {
            readmeContents = MarkdownHelper.PreprocessMarkdown(readmeContents);

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

    private void MarkdownTextBlock_LinkClicked(object sender, CommunityToolkit.WinUI.UI.Controls.LinkClickedEventArgs e)
    {
        string link = e.Link;

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
            App.MainWindow.Navigate("Samples", new SampleNavigationArgs(sample));
        }
    }
}