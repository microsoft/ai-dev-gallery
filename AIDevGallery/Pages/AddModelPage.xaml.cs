// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AIDevGallery.Pages;

internal sealed partial class AddModelPage : Page
{
    private readonly ObservableCollection<Result> results = [];
    private CancellationTokenSource? cts;

    public AddModelPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        NavigatedToPageEvent.Log(nameof(AddModelPage));
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        SearchButton.IsEnabled = false;
        SearchButtonProgressBar.Visibility = Visibility.Visible;
        NoResultsPanel.Visibility = Visibility.Collapsed;
        SearchTextBox.IsEnabled = false;

        if (cts != null && !cts.IsCancellationRequested)
        {
            cts.Cancel();
        }

        cts = new CancellationTokenSource();

        await SearchModels(SearchTextBox.Text, cts.Token);

        if (results == null || results.Count <= 0)
        {
            NoResultsPanel.Visibility = Visibility.Visible;
        }

        SearchButton.IsEnabled = true;
        SearchButtonProgressBar.Visibility = Visibility.Collapsed;
        SearchTextBox.IsEnabled = true;
    }

    private async Task SearchModels(string query, CancellationToken cancellationToken)
    {
        this.results.Clear();
        SearchModelEvent.Log(query);

        var results = await HuggingFaceApi.FindModels(query);

        var resultCount = results!.Count;
        string announcement = $"{resultCount} search result{(resultCount == 1 ? string.Empty : 's')} found";

        NarratorHelper.Announce(SearchTextBox, announcement, "modelSearchActivityId");

        ActionBlock<(HFSearchResult Result, Sibling Config, string? ReadmeUrl)> actionBlock = null!;
        actionBlock = new ActionBlock<(HFSearchResult Result, Sibling Config, string? ReadmeUrl)>(
            async (item) =>
            {
                var (result, config, readmeUrl) = item;
                var configContents = await HuggingFaceApi.GetContentsOfTextFile(result.Id, config.RFilename);
                GenAIConfig? genAIConfig = null;
                try
                {
                    genAIConfig = JsonSerializer.Deserialize(configContents, SourceGenerationContext.Default.GenAIConfig);
                }
                catch (JsonException)
                {
                }

                if (genAIConfig != null &&
                    (
                        genAIConfig.Model.Decoder.SessionOptions.ProviderOptions.Length == 0 ||
                        genAIConfig.Model.Decoder.SessionOptions.ProviderOptions.Any(p => p.Dml != null))
                    )
                {
                    var pathComponents = config.RFilename.Split("/");
                    string modelPath = string.Empty;
                    if (pathComponents.Length > 1)
                    {
                        modelPath = string.Join("/", pathComponents.Take(pathComponents.Length - 1));
                    }

                    var modelUrl = $"https://huggingface.co/{result.Id}/tree/main/{modelPath}";

                    bool isDmlModel = genAIConfig.Model.Decoder.SessionOptions.ProviderOptions.Any(p => p.Dml != null);

                    var curratedModel = ModelTypeHelpers.ModelDetails.Values.Where(m => m.Url == modelUrl).FirstOrDefault();

                    var filesToDownload = await ModelInformationHelper.GetDownloadFilesFromHuggingFace(new HuggingFaceUrl(modelUrl));

                    var details = curratedModel ?? new ModelDetails()
                    {
                        Id = "useradded-languagemodel-" + Guid.NewGuid().ToString(),
                        Name = result.Id + " " + (isDmlModel ? "DML" : "CPU"),
                        Url = modelUrl,
                        Description = "TODO",
                        HardwareAccelerators = [isDmlModel ? HardwareAccelerator.DML : HardwareAccelerator.CPU],
                        IsUserAdded = true,
                        PromptTemplate = GetTemplateFromName(result.Id),
                        Size = filesToDownload.Sum(f => f.Size),
                        ReadmeUrl = readmeUrl != null ? $"https://huggingface.co/{result.Id}/blob/main/{readmeUrl}" : null
                    };

                    string? licenseKey = null;
                    if (result.Tags != null)
                    {
                        var licenseTag = result.Tags.Where(t => t.StartsWith("license:", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                        if (licenseTag != null)
                        {
                            licenseKey = licenseTag.Split(":").Last();
                        }
                    }

                    if (curratedModel == null)
                    {
                        details.License = licenseKey;
                    }

                    ResultState state = ResultState.NotDownloaded;

                    if (App.ModelCache.IsModelCached(details.Url))
                    {
                        state = ResultState.Downloaded;
                    }
                    else if (App.ModelCache.DownloadQueue.GetDownload(details.Url) != null)
                    {
                        state = ResultState.Downloading;
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        this.results.Add(new Result
                        {
                            Details = details,
                            SearchResult = result,
                            License = LicenseInfo.GetLicenseInfo(licenseKey),
                            State = state,
                            HFUrl = $"https://huggingface.co/{result.Id}"
                        });
                    });
                }

                if (actionBlock.InputCount == 0)
                {
                    actionBlock.Complete();
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            });

        foreach (var result in results)
        {
            if (result.Siblings == null)
            {
                continue;
            }

            var configs = result.Siblings.Where(r => r.RFilename.EndsWith("genai_config.json", StringComparison.InvariantCultureIgnoreCase));

            var readmeSiblings = result.Siblings.Where(r => r.RFilename.EndsWith("readme.md", StringComparison.InvariantCultureIgnoreCase));
            string? readmeUrl = null;

            if (readmeSiblings.Any())
            {
                readmeUrl = readmeSiblings.First().RFilename;
            }

            if (!configs.Any())
            {
                continue;
            }

            foreach (var config in configs)
            {
                actionBlock.Post((result, config, readmeUrl));
            }
        }

        // Happens if no search result has a genai_config.json
        if (actionBlock.InputCount == 0)
        {
            actionBlock.Complete();
        }

        await actionBlock.Completion;
    }

    private PromptTemplate? GetTemplateFromName(string name)
    {
        switch (name)
        {
            case string p when p.Contains("phi3") || p.Contains("phi-3"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Phi3];
            case string l when l.Contains("llama") || l.Contains("llama-3"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Llama3];
            case string m when m.Contains("mistral"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Mistral];
            default:
                return null;
        }
    }

    private async void DownloadModelClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;

        if (button!.DataContext is not Result result)
        {
            return;
        }

        DownloadSearchedModelEvent.Log(result.Details.Name);

        ModelNameTxt.Text = result.Details.Name;
        ModelLicenseLink.NavigateUri = new Uri(result.License.LicenseUrl ?? $"https://huggingface.co/{result.SearchResult.Id}");
        ModelLicenseLabel.Text = result.License.Name;

        if (result.Details.Compatibility.CompatibilityState != ModelCompatibilityState.Compatible)
        {
            WarningInfoBar.Message = result.Details.Compatibility.CompatibilityIssueDescription;
            WarningInfoBar.IsOpen = true;
        }

        AgreeCheckBox.IsChecked = false;

        var output = await DownloadDialog.ShowAsync();

        if (output == ContentDialogResult.Primary)
        {
            App.ModelCache.AddModelToDownloadQueue(result!.Details);
            result.State = ResultState.Downloading;
        }
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as HyperlinkButton;
        var result = button!.DataContext as Result;

        string url = result!.License.LicenseUrl ?? $"https://huggingface.co/{result.SearchResult.Id}";

        Process.Start(new ProcessStartInfo()
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void ViewModelDetails(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;

        if (button!.DataContext is Result result)
        {
            var curratedModels = ModelTypeHelpers.ModelDetails.Where(p => p.Value.Url == result.Details.Url).ToList();

            if (curratedModels.Count > 0)
            {
                var parentKey = ModelTypeHelpers.ParentMapping.FirstOrDefault(p => p.Value.Contains(curratedModels[0].Key));
                App.MainWindow.Navigate("models", parentKey.Key);
            }
            else
            {
                App.MainWindow.Navigate("models", result.Details);
            }
        }
    }

    internal static Visibility VisibleWhenNotDownloaded(ResultState state)
    {
        return state == ResultState.NotDownloaded ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static Visibility VisibleWhenDownloaded(ResultState state)
    {
        return state == ResultState.Downloaded ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static Visibility VisibleWhenDownloading(ResultState state)
    {
        return state == ResultState.Downloading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        this.Focus(FocusState.Programmatic);
    }
}

internal partial class Result : ObservableObject
{
    public required HFSearchResult SearchResult { get; init; }
    public required ModelDetails Details { get; init; }
    public required LicenseInfo License { get; init; }
    public required string HFUrl { get; init; }

    public bool IsModelDownloadable => Details.Compatibility.CompatibilityState != ModelCompatibilityState.NotCompatible;
    public Visibility VisibleWhenCompatibilityIssue => Details.Compatibility.CompatibilityState == ModelCompatibilityState.Compatible ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    public partial ResultState State { get; set; }
}

internal enum ResultState
{
    NotDownloaded,
    Downloading,
    Downloaded
}