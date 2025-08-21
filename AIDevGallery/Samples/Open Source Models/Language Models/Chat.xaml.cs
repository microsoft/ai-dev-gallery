// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Data;
using Windows.UI.Text;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

// 内容块类型到字体样式的转换器
public class ContentBlockTypeToFontStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ContentBlockType blockType)
        {
            return blockType == ContentBlockType.Think ? FontStyle.Italic : FontStyle.Normal;
        }
        return FontStyle.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

[GallerySample(
    Name = "Chat",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Id = "feb39ede-cb55-4e36-9ec6-cf7c5333254f",
    Icon = "\uE8D4",
    Scenario = ScenarioType.TextChat,
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.Extensions.AI"
    ],
    SharedCode = [
        SharedCodeEnum.Message,
        SharedCodeEnum.ChatTemplateSelector,
    ])]
internal sealed partial class Chat : BaseSamplePage
{
    private CancellationTokenSource? cts;
    public ObservableCollection<Message> Messages { get; } = [];

    private bool isImeActive = true;

    private IChatClient? model;
    public Chat()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    private ScrollViewer? scrollViewer;

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            model = await sampleParams.GetIChatClientAsync();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }

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
            !string.IsNullOrWhiteSpace(InputBox.Text) &&
            isImeActive == false)
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
        else
        {
            isImeActive = true;
        }
    }

    private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        isImeActive = false;
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

        Messages.Add(new Message(text.Trim(), DateTime.Now, ChatRole.User));
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputBox, "Generating response, please wait.", "ChatWaitAnnouncementActivityId"); // <exclude-line>>
        SendSampleInteractedEvent("AddMessage"); // <exclude-line>

        Task.Run(async () =>
        {
            var history = Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            var responseMessage = new Message(string.Empty, DateTime.Now, ChatRole.Assistant);

            DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(responseMessage);
                StopBtn.Visibility = Visibility.Visible;
                InputBox.IsEnabled = false;
                InputBox.PlaceholderText = "Please wait for the response to complete before entering a new prompt";
            });

            cts = new CancellationTokenSource();

            history.Insert(0, new ChatMessage(ChatRole.System, "You are a helpful assistant"));

            // <exclude>
            ShowDebugInfo(null);
            var swEnd = Stopwatch.StartNew();
            var swTtft = Stopwatch.StartNew();
            int outputTokens = 0;

            // </exclude>
            
            var accumulatedContent = string.Empty;
            var currentThinkContent = string.Empty;
            var isInThinkBlock = false;
            
            await foreach (var messagePart in model.GetStreamingResponseAsync(history, null, cts.Token))
            {
                // <exclude>
                if (outputTokens == 0)
                {
                    swTtft.Stop();
                }

                outputTokens++;
                double currentTps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
                ShowDebugInfo($"{Math.Round(currentTps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

                // </exclude>
                
                // 处理不同类型的响应
                string part;
                if (messagePart is ChatResponseUpdate chatUpdate)
                {
                    part = chatUpdate.Text ?? string.Empty;
                }
                else
                {
                    part = messagePart.ToString();
                }
                
                accumulatedContent += part;
                
                // 检查是否进入think标签
                if (part.Contains("<think>"))
                {
                    isInThinkBlock = true;
                    currentThinkContent = string.Empty;
                    
                    // 如果之前有普通内容，先添加到ContentBlocks
                    var normalContent = accumulatedContent.Replace("<think>", "").Trim();
                    if (!string.IsNullOrEmpty(normalContent))
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            responseMessage.ContentBlocks.Add(new ContentBlock(normalContent, ContentBlockType.Normal));
                        });
                    }
                    
                    accumulatedContent = string.Empty;
                }
                
                // 如果在think标签内
                if (isInThinkBlock)
                {
                    currentThinkContent += part;
                    
                    // 检查是否结束think标签
                    if (part.Contains("</think>"))
                    {
                        isInThinkBlock = false;
                        
                        // 提取think内容（去除标签）
                        var thinkContent = currentThinkContent
                            .Replace("<think>", "")
                            .Replace("</think>", "")
                            .Trim();
                        
                        if (!string.IsNullOrEmpty(thinkContent))
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                responseMessage.ContentBlocks.Add(new ContentBlock(thinkContent, ContentBlockType.Think));
                            });
                        }
                        
                        currentThinkContent = string.Empty;
                        accumulatedContent = string.Empty;
                    }
                }
                else
                {
                    // 不在think标签内，检查是否有完整的think标签
                    if (accumulatedContent.Contains("</think>"))
                    {
                        // 处理完整的think标签
                        var parts = accumulatedContent.Split(new[] { "</think>" }, StringSplitOptions.None);
                        if (parts.Length >= 2)
                        {
                            var beforeThink = parts[0].Replace("<think>", "").Trim();
                            var thinkContent = parts[0].Substring(parts[0].IndexOf("<think>") + 7).Trim();
                            var afterThink = parts[1].Trim();
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                if (!string.IsNullOrEmpty(beforeThink))
                                {
                                    responseMessage.ContentBlocks.Add(new ContentBlock(beforeThink, ContentBlockType.Normal));
                                }
                                if (!string.IsNullOrEmpty(thinkContent))
                                {
                                    responseMessage.ContentBlocks.Add(new ContentBlock(thinkContent, ContentBlockType.Think));
                                }
                                if (!string.IsNullOrEmpty(afterThink))
                                {
                                    responseMessage.ContentBlocks.Add(new ContentBlock(afterThink, ContentBlockType.Normal));
                                }
                            });
                        }
                        accumulatedContent = string.Empty;
                    }
                    else if (!accumulatedContent.Contains("<think>"))
                    {
                        // 没有think标签，直接添加普通内容
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            responseMessage.ContentBlocks.Add(new ContentBlock(accumulatedContent, ContentBlockType.Normal));
                        });
                        accumulatedContent = string.Empty;
                    }
                }

                // 保持原有的Content属性更新以保持向后兼容
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
            
            // 处理剩余的内容
            if (!string.IsNullOrEmpty(accumulatedContent))
            {
                var finalContent = accumulatedContent.Trim();
                if (!string.IsNullOrEmpty(finalContent))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        responseMessage.ContentBlocks.Add(new ContentBlock(finalContent, ContentBlockType.Normal));
                    });
                }
            }

            // <exclude>
            swEnd.Stop();
            double tps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
            ShowDebugInfo($"{Math.Round(tps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");
            // </exclude>
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