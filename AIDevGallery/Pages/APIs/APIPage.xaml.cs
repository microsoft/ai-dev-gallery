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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal sealed partial class APIPage : Page
{

    private const string DocsBaseUrl = "https://learn.microsoft.com/";
    private const string WcrDocsRelativePath = "/windows/ai/apis/";
    public ModelFamily? ModelFamily { get; set; }
    private ModelType? modelFamilyType;
    public bool IsNotApi => !modelFamilyType.HasValue || !ModelTypeHelpers.ApiDefinitionDetails.ContainsKey(modelFamilyType.Value);

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
            summaryGrid.Visibility = Visibility.Collapsed;
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

        if (!string.IsNullOrWhiteSpace(readmeContents))
        {
            readmeContents = PreprocessMarkdown(readmeContents);

            markdownTextBlock.Text = readmeContents;
        }

        readmeProgressRing.IsActive = false;
    }

    private string PreprocessMarkdown(string markdown)
    {
        markdown = Regex.Replace(markdown, @"\A---\n[\s\S]*?---\n", string.Empty, RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]", "> **ℹ️ Important:**", RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!NOTE\]", "> **❗ Note:**", RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!TIP\]", "> **💡 Tip:**", RegexOptions.Multiline);

        return markdown;
    }

    private IEnumerable<ModelDetails> GetAllSampleDetails()
    {
        if (!modelFamilyType.HasValue || !ModelTypeHelpers.ParentMapping.TryGetValue(modelFamilyType.Value, out List<ModelType>? modelTypes))
        {
            yield break;
        }

        if (modelTypes.Count == 0)
        {
            // Its an API
            modelTypes = [modelFamilyType.Value];
        }

        foreach (var modelType in modelTypes)
        {
            if (ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out var modelDetails))
            {
                yield return modelDetails;
            }
            else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(modelType, out var apiDefinition))
            {
                yield return ModelDetailsHelper.GetModelDetailsFromApiDefinition(modelType, apiDefinition);
            }
        }
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

        if (!IsNotApi && !IsValidUrl(link))
        {
            link = FixWcrReadmeLink(link);
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
            //var availableModel = modelSelectionControl.DownloadedModels.FirstOrDefault();
           // App.MainWindow.Navigate("Samples", new SampleNavigationArgs(sample, availableModel));
        }
    }

    private bool IsValidUrl(string url)
    {
        Uri uri;
        return Uri.TryCreate(url, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private string FixWcrReadmeLink(string link)
    {
        string fixedLink;

        if (link.StartsWith('/'))
        {
            fixedLink = Path.Join(DocsBaseUrl, link);
        }
        else
        {
            fixedLink = Path.Join(DocsBaseUrl, WcrDocsRelativePath, link.Replace(".md", string.Empty));
        }

        return fixedLink;
    }
}