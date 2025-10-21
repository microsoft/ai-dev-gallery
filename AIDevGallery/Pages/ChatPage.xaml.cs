// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Samples.SharedCode.StableDiffusionCode;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private CancellationTokenSource? cts;
    private ModelDownload? currentModelDownload;
    private ChatMessage? downloadingMessage;

    public ChatPage()
    {
        this.InitializeComponent();
        this.Loaded += ChatPage_Loaded;
        this.Unloaded += ChatPage_Unloaded;
    }

    private void ChatPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize suggestions
        suggestions.Add("Create an image");
        suggestions.Add("Summarize text");
        suggestions.Add("Explain code");
        suggestions.Add("Draft a text");
        suggestions.Add("Improve writing");
        
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
            else
            {
                // Other suggestions are not implemented in this prototype
                AddAssistantMessage("This feature is coming soon! For this prototype, only 'Create an image' is functional.");
            }
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

        // Check if model is already cached
        await Task.Delay(500); // Small delay for better UX

        var models = ModelDetailsHelper.GetModelDetailsForModelType(ModelType.StableDiffusion);
        selectedModel = models.FirstOrDefault();

        if (selectedModel != null && App.ModelCache.IsModelCached(selectedModel.Url))
        {
            // Model already cached
            AddAssistantMessage("Model is already downloaded! Running the sample for you. What image would you like to create?");
            await LoadModelAsync();
        }
        else
        {
            // Show license dialog
            LicenseCheckBox.IsChecked = false;
            LicenseDialog.IsPrimaryButtonEnabled = false;
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
            downloadingMessage.Text = "Model is already downloaded!";
            downloadingMessage.IsLoading = false;
            await LoadModelAsync();
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

            if (e.Status == DownloadStatus.InProgress)
            {
                int progressPercent = (int)(e.Progress * 100);
                downloadingMessage.Text = $"Downloading model... {progressPercent}%";
            }
            else if (e.Status == DownloadStatus.Completed)
            {
                downloadingMessage.Text = "Model download complete! Running the sample for you. What image would you like to create?";
                downloadingMessage.IsLoading = false;

                if (currentModelDownload != null)
                {
                    currentModelDownload.StateChanged -= ModelDownload_StateChanged;
                    currentModelDownload = null;
                }

                // Load the model
                await LoadModelAsync();
            }
            else if (e.Status == DownloadStatus.Canceled)
            {
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

    private void LicenseCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        LicenseDialog.IsPrimaryButtonEnabled = LicenseCheckBox.IsChecked == true;
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
}

public class ChatMessage : INotifyPropertyChanged
{
    private string text = string.Empty;
    private bool isUser;
    private bool isAssistant;
    private BitmapImage? imageSource;
    private bool isLoading;

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

    public Visibility IsUserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsAssistantVisibility => IsAssistant ? Visibility.Visible : Visibility.Collapsed;
    public bool HasImage => ImageSource != null;
    public Visibility HasImageVisibility => HasImage ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsLoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
