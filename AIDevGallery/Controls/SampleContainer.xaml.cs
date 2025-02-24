// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using ColorCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;

internal sealed partial class SampleContainer : UserControl
{
    public static readonly DependencyProperty DisclaimerHorizontalAlignmentProperty = DependencyProperty.Register(nameof(DisclaimerHorizontalAlignment), typeof(HorizontalAlignment), typeof(SampleContainer), new PropertyMetadata(defaultValue: HorizontalAlignment.Center));

    public HorizontalAlignment DisclaimerHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(DisclaimerHorizontalAlignmentProperty);
        set => SetValue(DisclaimerHorizontalAlignmentProperty, value);
    }

    public List<string> NugetPackageReferences
    {
        get { return (List<string>)GetValue(NugetPackageReferencesProperty); }
        set { SetValue(NugetPackageReferencesProperty, value); }
    }

    public static readonly DependencyProperty NugetPackageReferencesProperty =
        DependencyProperty.Register("NugetPackageReferences", typeof(List<string>), typeof(SampleContainer), new PropertyMetadata(null));

    private Sample? _sampleCache;
    private Dictionary<ModelType, ExpandedModelDetails>? _cachedModels;
    private List<ModelDetails>? _modelsCache;
    private CancellationTokenSource? _sampleLoadingCts;
    private TaskCompletionSource? _sampleLoadedCompletionSource;
    private double _codePaneWidth;
    private ModelType _wcrApi;

    private static readonly List<WeakReference<SampleContainer>> References = [];

    internal static bool AnySamplesLoading()
    {
        return References.Any(r => r.TryGetTarget(out var sampleContainer) && sampleContainer._sampleLoadedCompletionSource != null);
    }

    internal static async Task WaitUnloadAllAsync()
    {
        foreach (var reference in References)
        {
            if (reference.TryGetTarget(out var sampleContainer))
            {
                sampleContainer.CancelCTS();
                if (sampleContainer._sampleLoadedCompletionSource != null)
                {
                    try
                    {
                        await sampleContainer._sampleLoadedCompletionSource.Task;
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        sampleContainer._sampleLoadedCompletionSource = null;
                    }
                }
            }
        }

        References.Clear();
    }

    private void CancelCTS()
    {
        if (_sampleLoadingCts != null)
        {
            _sampleLoadingCts.Cancel();
            _sampleLoadingCts = null;
        }
    }

    public SampleContainer()
    {
        this.InitializeComponent();
        References.Add(new WeakReference<SampleContainer>(this));
        this.Unloaded += (sender, args) =>
        {
            CancelCTS();
            var reference = References.FirstOrDefault(r => r.TryGetTarget(out var sampleContainer) && sampleContainer == this);
            if (reference != null)
            {
                References.Remove(reference);
            }
        };
    }

    public async Task LoadSampleAsync(Sample? sample, List<ModelDetails>? models)
    {
        if (sample == null)
        {
            this.Visibility = Visibility.Collapsed;
            return;
        }

        this.Visibility = Visibility.Visible;
        if (!LoadSampleMetadata(sample, models))
        {
            return;
        }

        CancelCTS();

        if (models == null)
        {
            NavigatedToSampleEvent.Log(sample.Name ?? string.Empty);
            SampleFrame.Navigate(sample.PageType);
            VisualStateManager.GoToState(this, "SampleLoaded", true);
            return;
        }

        if (models == null || models.Count == 0)
        {
            VisualStateManager.GoToState(this, "Disabled", true);
            SampleFrame.Content = null;
            return;
        }

        var cachedModelsPaths = models.Select(m =>
        {
            // If it is an API, use the URL just to count
            if (m.Size == 0)
            {
                return m.Url;
            }

            return App.ModelCache.GetCachedModel(m.Url)?.Path;
        })
            .Where(cm => cm != null)
            .Select(cm => cm!)
            .ToList();

        if (cachedModelsPaths == null || cachedModelsPaths.Count != models.Count)
        {
            VisualStateManager.GoToState(this, "Disabled", true);
            SampleFrame.Content = null;
            return;
        }

        // show that models are not compatible with this device
        if (models.Any(m => m.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI) && m.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible))
        {
            VisualStateManager.GoToState(this, "WcrApiNotCompatible", true);
            SampleFrame.Content = null;
            return;
        }

        // if WCR API, check if model is downloaded
        var wcrApis = models.Where(m => m.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI)).ToList();
        if (wcrApis.Count > 0)
        {
            var index = 0;

            do
            {
                var wcrApi = wcrApis[index];
                var apiType = ModelTypeHelpers.ApiDefinitionDetails.FirstOrDefault(md => md.Value.Id == wcrApi.Id).Key;

                try
                {
                    if (WcrApiHelpers.GetApiAvailability(apiType) != WcrApiAvailability.Available)
                    {
                        modelDownloader.State = WcrApiDownloadState.NotStarted;
                        modelDownloader.ErrorMessage = string.Empty;
                        modelDownloader.DownloadProgress = 0;
                        SampleFrame.Content = null;
                        _wcrApi = apiType;

                        VisualStateManager.GoToState(this, "WcrModelNeedsDownload", true);
                        if (!await modelDownloader.SetDownloadOperation(apiType, sample.Id, WcrApiHelpers.MakeAvailables[apiType]))
                        {
                            return;
                        }
                    }
                }
                catch
                {
                    VisualStateManager.GoToState(this, "WcrApiNotCompatible", true);
                    SampleFrame.Content = null;
                    return;
                }

                ++index;
            }
            while (index < wcrApis.Count);
        }

        // model available
        VisualStateManager.GoToState(this, "SampleLoading", true);
        SampleFrame.Content = null;

        _sampleLoadingCts = new CancellationTokenSource();
        _sampleLoadedCompletionSource = new TaskCompletionSource();
        BaseSampleNavigationParameters sampleNavigationParameters;

        var modelPath = cachedModelsPaths.First();
        var token = _sampleLoadingCts.Token;

        if (cachedModelsPaths.Count == 1)
        {
            sampleNavigationParameters = new SampleNavigationParameters(
                sample.Id,
                models.First().Id,
                modelPath,
                models.First().HardwareAccelerators.First(),
                models.First().PromptTemplate?.ToLlmPromptTemplate(),
                _sampleLoadedCompletionSource,
                token);
        }
        else
        {
            var hardwareAccelerators = new List<HardwareAccelerator>();
            var promptTemplates = new List<LlmPromptTemplate?>();
            foreach (var model in models)
            {
                hardwareAccelerators.Add(model.HardwareAccelerators.First());
                promptTemplates.Add(model.PromptTemplate?.ToLlmPromptTemplate());
            }

            sampleNavigationParameters = new MultiModelSampleNavigationParameters(
                sample.Id,
                models.Select(m => m.Id).ToArray(),
                [.. cachedModelsPaths],
                [.. hardwareAccelerators],
                [.. promptTemplates],
                _sampleLoadedCompletionSource,
                token);
        }

        NavigatedToSampleEvent.Log(sample.Name ?? string.Empty);
        SampleFrame.Navigate(sample.PageType, sampleNavigationParameters);

        await _sampleLoadedCompletionSource.Task;

        _sampleLoadedCompletionSource = null;
        _sampleLoadingCts = null;

        NavigatedToSampleLoadedEvent.Log(sample.Name ?? string.Empty);

        VisualStateManager.GoToState(this, "SampleLoaded", true);

        CodePivot.Items.Clear();

        RenderCode();
    }

    [MemberNotNull(nameof(_sampleCache))]
    private bool LoadSampleMetadata(Sample sample, List<ModelDetails>? models)
    {
        if (_sampleCache == sample &&
            _modelsCache != null &&
            models != null)
        {
            var modelsAreEqual = true;
            if (_modelsCache.Count != models.Count)
            {
                modelsAreEqual = false;
            }
            else
            {
                for (int i = 0; i < models.Count; i++)
                {
                    ModelDetails? model = models[i];
                    if (!_modelsCache[i].Id.Equals(model.Id, StringComparison.Ordinal) ||
                        !_modelsCache[i].HardwareAccelerators.SequenceEqual(model.HardwareAccelerators))
                    {
                        modelsAreEqual = false;
                    }
                }
            }

            if (modelsAreEqual)
            {
                return false;
            }
        }

        _sampleCache = sample;

        if (models != null)
        {
            _cachedModels = sample.GetCacheModelDetailsDictionary(models.ToArray());

            if (_cachedModels != null)
            {
                NugetPackageReferences = sample.GetAllNugetPackageReferences(_cachedModels);
                _modelsCache = models;
            }
        }

        if (sample == null)
        {
            Visibility = Visibility.Collapsed;
        }

        return true;
    }

    private void RenderCode(bool force = false)
    {
        var codeFormatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(ActualTheme));

        if (_sampleCache == null)
        {
            return;
        }

        if (CodePivot.Items.Count > 0 && !force)
        {
            return;
        }

        CodePivot.Items.Clear();

        if (!string.IsNullOrEmpty(_sampleCache.CSCode))
        {
            CodePivot.Items.Add(CreateCodeBlock(codeFormatter, "Sample.xaml.cs", _sampleCache.CSCode, Languages.CSharp));
        }

        if (!string.IsNullOrEmpty(_sampleCache.XAMLCode))
        {
            CodePivot.Items.Add(CreateCodeBlock(codeFormatter, "Sample.xaml", _sampleCache.XAMLCode, Languages.FindById("xaml")));
        }

        if (_cachedModels != null)
        {
            foreach (var sharedCodeEnum in _sampleCache.GetAllSharedCode(_cachedModels))
            {
                string sharedCodeName = Samples.SharedCodeHelpers.GetName(sharedCodeEnum);
                string sharedCodeContent = Samples.SharedCodeHelpers.GetSource(sharedCodeEnum);

                CodePivot.Items.Add(CreateCodeBlock(codeFormatter, sharedCodeName, sharedCodeContent, Languages.CSharp));
            }
        }
    }

    private PivotItem CreateCodeBlock(RichTextBlockFormatter codeFormatter, string header, string code, ILanguage language)
    {
        var textBlock = new RichTextBlock()
        {
            Margin = new Thickness(0, 12, 0, 12),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 14,
            IsTextSelectionEnabled = true
        };

        codeFormatter.FormatRichTextBlock(code, language, textBlock);

        PivotItem item = new()
        {
            Header = header,
            Content = new ScrollViewer()
            {
                HorizontalScrollMode = ScrollMode.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollMode = ScrollMode.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                Content = textBlock,
                Padding = new Thickness(0, 0, 16, 16)
            }
        };
        AutomationProperties.SetName(item, header);
        return item;
    }

    private void UserControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        RenderCode(true);
    }

    public void ShowCode()
    {
        RenderCode();

        CodeColumn.Width = _codePaneWidth == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(_codePaneWidth);
        VisualStateManager.GoToState(this, "ShowCodePane", true);
    }

    public void HideCode()
    {
        _codePaneWidth = CodeColumn.ActualWidth;
        VisualStateManager.GoToState(this, "HideCodePane", true);
    }

    private async void NuGetPackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton button && button.Tag is string url)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.nuget.org/packages/" + url));
        }
    }

    private Task ReloadSampleAsync()
    {
        var models = _modelsCache;
        var sample = _sampleCache;
        _modelsCache = null;
        _sampleCache = null;

        return LoadSampleAsync(sample, models);
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        if (WcrApiHelpers.GetApiAvailability(_wcrApi) != WcrApiAvailability.Available)
        {
            var op = WcrApiHelpers.MakeAvailables[_wcrApi]();
            if (await modelDownloader.SetDownloadOperation(op))
            {
                // reload sample
                _ = ReloadSampleAsync();
            }
        }
        else
        {
            modelDownloader.State = WcrApiDownloadState.Downloaded;
            _ = ReloadSampleAsync();
        }
    }
}