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
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Controls;

internal sealed partial class SampleContainer : UserControl
{
    public static readonly DependencyProperty DisclaimerHorizontalAlignmentProperty = DependencyProperty.Register(nameof(DisclaimerHorizontalAlignment), typeof(HorizontalAlignment), typeof(SampleContainer), new PropertyMetadata(defaultValue: HorizontalAlignment.Left));

    public HorizontalAlignment DisclaimerHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(DisclaimerHorizontalAlignmentProperty);
        set => SetValue(DisclaimerHorizontalAlignmentProperty, value);
    }

    public static readonly DependencyProperty FooterContentProperty = DependencyProperty.Register(nameof(FooterContent), typeof(object), typeof(SampleContainer), new PropertyMetadata(defaultValue: null));

    public object FooterContent
    {
        get => (object)GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public static readonly DependencyProperty ShowFooterProperty = DependencyProperty.Register(nameof(ShowFooter), typeof(bool), typeof(SampleContainer), new PropertyMetadata(defaultValue: false, OnShowFooterChanged));

    public bool ShowFooter
    {
        get => (bool)GetValue(ShowFooterProperty);
        set => SetValue(ShowFooterProperty, value);
    }

    public List<string> NugetPackageReferences
    {
        get { return (List<string>)GetValue(NugetPackageReferencesProperty); }
        set { SetValue(NugetPackageReferencesProperty, value); }
    }

    public static readonly DependencyProperty NugetPackageReferencesProperty =
        DependencyProperty.Register("NugetPackageReferences", typeof(List<string>), typeof(SampleContainer), new PropertyMetadata(null));

    private RichTextBlockFormatter codeFormatter;
    private Dictionary<string, string> codeFiles = new();
    private Sample? _sampleCache;
    private Dictionary<ModelType, ExpandedModelDetails>? _cachedModels;
    private List<ModelDetails>? _modelsCache;
    private WinMlSampleOptions? _currentWinMlSampleOptions;
    private CancellationTokenSource? _sampleLoadingCts;
    private TaskCompletionSource? _sampleLoadedCompletionSource;
    private double _codePaneWidth;
    private ModelType? _wcrApi;

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
        codeFormatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(ActualTheme));
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

    public async Task LoadSampleAsync(Sample? sample, List<ModelDetails>? models, WinMlSampleOptions? winMlSampleOptions = null)
    {
        if (sample == null)
        {
            this.Visibility = Visibility.Collapsed;
            return;
        }

        this.Visibility = Visibility.Visible;
        if (!LoadSampleMetadata(sample, models, winMlSampleOptions))
        {
            return;
        }

        SetFooterVisualStates();
        ShowDebugInfo(null);
        RenderCodeTabs(true);

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
            if (m.IsApi())
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

        _sampleLoadingCts = new CancellationTokenSource();
        var token = _sampleLoadingCts.Token;

        // if WCR API, check if model is downloaded
        foreach (var wcrApi in models.Where(m => m.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI)))
        {
            var apiType = ModelTypeHelpers.ApiDefinitionDetails.FirstOrDefault(md => md.Value.Id == wcrApi.Id).Key;

            try
            {
                var state = WcrApiHelpers.GetApiAvailability(apiType);
                if (state != AIFeatureReadyState.Ready && !WcrApiHelpers.IsModelReadyWorkaround.ContainsKey(apiType))
                {
                    modelDownloader.State = state switch
                    {
                        AIFeatureReadyState.NotReady => WcrApiDownloadState.NotStarted,
                        _ => WcrApiDownloadState.Error
                    };

                    modelDownloader.ErrorMessage = WcrApiHelpers.GetStringDescription(state);
                    modelDownloader.DownloadProgress = 0;
                    SampleFrame.Content = null;
                    _wcrApi = apiType;

                    VisualStateManager.GoToState(this, "WcrModelNeedsDownload", true);
                    if (!await modelDownloader.SetDownloadOperation(apiType, sample.Id, WcrApiHelpers.EnsureReadyFuncs[apiType]).WaitAsync(token))
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                VisualStateManager.GoToState(this, "WcrApiNotCompatible", true);
                SampleFrame.Content = null;
                return;
            }
        }

        // model available
        VisualStateManager.GoToState(this, "SampleLoading", true);
        SampleFrame.Content = null;

        _sampleLoadedCompletionSource = new TaskCompletionSource();
        BaseSampleNavigationParameters sampleNavigationParameters;

        if (cachedModelsPaths.Count == 1)
        {
            sampleNavigationParameters = new SampleNavigationParameters(
                sample.Id,
                models.First().Id,
                cachedModelsPaths.First(),
                models.First().HardwareAccelerators.First(),
                models.First().PromptTemplate?.ToLlmPromptTemplate(),
                _sampleLoadedCompletionSource,
                winMlSampleOptions,
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
                winMlSampleOptions,
                token);
        }

        NavigatedToSampleEvent.Log(sample.Name ?? string.Empty);
        SampleFrame.Navigate(sample.PageType, sampleNavigationParameters);

        try
        {
            await _sampleLoadedCompletionSource.Task.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _sampleLoadedCompletionSource = null;
            _sampleLoadingCts = null;
        }

        _sampleLoadedCompletionSource = null;
        _sampleLoadingCts = null;

        NavigatedToSampleLoadedEvent.Log(sample.Name ?? string.Empty);

        VisualStateManager.GoToState(this, "SampleLoaded", true);
    }

    public void ShowDebugInfo(string? contents)
    {
        if (string.IsNullOrEmpty(contents))
        {
            SampleDebugInfoButton.Visibility = Visibility.Collapsed;
            SampleDebugInfoButton.Text = string.Empty;
            SampleDebugInfoContent.Text = string.Empty;
            return;
        }

        SampleDebugInfoButton.Text = contents.Split('\n')[0];
        SampleDebugInfoContent.Text = contents;

        SampleDebugInfoButton.Visibility = Visibility.Visible;
    }

    [MemberNotNull(nameof(_sampleCache))]
    private bool LoadSampleMetadata(Sample sample, List<ModelDetails>? models, WinMlSampleOptions? winMlSampleOptions = null)
    {
        if (_sampleCache == sample &&
            _modelsCache != null &&
            models != null &&
            winMlSampleOptions == _currentWinMlSampleOptions)
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
        _currentWinMlSampleOptions = winMlSampleOptions;

        if (models != null)
        {
            _cachedModels = sample.GetCacheModelDetailsDictionary(models.ToArray(), _currentWinMlSampleOptions);

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

    private void RenderCodeTabs(bool force = false)
    {
        if (_sampleCache == null)
        {
            return;
        }

        if (CodeTabView.TabItems.Count > 0 && !force)
        {
            return;
        }

        codeFiles.Clear();
        CodeTabView.TabItems.Clear();

        if (!string.IsNullOrEmpty(_sampleCache.CSCode) && _cachedModels != null)
        {
            var modelInfos = _cachedModels.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value, $"@\"{kvp.Value.Path}\""));
            AddCodeTab("Sample.xaml.cs", _sampleCache.GetCleanCSCode(modelInfos));
        }

        if (!string.IsNullOrEmpty(_sampleCache.XAMLCode))
        {
            AddCodeTab("Sample.xaml", _sampleCache.XAMLCode);
        }

        if (_cachedModels != null)
        {
            foreach (var sharedCodeEnum in _sampleCache.GetAllSharedCode(_cachedModels))
            {
                string sharedCodeName = SharedCodeHelpers.GetName(sharedCodeEnum);
                string sharedCodeContent = SharedCodeHelpers.GetSource(sharedCodeEnum);

                AddCodeTab(sharedCodeName, sharedCodeContent);
            }
        }

        if (CodeTabView.TabItems.Count > 0)
        {
            CodeTabView.SelectedIndex = 0;
        }
    }

    private void AddCodeTab(string header, string code)
    {
        codeFiles.Add(header, code);
        CodeTabView.TabItems.Add(new TabViewItem()
        {
            Header = header,
            Tag = header,
            IsClosable = false,
        });
    }

    private void UserControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        codeFormatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(ActualTheme));
        RenderCode();
    }

    public void ShowCode()
    {
        RenderCodeTabs();

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
        var winMlSampleOptions = _currentWinMlSampleOptions;
        _modelsCache = null;
        _sampleCache = null;
        _currentWinMlSampleOptions = null;

        return LoadSampleAsync(sample, models, winMlSampleOptions);
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        if (_wcrApi == null)
        {
            return;
        }

        if (WcrApiHelpers.GetApiAvailability(_wcrApi.Value) != AIFeatureReadyState.Ready)
        {
            var op = WcrApiHelpers.EnsureReadyFuncs[_wcrApi.Value]();
            if (await modelDownloader.SetDownloadOperation(op))
            {
                // reload sample
                await ReloadSampleAsync();
            }
        }
        else
        {
            modelDownloader.State = WcrApiDownloadState.Downloaded;
            await ReloadSampleAsync();
        }
    }

    private void CodeTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderCode();
    }

    private void RenderCode()
    {
        var selectedTab = CodeTabView.SelectedItem as TabViewItem;

        if (selectedTab == null)
        {
            return;
        }

        var codeName = selectedTab?.Tag as string;
        var code = codeFiles[codeName!];

        var extension = codeName!.Split('.').LastOrDefault();

        CodeTextBlock.Blocks.Clear();
        codeFormatter.FormatRichTextBlock(code, Languages.FindById(extension) ?? Languages.CSharp, CodeTextBlock);

        var linesCount = code.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
        StringBuilder lineNumbers = new StringBuilder();
        for (int i = 1; i <= linesCount; i++)
        {
            lineNumbers.Append(i);
            lineNumbers.AppendLine();
        }

        LineNumbersTextBlock.Text = lineNumbers.ToString();
    }

    private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        LineNumbersScroller.ChangeView(null, e.NextView.VerticalOffset, null, true);
    }

    private static void OnShowFooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SampleContainer container)
        {
            container.SetFooterVisualStates();
        }
    }

    private void SetFooterVisualStates()
    {
        if (ShowFooter)
        {
            VisualStateManager.GoToState(this, "FooterVisible", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "FooterHidden", true);
        }
    }

    private void FooterGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate if the modelselectors collide with the export/code buttons
        if (FooterContent != null)
        {
            if ((AIContentWarningPanel.ActualWidth + FooterContentPresenter.ActualWidth) >= e.NewSize.Width)
            {
                VisualStateManager.GoToState(this, "WarningCollapsed", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "WarningVisible", true);
            }
        }
    }
}