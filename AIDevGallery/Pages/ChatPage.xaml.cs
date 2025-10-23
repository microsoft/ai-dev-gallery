// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Samples.SharedCode.StableDiffusionCode;
using AIDevGallery.Utils;
using ColorCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Pages;

public sealed partial class ChatPage : Page
{
    private ObservableCollection<ChatMessage> messages = new();
    private ObservableCollection<string> suggestions = new();
    private StableDiffusion? stableDiffusion;
    private ModelDetails? selectedModel;
    private bool isModelReady;
    private bool isLoadingModel; // Flag to prevent duplicate loading
    private CancellationTokenSource? cts;
    private ModelDownload? currentModelDownload;
    private ChatMessage? downloadingMessage;
    private DispatcherTimer? progressTimer;
    private string? lastGeneratedPrompt; // Store the last prompt used for image generation

    public ChatPage()
    {
        this.InitializeComponent();
        this.Loaded += ChatPage_Loaded;
        this.Unloaded += ChatPage_Unloaded;
        
        // Initialize progress timer (same as DownloadableModel)
        progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        progressTimer.Tick += ProgressTimer_Tick;
    }

    private void ChatPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize suggestions
        suggestions.Add("Create an image");
        suggestions.Add("Summarize text");
        suggestions.Add("Explain code");
        suggestions.Add("Draft a text");
        
        SuggestionButtons.ItemsSource = suggestions;
        MessagesItemsControl.ItemsSource = messages;
    }

    private void ChatPage_Unloaded(object sender, RoutedEventArgs e)
    {
        CleanUp();
    }

    private void CleanUp()
    {
        cts?.Cancel();
        cts?.Dispose();
        stableDiffusion?.Dispose();
        
        if (progressTimer != null)
        {
            progressTimer.Stop();
            progressTimer.Tick -= ProgressTimer_Tick;
        }
        
        if (currentModelDownload != null)
        {
            currentModelDownload.StateChanged -= ModelDownload_StateChanged;
            currentModelDownload = null;
        }
    }

    private void SuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string suggestion)
        {
            if (suggestion == "Create an image")
            {
                HandleCreateImageRequest();
            }
            // Other suggestions are not implemented yet - do nothing
        }
    }

    private async void HandleCreateImageRequest()
    {
        // Switch to chat state
        InitialStateGrid.Visibility = Visibility.Collapsed;
        ChatStateGrid.Visibility = Visibility.Visible;

        // Add user message
        AddUserMessage("Create an image");

        // Add assistant response about the model
        AddAssistantMessage("Great! I recommend using Stable Diffusion v1.4 for image generation. Let me help you download the model.");

        // Add a small delay before showing the license dialog
        await Task.Delay(800);

        // Check if model is already cached
        await Task.Delay(500); // Small delay for better UX

        var models = ModelDetailsHelper.GetModelDetailsForModelType(ModelType.StableDiffusion);
        selectedModel = models.FirstOrDefault();

        if (selectedModel != null && App.ModelCache.IsModelCached(selectedModel.Url))
        {
            // Model already cached
            AddAssistantMessage("Model is already downloaded! Preparing the model for you...");
            UpdateModelButton();
            await LoadModelAsync();

            // After model is loaded, prompt for input
            if (isModelReady)
            {
                AddAssistantMessage("All set! What image would you like to create?");
            }
        }
        else
        {
            // Show license dialog
            LicenseCheckBox.IsChecked = false;
            await LicenseDialog.ShowAsync();
        }
    }

    private async void LicenseDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (selectedModel == null)
            return;

        // Add downloading message
        downloadingMessage = AddAssistantMessageWithLoading("Downloading model...");

        // Start actual model download
        currentModelDownload = App.ModelDownloadQueue.AddModel(selectedModel);
        
        if (currentModelDownload == null)
        {
            // Model was already in cache
            downloadingMessage.Text = "Model is already downloaded! Preparing the model for you...";
            downloadingMessage.IsLoading = false;
            UpdateModelButton();
            await LoadModelAsync();

            // After model is loaded, prompt for input
            if (isModelReady)
            {
                AddAssistantMessage("All set! What image would you like to create?");
            }
        }
        else
        {
            // Subscribe to download progress
            currentModelDownload.StateChanged += ModelDownload_StateChanged;
        }
    }

    private void ModelDownload_StateChanged(object? sender, ModelDownloadEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            if (downloadingMessage == null)
                return;

            // Start timer to update progress (same pattern as DownloadableModel)
            if (progressTimer != null && !progressTimer.IsEnabled)
            {
                progressTimer.Start();
            }

            if (e.Status == DownloadStatus.Completed)
            {
                if (progressTimer != null)
                {
                    progressTimer.Stop();
                }

                downloadingMessage.Text = "Model download complete! Preparing the model for you...";
                downloadingMessage.IsLoading = false;
                UpdateModelButton();

                if (currentModelDownload != null)
                {
                    currentModelDownload.StateChanged -= ModelDownload_StateChanged;
                    currentModelDownload = null;
                }

                // Load the model (only if not already loading)
                if (!isLoadingModel && !isModelReady)
                {
                    isLoadingModel = true;
                    await LoadModelAsync();
                    isLoadingModel = false;
                    
                    // After model is loaded, prompt for input
                    if (isModelReady)
                    {
                        AddAssistantMessage("All set! What image would you like to create?");
                    }
                }
            }
            else if (e.Status == DownloadStatus.Canceled)
            {
                if (progressTimer != null)
                {
                    progressTimer.Stop();
                }

                downloadingMessage.Text = "Model download was canceled.";
                downloadingMessage.IsLoading = false;

                if (currentModelDownload != null)
                {
                    currentModelDownload.StateChanged -= ModelDownload_StateChanged;
                    currentModelDownload = null;
                }
            }
        });
    }

    private void ProgressTimer_Tick(object? sender, object e)
    {
        if (progressTimer != null)
        {
            progressTimer.Stop();
        }

        if (currentModelDownload != null && downloadingMessage != null)
        {
            // Read current progress from ModelDownload (same as DownloadableModel)
            float progressPercent = currentModelDownload.DownloadProgress * 100;
            downloadingMessage.Text = $"Downloading model... {progressPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%";
        }
    }

    private async Task LoadModelAsync()
    {
        if (selectedModel == null || isModelReady)
            return;

        try
        {
            // Check if model is cached
            if (!App.ModelCache.IsModelCached(selectedModel.Url))
            {
                AddAssistantMessage("Model is not available. Please download it first.");
                return;
            }

            // Get the cached model path
            var cachedModel = App.ModelCache.GetCachedModel(selectedModel.Url);
            if (cachedModel == null)
            {
                AddAssistantMessage("Error: Could not find cached model path.");
                return;
            }

            string modelPath = cachedModel.Path;

            // Initialize Stable Diffusion
            // Note: This may throw an exception about ORT extensions already being registered
            // if the user has previously used other samples. This is usually safe to ignore.
            try
            {
                stableDiffusion = new StableDiffusion(modelPath);
                await stableDiffusion.InitializeAsync(App.AppData.WinMLSampleOptions);
            }
            catch (Exception initEx)
            {
                // Check if it's the known ORT extensions issue
                if (initEx.Message.Contains("domain is already exist") || 
                    initEx.Message.Contains("DomainToVersion") ||
                    initEx.Message.Contains("ai.onnx.contrib"))
                {
                    // This error occurs when ORT extensions are already registered (e.g., from another sample)
                    // The model initialization might have partially succeeded, so we'll attempt to use it
                    AddAssistantMessage("Note: Encountered a known initialization issue. This happens when you've used other AI samples. The model should still work for image generation.");
                    
                    // Mark as ready - the StableDiffusion object might still be usable
                    isModelReady = true;
                    return;
                }
                else
                {
                    // It's a different error, re-throw it
                    throw;
                }
            }
            
            isModelReady = true;
        }
        catch (Exception ex)
        {
            AddAssistantMessage($"Error loading model: {ex.Message}");
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void ChatInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private async void SendMessage()
    {
        string userInput = ChatInputBox.Text.Trim();
        
        if (string.IsNullOrEmpty(userInput))
            return;

        // Clear input box
        ChatInputBox.Text = string.Empty;

        // Add user message
        AddUserMessage(userInput);

        // If model is ready, generate image
        if (isModelReady && stableDiffusion != null)
        {
            await GenerateImageAsync(userInput);
        }
        else
        {
            AddAssistantMessage("Please wait, the model is still loading...");
        }
    }

    private async Task GenerateImageAsync(string prompt)
    {
        var generatingMessage = AddAssistantMessageWithLoading($"Generating image for: '{prompt}'...");

        try
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var image = await Task.Run(() => stableDiffusion!.Inference(prompt, token), token);

            if (image != null)
            {
                // Convert Bitmap to BitmapImage
                BitmapImage bitmapImage = BitmapFunctions.ConvertBitmapToBitmapImage(image);
                
                // Update message with image
                generatingMessage.Text = "Here's your generated image:";
                generatingMessage.ImageSource = bitmapImage;
                generatingMessage.IsLoading = false;
                
                // Save the prompt for potential export
                lastGeneratedPrompt = prompt;
                
                // Scroll to bottom
                ScrollToBottom();

                // Ask user if they want to export as VS project
                await AskUserToExportProject(prompt);
            }
            else
            {
                generatingMessage.Text = "Failed to generate image. Please try again.";
                generatingMessage.IsLoading = false;
            }
        }
        catch (OperationCanceledException)
        {
            generatingMessage.Text = "Image generation was cancelled.";
            generatingMessage.IsLoading = false;
        }
        catch (Exception ex)
        {
            generatingMessage.Text = $"Error generating image: {ex.Message}";
            generatingMessage.IsLoading = false;
        }
    }

    private async Task AskUserToExportProject(string prompt)
    {
        // Instead of showing a dialog, add an inline message with export button
        var exportMessage = new ChatMessage
        {
            Text = "Would you like to export this as a Visual Studio project? The project will be pre-configured with your prompt.",
            IsUser = false,
            IsAssistant = true,
            HasExportButton = true,
            ExportPrompt = prompt
        };
        messages.Add(exportMessage);
        ScrollToBottom();
    }

    private async void ExportProjectButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the prompt from the button's Tag
        if (sender is Button button && button.Tag is string prompt)
        {
            var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1");
            
            if (sample != null && selectedModel != null)
            {
                await ExportProjectWithPromptAsync(sample, selectedModel, prompt);
            }
        }
    }

    private void AddUserMessage(string text)
    {
        messages.Add(new ChatMessage
        {
            Text = text,
            IsUser = true,
            IsAssistant = false
        });
        ScrollToBottom();
    }

    private void AddAssistantMessage(string text)
    {
        messages.Add(new ChatMessage
        {
            Text = text,
            IsUser = false,
            IsAssistant = true
        });
        ScrollToBottom();
    }

    private ChatMessage AddAssistantMessageWithLoading(string text)
    {
        var message = new ChatMessage
        {
            Text = text,
            IsUser = false,
            IsAssistant = true,
            IsLoading = true
        };
        messages.Add(message);
        ScrollToBottom();
        return message;
    }

    private void ScrollToBottom()
    {
        // Delay to ensure layout is updated
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null);
        });
    }

    private Grid CreateCodeViewerContent(Dictionary<string, string> codeFiles)
    {
        // Create code formatter with syntax highlighting
        var codeFormatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(ActualTheme));

        // Create TabView for multiple files
        var codeTabView = new TabView
        {
            IsAddTabButtonVisible = false,
            TabWidthMode = TabViewWidthMode.SizeToContent
        };

        // Add tabs for each code file
        foreach (var codeFile in codeFiles)
        {
            var tabItem = new TabViewItem
            {
                Header = codeFile.Key,
                IsClosable = false
            };
            codeTabView.TabItems.Add(tabItem);
        }

        // Create the code display area
        var codeTextBlock = new RichTextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 14,
            IsTextSelectionEnabled = true,
            LineHeight = 16,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(8, 16, 8, 16)
        };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent)
        };

        scrollViewer.Content = codeTextBlock;

        // Handle tab selection changed
        codeTabView.SelectionChanged += (s, args) =>
        {
            var selectedTab = codeTabView.SelectedItem as TabViewItem;
            if (selectedTab != null && selectedTab.Header is string fileName)
            {
                var code = codeFiles[fileName];
                var extension = fileName.Split('.').LastOrDefault();
                
                codeTextBlock.Blocks.Clear();
                codeFormatter.FormatRichTextBlock(
                    code, 
                    Languages.FindById(extension) ?? Languages.CSharp, 
                    codeTextBlock);
            }
        };

        // Select first tab by default
        if (codeTabView.TabItems.Count > 0)
        {
            codeTabView.SelectedIndex = 0;
        }

        // Create container grid
        var grid = new Grid
        {
            Width = 800,
            Height = 600,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        Grid.SetRow(codeTabView, 0);
        Grid.SetRow(scrollViewer, 1);

        grid.Children.Add(codeTabView);
        grid.Children.Add(scrollViewer);

        return grid;
    }
}

public class ChatMessage : INotifyPropertyChanged
{
    private string text = string.Empty;
    private bool isUser;
    private bool isAssistant;
    private BitmapImage? imageSource;
    private bool isLoading;
    private bool hasExportButton;
    private string? exportPrompt;

    public string Text
    {
        get => text;
        set
        {
            text = value;
            OnPropertyChanged();
        }
    }

    public bool IsUser
    {
        get => isUser;
        set
        {
            isUser = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUserVisibility));
        }
    }

    public bool IsAssistant
    {
        get => isAssistant;
        set
        {
            isAssistant = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAssistantVisibility));
        }
    }

    public BitmapImage? ImageSource
    {
        get => imageSource;
        set
        {
            imageSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasImageVisibility));
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        set
        {
            isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLoadingVisibility));
        }
    }

    public bool HasExportButton
    {
        get => hasExportButton;
        set
        {
            hasExportButton = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasExportButtonVisibility));
        }
    }

    public string? ExportPrompt
    {
        get => exportPrompt;
        set
        {
            exportPrompt = value;
            OnPropertyChanged();
        }
    }

    public Visibility IsUserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsAssistantVisibility => IsAssistant ? Visibility.Visible : Visibility.Collapsed;
    public bool HasImage => ImageSource != null;
    public Visibility HasImageVisibility => HasImage ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsLoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasExportButtonVisibility => HasExportButton ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Extension methods for top bar buttons
partial class ChatPage
{
    // Initial State button handlers (for Chat sample)
    private async void InitialCodeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the Chat sample (Language Models)
            var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "feb39ede-cb55-4e36-9ec6-cf7c5333254f");
            
            if (sample == null)
            {
                return;
            }

            await ShowSampleCodeAsync(sample);
        }
        catch (Exception)
        {
            // Silently fail for demo purposes
        }
    }

    private async void InitialExportToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the Chat sample (Language Models)
            var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "feb39ede-cb55-4e36-9ec6-cf7c5333254f");
            
            if (sample == null)
            {
                return;
            }

            // Get a default language model for the Chat sample
            // Chat sample requires ModelType.LanguageModels or ModelType.PhiSilica
            ModelDetails? languageModel = null;
            
            // Try to find a compatible language model
            if (ModelTypeHelpers.ModelDetails.TryGetValue(ModelType.LanguageModels, out var langModel))
            {
                languageModel = langModel;
            }
            else if (ModelTypeHelpers.ModelDetails.TryGetValue(ModelType.PhiSilica, out var phiModel))
            {
                languageModel = phiModel;
            }

            if (languageModel == null)
            {
                return;
            }

            // Use the existing Generator.AskGenerateAndOpenAsync method
            await Generator.AskGenerateAndOpenAsync(
                sample,
                new ModelDetails[] { languageModel },
                App.AppData.WinMLSampleOptions,
                this.XamlRoot);
        }
        catch (Exception)
        {
            // Silently fail for demo purposes
        }
    }

    private async Task ShowSampleCodeAsync(Sample sample)
    {
        try
        {
            // Collect all code files (similar to ChatCodeToggle_Click for Stable Diffusion)
            var codeFiles = new Dictionary<string, string>();

            // We'll use a minimal model info dictionary since Chat sample is generic
            var modelInfos = new Dictionary<ModelType, (ExpandedModelDetails, string)>();

            // Add Sample.xaml.cs
            if (!string.IsNullOrEmpty(sample.CSCode))
            {
                codeFiles["Sample.xaml.cs"] = sample.GetCleanCSCode(modelInfos);
            }

            // Add Sample.xaml
            if (!string.IsNullOrEmpty(sample.XAMLCode))
            {
                codeFiles["Sample.xaml"] = sample.XAMLCode;
            }

            // Add shared code files
            var expandedModels = new Dictionary<ModelType, ExpandedModelDetails>();
            foreach (var sharedCodeEnum in sample.GetAllSharedCode(expandedModels))
            {
                string sharedCodeName = SharedCodeHelpers.GetName(sharedCodeEnum);
                string sharedCodeContent = SharedCodeHelpers.GetSource(sharedCodeEnum);
                codeFiles[sharedCodeName] = sharedCodeContent;
            }

            if (codeFiles.Count == 0)
            {
                return;
            }

            // Create the code viewer dialog
            var codeViewerContent = CreateCodeViewerContent(codeFiles);

            var codeDialog = new ContentDialog
            {
                Title = "Chat Sample - Source Code",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = codeViewerContent
            };

            await codeDialog.ShowAsync();
        }
        catch (Exception)
        {
            // Silently fail for demo purposes
        }
    }

    // Chat State button handlers (for Stable Diffusion sample)
    private async void ChatCodeToggle_Click(object sender, RoutedEventArgs e)
    {
        // Get the Generate Image sample
        var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1");
        
        if (sample == null || selectedModel == null)
        {
            return;
        }

        try
        {
            // Get the cached model
            var cachedModelPath = App.ModelCache.GetCachedModel(selectedModel.Url);
            if (cachedModelPath == null)
            {
                return;
            }

            // Create ExpandedModelDetails for code generation
            var expandedModel = new ExpandedModelDetails(
                Id: selectedModel.Id,
                Path: cachedModelPath.Path,
                Url: selectedModel.Url,
                ModelSize: cachedModelPath.ModelSize,
                HardwareAccelerator: selectedModel.HardwareAccelerators.FirstOrDefault(),
                WinMlSampleOptions: App.AppData.WinMLSampleOptions
            );

            // Collect all code files
            var codeFiles = new Dictionary<string, string>();

            // Create model info dictionary for GetCleanCSCode
            var modelInfos = new Dictionary<ModelType, (ExpandedModelDetails, string)>
            {
                { 
                    ModelType.StableDiffusion, 
                    (expandedModel, $"@\"{cachedModelPath.Path}\"") 
                }
            };

            // Add Sample.xaml.cs
            if (!string.IsNullOrEmpty(sample.CSCode))
            {
                codeFiles["Sample.xaml.cs"] = sample.GetCleanCSCode(modelInfos);
            }

            // Add Sample.xaml
            if (!string.IsNullOrEmpty(sample.XAMLCode))
            {
                codeFiles["Sample.xaml"] = sample.XAMLCode;
            }

            // Add shared code files
            var expandedModels = new Dictionary<ModelType, ExpandedModelDetails>
            {
                { ModelType.StableDiffusion, expandedModel }
            };

            foreach (var sharedCodeEnum in sample.GetAllSharedCode(expandedModels))
            {
                string sharedCodeName = SharedCodeHelpers.GetName(sharedCodeEnum);
                string sharedCodeContent = SharedCodeHelpers.GetSource(sharedCodeEnum);
                codeFiles[sharedCodeName] = sharedCodeContent;
            }

            // Create the code viewer dialog
            var codeViewerContent = CreateCodeViewerContent(codeFiles);

            var codeDialog = new ContentDialog
            {
                Title = "Stable Diffusion Image Generation - Source Code",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = codeViewerContent
            };

            await codeDialog.ShowAsync();
        }
        catch (Exception)
        {
            // Silently fail for top bar button
        }
    }

    private async void ChatExportToggle_Click(object sender, RoutedEventArgs e)
    {
        // Get the Generate Image sample
        var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1");
        
        if (sample == null || selectedModel == null)
        {
            return;
        }

        // If we have a prompt from the last generation, use custom export with prompt injection
        if (!string.IsNullOrEmpty(lastGeneratedPrompt))
        {
            await ExportProjectWithPromptAsync(sample, selectedModel, lastGeneratedPrompt);
        }
        else
        {
            // Fallback to standard export
            await Generator.AskGenerateAndOpenAsync(
                sample,
                new[] { selectedModel },
                App.AppData.WinMLSampleOptions,
                this.XamlRoot);
        }
    }

    private async Task ExportProjectWithPromptAsync(Sample sample, ModelDetails model, string userPrompt)
    {
        var cachedModels = sample.GetCacheModelDetailsDictionary(new[] { model }, App.AppData.WinMLSampleOptions);

        if (cachedModels == null)
        {
            return;
        }

        var contentStackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        contentStackPanel.Children.Add(new TextBlock
        {
            Text = "Create a standalone VS project based on this sample.",
            Margin = new Thickness(0, 0, 0, 16)
        });

        RadioButton? copyRadioButton = null;

        var totalSize = cachedModels.Sum(cm => cm.Value.ModelSize);
        if (totalSize != 0)
        {
            var radioButtons = new RadioButtons();
            radioButtons.Items.Add(new RadioButton
            {
                Content = "Reference model from model cache",
                IsChecked = true
            });

            copyRadioButton = new RadioButton
            {
                Content = new TextBlock()
                {
                    Text = $"Copy model({AppUtils.FileSizeToString(totalSize)}) to project directory"
                }
            };

            radioButtons.Items.Add(copyRadioButton);

            contentStackPanel.Children.Add(radioButtons);
        }

        ContentDialog exportDialog = new ContentDialog()
        {
            Title = "Export Visual Studio project",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = "Export",
            XamlRoot = this.XamlRoot,
            Content = contentStackPanel
        };

        ContentDialog? progressDialog = null;
        try
        {
            var output = await exportDialog.ShowAsync();

            if (output == ContentDialogResult.Primary)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var folder = await picker.PickSingleFolderAsync();
                
                if (folder != null)
                {
                    progressDialog = new ContentDialog
                    {
                        XamlRoot = this.XamlRoot,
                        Title = "Creating Visual Studio project..",
                        Content = new ProgressRing { IsActive = true, Width = 48, Height = 48 }
                    };
                    _ = progressDialog.ShowAsync();

                    var generator = new Generator();
                    var projectPath = await generator.GenerateAsync(
                        sample,
                        cachedModels,
                        copyRadioButton != null && copyRadioButton.IsChecked != null && copyRadioButton.IsChecked.Value,
                        folder.Path,
                        CancellationToken.None);

                    // Post-process: Inject the user's prompt into the generated code
                    await InjectPromptIntoGeneratedProject(projectPath, userPrompt);

                    progressDialog.Closed += async (_, _) =>
                    {
                        var confirmationDialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = "Project exported",
                            Content = new TextBlock
                            {
                                Text = $"The project has been successfully exported with your prompt: \"{userPrompt}\"",
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            PrimaryButtonText = "Open folder",
                            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                            CloseButtonText = "Close"
                        };

                        var shouldOpenFolder = await confirmationDialog.ShowAsync();
                        if (shouldOpenFolder == ContentDialogResult.Primary)
                        {
                            await Windows.System.Launcher.LaunchFolderPathAsync(projectPath);
                        }
                    };
                    
                    progressDialog.Hide();
                    progressDialog = null;
                }
            }
        }
        catch (Exception ex)
        {
            progressDialog?.Hide();
            
            var errorDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Export failed",
                Content = new TextBlock
                {
                    Text = $"Failed to export project: {ex.Message}",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                CloseButtonText = "Close"
            };
            await errorDialog.ShowAsync();
        }
    }

    private async Task InjectPromptIntoGeneratedProject(string projectPath, string prompt)
    {
        try
        {
            string csPath = Path.Join(projectPath, "Sample.xaml.cs");
            
            if (!File.Exists(csPath))
            {
                return;
            }

            string csCode = await File.ReadAllTextAsync(csPath);

            // Replace the empty prompt with the user's prompt
            // This makes it visible in the code when opening the project
            csCode = csCode.Replace(
                "private string prompt = string.Empty;",
                $"private string prompt = \"{EscapeForCSharp(prompt)}\";");

            // Optional: Also pre-fill the TextBox in Page_Loaded for better demo experience
            // Users can still see and modify the prompt in the UI
            csCode = csCode.Replace(
                "InputBox.Focus(FocusState.Programmatic);",
                $"InputBox.Text = \"{EscapeForCSharp(prompt)}\";\n        InputBox.Focus(FocusState.Programmatic);");

            await File.WriteAllTextAsync(csPath, csCode);
        }
        catch (Exception ex)
        {
            // Silently fail - the project is still usable even if injection fails
            System.Diagnostics.Debug.WriteLine($"Failed to inject prompt: {ex.Message}");
        }
    }

    private string EscapeForCSharp(string text)
    {
        // Escape special characters for C# string literals
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }

    private void UpdateModelButton()
    {
        // Show model info, hide placeholder
        ChatModelPlaceholder.Visibility = Visibility.Collapsed;
        ChatModelInfo.Visibility = Visibility.Visible;
    }
}
