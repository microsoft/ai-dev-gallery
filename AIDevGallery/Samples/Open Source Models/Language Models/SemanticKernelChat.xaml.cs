// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Semantic Kernel Chat",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Id = "0d20e41d-d2d4-4e49-b55a-2a38a02db482",
    Icon = "\uE8D4",
    Scenario = ScenarioType.TextSemanticKernelChat,
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.SemanticKernel.Core"
    ],
    SharedCode = [
        SharedCodeEnum.ChatTemplateSelector,
        SharedCodeEnum.Message,
    ])]
internal sealed partial class SemanticKernelChat : BaseSamplePage
{
    private const string _systemPrompt = "You are a helpful assistant.";

    private CancellationTokenSource? cts;
    private IChatCompletionService? _chatCompletionService;
    private Kernel? _semanticKernel;
    private ChatHistory _chatHistory;
    private bool _modelReady;

    public ObservableCollection<Message> Messages { get; } = [];

    public SemanticKernelChat()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(_systemPrompt);
        this.InitializeComponent();
    }

    private ScrollViewer? scrollViewer;

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var model = await sampleParams.GetIChatClientAsync();
        if (model == null)
        {
            return;
        }

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        _chatCompletionService = model.AsChatCompletionService();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        IKernelBuilder builder = Kernel.CreateBuilder();
        _semanticKernel = builder.Build();

        InputBox.IsEnabled = true;
        _modelReady = true;

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        InputBox.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private void CleanUp()
    {
        CancelResponse();
        _chatCompletionService = null;
        _semanticKernel = null;
    }

    private void CancelResponse()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        SendBtn.Visibility = Visibility.Visible;
        EnableInputBoxWithPlaceholder();
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter &&
            !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) &&
            sender is TextBox &&
            !string.IsNullOrWhiteSpace(InputBox.Text))
        {
            var cursorPosition = InputBox.SelectionStart;
            var text = InputBox.Text;
            if (cursorPosition > 0 && (text[cursorPosition - 1] == '\n' || text[cursorPosition - 1] == '\r'))
            {
                text = text.Remove(cursorPosition - 1, 1);
                InputBox.Text = text;
            }

            InputBox.SelectionStart = cursorPosition - 1;

            SendMessage();
        }
    }

    private void SendMessage()
    {
        if (InputBox.Text.Length > 0)
        {
            AddMessage(InputBox.Text);
            InputBox.Text = string.Empty;
            SendBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void AddMessage(string text)
    {
        if (_chatCompletionService == null || _semanticKernel == null || !_modelReady)
        {
            return;
        }

        Messages.Add(new Message(text.Trim(), DateTime.Now, ChatRole.User));
        _chatHistory.AddUserMessage(text);
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputBox, "Generating response, please wait.", "ChatWaitAnnouncementActivityId"); // <exclude-line>

        Task.Run(
        async () =>
        {
            var responseMessage = new Message(string.Empty, DateTime.Now, ChatRole.Assistant);

            DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(responseMessage);
                StopBtn.Visibility = Visibility.Visible;
                InputBox.IsEnabled = false;
                InputBox.PlaceholderText = "Please wait for the response to complete before entering a new prompt";
            });

            cts = new CancellationTokenSource();
            string fullResponse = string.Empty;

            await foreach (var messagePart in _chatCompletionService.GetStreamingChatMessageContentsAsync(_chatHistory, null, _semanticKernel, cts.Token))
            {
                fullResponse += messagePart.Content;
                DispatcherQueue.TryEnqueue(() =>
                {
                    responseMessage.Content += messagePart.Content;

                    // <exclude>
                    if (!contentStartedBeingGenerated)
                    {
                        NarratorHelper.Announce(InputBox, "Response has started generating.", "ChatResponseAnnouncementActivityId");
                        contentStartedBeingGenerated = true;
                    }

                    // </exclude>
                });
            }

            _chatHistory.AddAssistantMessage(fullResponse);

            cts?.Dispose();
            cts = null;

            DispatcherQueue.TryEnqueue(() =>
            {
                NarratorHelper.Announce(InputBox, "Content has finished generating.", "ChatDoneAnnouncementActivityId"); // <exclude-line>
                StopBtn.Visibility = Visibility.Collapsed;
                SendBtn.Visibility = Visibility.Visible;
                EnableInputBoxWithPlaceholder();
            });
        });
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelResponse();
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SendBtn.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);
    }

    private void EnableInputBoxWithPlaceholder()
    {
        InputBox.IsEnabled = true;
        InputBox.PlaceholderText = "Enter your prompt (Press Shift + Enter to insert a newline)";
    }

    private void InvertedListView_Loaded(object sender, RoutedEventArgs e)
    {
        scrollViewer = FindElement<ScrollViewer>(InvertedListView);

        ItemsStackPanel? itemsStackPanel = FindElement<ItemsStackPanel>(InvertedListView);
        if (itemsStackPanel != null)
        {
            itemsStackPanel.SizeChanged += ItemsStackPanel_SizeChanged;
        }
    }

    private void ItemsStackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (scrollViewer != null)
        {
            bool isScrollbarVisible = scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;

            if (isScrollbarVisible)
            {
                InvertedListView.Padding = new Thickness(-12, 0, 12, 24);
            }
            else
            {
                InvertedListView.Padding = new Thickness(-12, 0, -12, 24);
            }
        }
    }

    private T? FindElement<T>(DependencyObject element)
        where T : DependencyObject
    {
        if (element is T targetElement)
        {
            return targetElement;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = FindElement<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}