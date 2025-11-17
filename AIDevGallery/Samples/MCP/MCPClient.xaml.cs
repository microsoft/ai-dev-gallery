// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.MCP.Services;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP;

[GallerySample(
    Name = "MCP Client",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Id = "feb39ede-cb55-4e36-9ec6-cf7c5333255f",
    Icon = "\uE8D4",
    Scenario = ScenarioType.MCPMCPClient,
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "CommunityToolkit.WinUI.Converters",
        "Microsoft.Extensions.AI",
        "ModelContextProtocol"
    ],
    SharedCode = [
        SharedCodeEnum.Message,
        SharedCodeEnum.ChatTemplateSelector,
    ])]
internal sealed partial class MCPClient : BaseSamplePage
{
    private CancellationTokenSource? cts;
    public ObservableCollection<Message> Messages { get; } = [];

    private bool isImeActive = true;

    private IChatClient? model;
    private McpManager? mcpManager;

    // Markers for the assistant's think area (displayed in a dedicated UI region).
    private static readonly string[] ThinkTagOpens = new[] { "<think>", "<thought>", "<reasoning>" };
    private static readonly string[] ThinkTagCloses = new[] { "</think>", "</thought>", "</reasoning>" };
    private static readonly int MaxOpenThinkMarkerLength = ThinkTagOpens.Max(s => s.Length);

    public MCPClient()
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

            // 初始化 MCP 管理器，传递AI聊天客户端用于智能路由
            mcpManager = new McpManager(chatClient: model);
            var mcpInitialized = await mcpManager.InitializeAsync();

            if (!mcpInitialized)
            {
                // 延迟显示错误，避免与其他对话框冲突
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(500); // 等待其他对话框关闭
                    ShowException(new Exception("Failed to initialize MCP Manager. MCP functionality will be limited."));
                });
            }

            // 更新状态显示
            DispatcherQueue.TryEnqueue(UpdateMcpStatus);
        }
        catch (Exception ex)
        {
            // 延迟显示错误，避免与其他对话框冲突
            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(500);
                ShowException(ex);
            });
        }

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        InputBox.Focus(FocusState.Programmatic);
        UpdateRewriteButtonState();
        UpdateClearButtonState();
        UpdateMcpStatus();
    }

    // </exclude>
    private void CleanUp()
    {
        CancelResponse();

        // 确保关闭任何打开的对话框
        if (_currentDialog != null)
        {
            try
            {
                _currentDialog.Hide();
            }
            catch
            {
                // 忽略关闭错误
            }

            _currentDialog = null;
        }

        model?.Dispose();
        mcpManager?.Dispose();
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
        if (model == null || mcpManager == null)
        {
            return;
        }

        Messages.Add(new Message(text.Trim(), DateTime.Now, ChatRole.User));
        UpdateRewriteButtonState();
        UpdateClearButtonState();
        NarratorHelper.Announce(InputBox, "Processing your request with MCP tools...", "ChatWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("AddMessage"); // <exclude-line>

        Task.Run(async () =>
        {
            var responseMessage = new Message(string.Empty, DateTime.Now, ChatRole.Assistant)
            {
                IsPending = true
            };

            DispatcherQueue.TryEnqueue(() =>
            {
                Messages.Add(responseMessage);
                UpdateClearButtonState();
                StopBtn.Visibility = Visibility.Visible;
                InputBox.IsEnabled = false;
                InputBox.PlaceholderText = "Processing MCP request, please wait...";
            });

            cts = new CancellationTokenSource();

            try
            {
                // <exclude>
                ShowDebugInfo("Processing with MCP tools...");
                var swEnd = Stopwatch.StartNew();

                // </exclude>

                // 使用 MCP 管理器处理查询
                var mcpResponse = await mcpManager.ProcessQueryAsync(text.Trim(), model, cts.Token);

                // <exclude>
                swEnd.Stop();
                var debugInfo = $"MCP processing completed in {swEnd.Elapsed.TotalSeconds:0.00}s\nSource: {mcpResponse.Source}";
                
                // 添加路由决策详细信息
                if (mcpResponse.RawResult?.RoutingInfo != null)
                {
                    var routing = mcpResponse.RawResult.RoutingInfo;
                    debugInfo += $"\nRouting Decision:\n  Server: {routing.SelectedServer.Name}\n  Tool: {routing.SelectedTool.Name}\n  Confidence: {routing.Confidence:F2}\n  Reasoning: {routing.Reasoning}";
                }
                
                ShowDebugInfo(debugInfo);

                // </exclude>
                DispatcherQueue.TryEnqueue(() =>
                {
                    responseMessage.IsPending = false;
                    responseMessage.Content = mcpResponse.Answer;

                    // 如果需要用户确认，添加特殊标记
                    if (mcpResponse.RequiresConfirmation)
                    {
                        responseMessage.ThinkContent = "This action requires confirmation. Please respond with 'yes' to proceed.";
                    }

                    // 添加调试信息到思考区域（仅在开发模式下）
                    if (!string.IsNullOrEmpty(mcpResponse.Source) && mcpResponse.Source != "System")
                    {
                        var debugInfo = $"Tool used: {mcpResponse.Source}";
                        if (mcpResponse.RawResult?.ExecutionTime != null)
                        {
                            debugInfo += $" (executed in {mcpResponse.RawResult.ExecutionTime.TotalMilliseconds:0}ms)";
                        }

                        responseMessage.ThinkContent = string.IsNullOrEmpty(responseMessage.ThinkContent)
                            ? debugInfo
                            : $"{responseMessage.ThinkContent}\n\n{debugInfo}";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    responseMessage.IsPending = false;
                    responseMessage.Content = "Request was cancelled.";
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    responseMessage.IsPending = false;
                    responseMessage.Content = $"Error processing request: {ex.Message}";
                });
            }

            cts?.Dispose();
            cts = null;

            DispatcherQueue.TryEnqueue(() =>
            {
                NarratorHelper.Announce(InputBox, "MCP response completed.", "ChatDoneAnnouncementActivityId"); // <exclude-line>
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

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        // Cancel any ongoing response generation before clearing chat
        CancelResponse();
        ClearChat();
    }

    private void RewriteBtn_Click(object sender, RoutedEventArgs e)
    {
        RewriteLastMessage();
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

    private void ClearChat()
    {
        Messages.Clear();
        UpdateRewriteButtonState();
        UpdateClearButtonState();
        SendSampleInteractedEvent("ClearChat"); // <exclude-line>
    }

    private void RewriteLastMessage()
    {
        var lastUserMessage = Messages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastUserMessage != null)
        {
            InputBox.Text = lastUserMessage.Content;
            InputBox.Focus(FocusState.Programmatic);

            InputBox.SelectionStart = InputBox.Text.Length;
            InputBox.SelectionLength = 0;
            SendSampleInteractedEvent("RewriteLastMessage"); // <exclude-line>
        }
    }

    private void UpdateRewriteButtonState()
    {
        foreach (var message in Messages.Where(m => m.Role == ChatRole.User))
        {
            message.IsLastUserMessage = false;
        }

        var lastUserMessage = Messages.LastOrDefault(m => m.Role == ChatRole.User);
        lastUserMessage?.IsLastUserMessage = true;
    }

    private void UpdateClearButtonState()
    {
        ClearBtn.IsEnabled = Messages.Count > 0;
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

    private async void UpdateMcpStatus()
    {
        if (mcpManager == null)
        {
            McpStatusIcon.Glyph = "\uE783"; // Warning icon
            McpStatusText.Text = "MCP not initialized";
            return;
        }

        try
        {
            var status = await mcpManager.GetSystemStatusAsync();
            var initialized = (bool)(status["initialized"] ?? false);
            var serverCount = (int)(status["connected_servers"] ?? 0);
            var toolCount = (int)(status["total_tools"] ?? 0);

            if (initialized && serverCount > 0)
            {
                McpStatusIcon.Glyph = "\uE73E"; // Checkmark icon
                McpStatusText.Text = $"{serverCount} servers, {toolCount} tools";
            }
            else if (initialized)
            {
                McpStatusIcon.Glyph = "\uE783"; // Warning icon
                McpStatusText.Text = "No MCP servers available";
            }
            else
            {
                McpStatusIcon.Glyph = "\uE894"; // Error icon
                McpStatusText.Text = "MCP initialization failed";
            }
        }
        catch (Exception ex)
        {
            McpStatusIcon.Glyph = "\uE894"; // Error icon
            McpStatusText.Text = "MCP status error";
        }
    }

    private async void McpStatusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (mcpManager == null)
        {
            await ShowMcpStatusDialog("MCP Manager not initialized");
            return;
        }

        try
        {
            var status = await mcpManager.GetSystemStatusAsync();
            var statusText = FormatMcpStatus(status);

            // 添加工具目录信息
            var toolCatalog = mcpManager.GetToolCatalog();
            var fullContent = $"{statusText}\n\n{new string('=', 50)}\n\n{toolCatalog}";

            await ShowMcpStatusDialog(fullContent);
        }
        catch (Exception ex)
        {
            await ShowMcpStatusDialog($"Error retrieving MCP status: {ex.Message}");
        }
    }

    private string FormatMcpStatus(Dictionary<string, object> status)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Initialized: {status.GetValueOrDefault("initialized", false)}");
        text.AppendLine($"Connected Servers: {status.GetValueOrDefault("connected_servers", 0)}");
        text.AppendLine($"Total Tools: {status.GetValueOrDefault("total_tools", 0)}");
        text.AppendLine();

        if (status.ContainsKey("servers") && status["servers"] is List<object> servers)
        {
            text.AppendLine("Server Details:");
            foreach (var server in servers.Cast<Dictionary<string, object>>())
            {
                text.AppendLine($"  • {server.GetValueOrDefault("server_name", "Unknown")}");
                text.AppendLine($"    Connected: {server.GetValueOrDefault("connected", false)}");
                text.AppendLine($"    Tools: {server.GetValueOrDefault("tool_count", 0)}");
                text.AppendLine($"    Success Rate: {server.GetValueOrDefault("success_rate", 0.0):P1}");
                text.AppendLine();
            }
        }

        return text.ToString();
    }

    private static ContentDialog? _currentDialog;

    private async Task ShowMcpStatusDialog(string content)
    {
        // 确保一次只有一个对话框打开
        if (_currentDialog != null)
        {
            try
            {
                _currentDialog.Hide();
            }
            catch
            {
                // 忽略可能的关闭错误
            }

            _currentDialog = null;
        }

        var dialog = new ContentDialog
        {
            Title = "MCP Status",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas")
                },
                MaxHeight = 400
            },
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        _currentDialog = dialog;

        try
        {
            await dialog.ShowAsync();
        }
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80004005))
        {
            // ContentDialog已经打开的情况，静默处理
        }
        finally
        {
            if (_currentDialog == dialog)
            {
                _currentDialog = null;
            }
        }
    }
}