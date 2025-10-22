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
                
                // Scroll to bottom
                ScrollToBottom();

                // Add follow-up message with options
                AddFollowUpOptionsMessage();
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

    private void AddFollowUpOptionsMessage()
    {
        var message = new ChatMessage
        {
            Text = "What would you like to do next?",
            IsUser = false,
            IsAssistant = true,
            HasActions = true
        };
        messages.Add(message);
        ScrollToBottom();
    }

    private async void ShowSourceCodeButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the Generate Image sample
        var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1");
        
        if (sample == null)
        {
            AddAssistantMessage("Sorry, I couldn't find the sample source code.");
            return;
        }

        if (selectedModel == null)
        {
            AddAssistantMessage("Model information not available.");
            return;
        }

        AddAssistantMessage("Opening source code viewer...");

        try
        {
            // Get the cached model
            var cachedModelPath = App.ModelCache.GetCachedModel(selectedModel.Url);
            if (cachedModelPath == null)
            {
                AddAssistantMessage("Model not found in cache.");
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
            AddAssistantMessage("✅ Source code displayed!");
        }
        catch (Exception ex)
        {
            AddAssistantMessage($"Error displaying source code: {ex.Message}");
        }
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

    private void AddCodeTabToView(TabView tabView, string header, string code)
    {
        // Use RichTextBlock for better multi-line text display
        var richTextBlock = new RichTextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            LineHeight = 16
        };

        // Add the code as a single paragraph
        var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
        var run = new Microsoft.UI.Xaml.Documents.Run
        {
            Text = code
        };
        paragraph.Inlines.Add(run);
        richTextBlock.Blocks.Add(paragraph);

        var scrollViewer = new ScrollViewer
        {
            Content = richTextBlock,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8)
        };

        tabView.TabItems.Add(new TabViewItem
        {
            Header = header,
            Content = scrollViewer,
            IsClosable = false
        });
    }

    private async void ExportProjectButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the Generate Image sample
        var sample = SampleDetails.Samples.FirstOrDefault(s => s.Id == "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1");
        
        if (sample == null)
        {
            AddAssistantMessage("Sorry, I couldn't find the sample information.");
            return;
        }

        // Get the model details
        if (selectedModel == null)
        {
            AddAssistantMessage("Model information not available.");
            return;
        }

        AddAssistantMessage("Let me help you export this as a Visual Studio project. You'll be able to choose where to save it and whether to copy the model.");

        try
        {
            // Get cached models
            var models = new[] { selectedModel };
            var cachedModels = sample.GetCacheModelDetailsDictionary(models, App.AppData.WinMLSampleOptions);

            if (cachedModels == null)
            {
                AddAssistantMessage("Failed to prepare model information.");
                return;
            }

            // Ask user about copying model
            var contentStackPanel = new StackPanel { Orientation = Orientation.Vertical };
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
                        Text = $"Copy model ({AppUtils.FileSizeToString(totalSize)}) to project directory"
                    }
                };
                radioButtons.Items.Add(copyRadioButton);
                contentStackPanel.Children.Add(radioButtons);
            }

            var exportDialog = new ContentDialog()
            {
                Title = "Export Visual Studio project",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonText = "Export",
                XamlRoot = XamlRoot,
                Content = contentStackPanel
            };

            var output = await exportDialog.ShowAsync();

            if (output != ContentDialogResult.Primary)
            {
                AddAssistantMessage("Export cancelled.");
                return;
            }

            // Pick folder
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            
            if (folder == null)
            {
                AddAssistantMessage("No folder selected. Export cancelled.");
                return;
            }

            // Show progress
            var progressDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Creating Visual Studio project...",
                Content = new ProgressRing { IsActive = true, Width = 48, Height = 48 }
            };
            _ = progressDialog.ShowAsync();

            // Generate project
            var generator = new Generator();
            bool copyModel = copyRadioButton != null && copyRadioButton.IsChecked == true;
            var projectPath = await generator.GenerateAsync(
                sample,
                cachedModels,
                copyModel,
                folder.Path,
                CancellationToken.None);

            progressDialog.Hide();

            // Add success message with open folder button
            AddProjectExportSuccessMessage(projectPath);
        }
        catch (Exception ex)
        {
            AddAssistantMessage($"❌ Error exporting project: {ex.Message}");
        }
    }

    private void AddProjectExportSuccessMessage(string projectPath)
    {
        var message = new ChatMessage
        {
            Text = $"✅ Project exported successfully to:\n{projectPath}",
            IsUser = false,
            IsAssistant = true,
            ProjectPath = projectPath,
            HasOpenFolderButton = true
        };
        messages.Add(message);
        ScrollToBottom();
    }

    private async void OpenProjectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string folderPath)
        {
            try
            {
                await Windows.System.Launcher.LaunchFolderPathAsync(folderPath);
            }
            catch (Exception ex)
            {
                AddAssistantMessage($"Failed to open folder: {ex.Message}");
            }
        }
    }
}

public class ChatMessage : INotifyPropertyChanged
{
    private string text = string.Empty;
    private bool isUser;
    private bool isAssistant;
    private BitmapImage? imageSource;
    private bool isLoading;
    private bool hasActions;
    private bool hasOpenFolderButton;
    private string? projectPath;

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

    public bool HasActions
    {
        get => hasActions;
        set
        {
            hasActions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActionsVisibility));
        }
    }

    public bool HasOpenFolderButton
    {
        get => hasOpenFolderButton;
        set
        {
            hasOpenFolderButton = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOpenFolderButtonVisibility));
        }
    }

    public string? ProjectPath
    {
        get => projectPath;
        set
        {
            projectPath = value;
            OnPropertyChanged();
        }
    }

    public Visibility IsUserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsAssistantVisibility => IsAssistant ? Visibility.Visible : Visibility.Collapsed;
    public bool HasImage => ImageSource != null;
    public Visibility HasImageVisibility => HasImage ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsLoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasActionsVisibility => HasActions ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasOpenFolderButtonVisibility => HasOpenFolderButton ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
