// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Microsoft.Windows.AI.Text.Experimental;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.WCRAPIs;
[GallerySample(
    Name = "Generate with Phi Silica with Adapter",
    Model1Types = [ModelType.PhiSilicaLora],
    Id = "3e392b7f-02a8-45e0-bed1-f75186368f12",
    Scenario = ScenarioType.TextLoRAAdapters,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]
internal sealed partial class PhiSilicaLoRa : BaseSamplePage
{
    internal enum GenerationType
    {
        All,
        With,
        Without
    }

    private const int MaxLength = 1000;
    private bool _isProgressVisible;
    private LanguageModel? _languageModel;
    private LanguageModelExperimental? _loraModel;
    private CancellationTokenSource? _cts;
    private IAsyncOperationWithProgress<LanguageModelResponseResult, string>? operation;
    private string _adapterFilePath = string.Empty;
    private string _systemPrompt = string.Empty;
    private GenerationType _generationType = GenerationType.All;

    public PhiSilicaLoRa()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = LanguageModel.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await LanguageModel.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, $"Phi-Silica is not available");
                }
            }

            _languageModel = await LanguageModel.CreateAsync();
            _loraModel = new LanguageModelExperimental(_languageModel);
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"Phi-Silica is not available: {msg}");
        }

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        GenerateButton.Focus(FocusState.Programmatic);
        _adapterFilePath = App.AppData.LastAdapterPath; // <exclude-line>
        if (!string.IsNullOrWhiteSpace(_adapterFilePath))
        {
            AdapterHyperLink.Content = Path.GetFileName(_adapterFilePath);
            GenerateButton.IsEnabled = true;
            ExampleAdapterLink.Visibility = Visibility.Collapsed;
        }
        else
        {
            GenerateButton.IsEnabled = false;
        }

        _systemPrompt = App.AppData.LastSystemPrompt; // <exclude-line>
        if (!string.IsNullOrWhiteSpace(_systemPrompt))
        {
            SystemPromptBox.Text = _systemPrompt;
        }
    }

    // </exclude>
    private async void CleanUp()
    {
        CancelGeneration();
        App.AppData.LastSystemPrompt = SystemPromptBox.Text; // <exclude-line>
        App.AppData.LastAdapterPath = _adapterFilePath; // <exclude-line>
        await App.AppData.SaveAsync(); // <exclude-line>
        _languageModel?.Dispose();
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set
        {
            _isProgressVisible = value;
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopIcon.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    public async Task GenerateText(string prompt, TextBlock textBlock, LanguageModelContext? context, LanguageModelOptionsExperimental? options = null)
    {
        if (_languageModel == null || _loraModel == null)
        {
            ShowException(null, "Phi-Silica is not available.");
            return;
        }

        textBlock.Text = string.Empty;
        OutputGrid.Visibility = Visibility.Visible;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        operation = context == null ?
            options == null ? _languageModel.GenerateResponseAsync(prompt) : _loraModel.GenerateResponseAsync(prompt, options) :
            options == null ? _languageModel.GenerateResponseAsync(context, prompt, new LanguageModelOptions()) : _loraModel.GenerateResponseAsync(context, prompt, options);

        if (operation == null)
        {
            NarratorHelper.Announce(InputTextBox, "Error generating content.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
            return;
        }

        operation.Progress = (asyncInfo, delta) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // <exclude>
                if (!contentStartedBeingGenerated)
                {
                    NarratorHelper.Announce(InputTextBox, "Content has started generating.", "GeneratedAnnouncementActivityId");
                    contentStartedBeingGenerated = true;
                }

                // </exclude>
                if (_isProgressVisible)
                {
                    StopBtn.Visibility = Visibility.Visible;
                    IsProgressVisible = false;
                }

                // This isn't working in this version for LoRa
                textBlock.Text += delta;
                if (_cts?.IsCancellationRequested == true)
                {
                    operation.Cancel();
                }
            });
        };

        var result = await operation;
        textBlock.Text = result.Text;

        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
    }

    private async Task RunQuery()
    {
        if (!File.Exists(_adapterFilePath))
        {
            ShowException(null, "Phi-Silica Lora adapter not found.");
            return;
        }

        if (_languageModel == null)
        {
            ShowException(null, "Phi-Silica is not available.");
            return;
        }

        if (this.InputTextBox.Text.Length > 0 && _loraModel != null)
        {
            LanguageModelContext? context = null;
            if (SystemPromptBox.Text.Length > 0)
            {
                context = _languageModel.CreateContext(SystemPromptBox.Text);
            }

            LowRankAdaptation? loraAdapter;
            try
            {
                 loraAdapter = _loraModel.LoadAdapter(_adapterFilePath);
            }
            catch (Exception ex)
            {
                ShowException(ex);
                return;
            }

            var options = new LanguageModelOptionsExperimental
            {
                LoraAdapter = loraAdapter
            };

            GenerateButton.Visibility = Visibility.Collapsed;
            StopBtn.Visibility = Visibility.Visible;
            InputTextBox.IsEnabled = false;
            SystemPromptBox.IsEnabled = false;
            IsProgressVisible = true;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                switch (_generationType)
                {
                    case GenerationType.All:
                        await Task.WhenAll(
                            GenerateText(InputTextBox.Text, LoraTxt, context, options),
                            GenerateText(InputTextBox.Text, NoLoraTxt, context));
                        break;
                    case GenerationType.With:
                        await GenerateText(InputTextBox.Text, LoraTxt, context, options);
                        break;
                    case GenerationType.Without:
                        await GenerateText(InputTextBox.Text, NoLoraTxt, context);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException))
                {
                    ShowException(ex);
                }
            }

            StopBtn.Visibility = Visibility.Collapsed;
            GenerateButton.Visibility = Visibility.Visible;
            InputTextBox.IsEnabled = true;
            SystemPromptBox.IsEnabled = true;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            await RunQuery();
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Cancel();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGeneration();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            if (inputLength >= MaxLength)
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}";
            }

            GenerateButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }

    private async void AdapterHyperLink_Click(object sender, RoutedEventArgs e)
    {
        /* if (File.Exists(_adapterFilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{_adapterFilePath}\"");
        } */

        var window = new Window();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".safetensors");
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _adapterFilePath = file.Path;
            App.AppData.LastAdapterPath = _adapterFilePath; // <exclude-line>
            await App.AppData.SaveAsync(); // <exclude-line>
            AdapterHyperLink.Content = Path.GetFileName(_adapterFilePath);
            GenerateButton.IsEnabled = !string.IsNullOrWhiteSpace(_adapterFilePath);
            ExampleAdapterLink.Visibility = string.IsNullOrWhiteSpace(_adapterFilePath) ? Visibility.Visible : Visibility.Collapsed;

        }
    }

    private async void GenerateAll_Click(object sender, RoutedEventArgs e)
    {
        _generationType = GenerationType.All;
        await RunQuery();
    }

    private async void GenerateWith_Click(object sender, RoutedEventArgs e)
    {
        _generationType = GenerationType.With;
        await RunQuery();
    }

    private async void GenerateWithout_Click(object sender, RoutedEventArgs e)
    {
        _generationType = GenerationType.Without;
        await RunQuery();
    }
}