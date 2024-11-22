// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Chat",
    Model1Types = [ModelType.LanguageModels],
    Id = "feb39ede-cb55-4e36-9ec6-cf7c5333254f",
    Icon = "\uE8D4",
    Scenario = ScenarioType.TextChat,
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    SharedCode = [
        SharedCodeEnum.GenAIModel,
        SharedCodeEnum.Message,
        SharedCodeEnum.LlmPromptTemplate,
        SharedCodeEnum.ChatTemplateSelector,
        SharedCodeEnum.Utils
    ])]
internal sealed partial class Chat : BaseSamplePage
{
    private CancellationTokenSource? cts;
    public ObservableCollection<Message> Messages { get; } = [];

    private IChatClient? model;
    public Chat()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        model = await sampleParams.GetIChatClientAsync();

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
        model?.Dispose();
    }

    private void CancelResponse()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        SendBtn.Visibility = Visibility.Visible;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
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
        if (model == null)
        {
            return;
        }

        Messages.Add(new Message(text, DateTime.Now, ChatRole.User));
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputBox, "Generating response, please wait.", "ChatWaitAnnouncementActivityId"); // <exclude-line>>

        Task.Run(async () =>
        {
            var history = Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            var responseMessage = new Message(string.Empty, DateTime.Now, ChatRole.Assistant);

            DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(responseMessage);
                StopBtn.Visibility = Visibility.Visible;
            });

            cts = new CancellationTokenSource();

            history.Insert(0, new ChatMessage(ChatRole.System, "You are a helpful assistant"));

            await foreach (var messagePart in model.CompleteStreamingAsync(history, null, cts.Token))
            {
                var part = messagePart;
                DispatcherQueue.TryEnqueue(() =>
                {
                    responseMessage.Content += part;

                    // <exclude>
                    if (!contentStartedBeingGenerated)
                    {
                        NarratorHelper.Announce(InputBox, "Response has started generating.", "ChatResponseAnnouncementActivityId");
                        contentStartedBeingGenerated = true;
                    }

                    // </exclude>
                });
            }

            cts?.Dispose();
            cts = null;

            DispatcherQueue.TryEnqueue(() =>
            {
                NarratorHelper.Announce(InputBox, "Content has finished generating.", "ChatDoneAnnouncementActivityId"); // <exclude-line>
                StopBtn.Visibility = Visibility.Collapsed;
                SendBtn.Visibility = Visibility.Visible;
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
}