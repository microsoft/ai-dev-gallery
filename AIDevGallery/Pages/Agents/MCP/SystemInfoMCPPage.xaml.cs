// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Pages;

internal sealed partial class SystemInfoMCPPage : Page
{
    private McpClient? mcpClient;
    private const string SystemInfoServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server";

    public SystemInfoMCPPage()
    {
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DisconnectFromServer();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await ConnectToServerAsync();
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        DisconnectFromServer();
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            ConnectButton.IsEnabled = false;
            ConnectionProgressRing.IsActive = true;
            ConnectionStatusText.Text = "Connecting...";
            ErrorPanel.Visibility = Visibility.Collapsed;

            // Create transport options for odr.exe with proxy to SystemInfo server
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "SystemInfo-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    SystemInfoServerId
                }
            };

            var transport = new StdioClientTransport(transportOptions);
            var mcpClientOptions = new McpClientOptions();
            
            mcpClient = await McpClient.CreateAsync(transport, mcpClientOptions);

            ConnectionStatusText.Text = "Connected";
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
            ConnectionProgressRing.IsActive = false;

            await LoadToolsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to connect to SystemInfo MCP Server. Make sure you're running on Windows MCP Private Preview 3 or later with odr.exe available.\n\nError: {ex.Message}");
            ConnectionStatusText.Text = "Connection failed";
            ConnectButton.IsEnabled = true;
            ConnectionProgressRing.IsActive = false;
        }
    }

    private void DisconnectFromServer()
    {
        if (mcpClient != null)
        {
            mcpClient = null;
            ConnectionStatusText.Text = "Disconnected";
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            ToolsPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Collapsed;
            ToolsList.ItemsSource = null;
        }
    }

    private async Task LoadToolsAsync()
    {
        if (mcpClient == null)
        {
            return;
        }

        try
        {
            var tools = await mcpClient.ListToolsAsync();

            if (tools == null || !tools.Any())
            {
                ShowError("No tools found in SystemInfo MCP Server.");
                return;
            }

            // Create buttons for each tool
            var toolButtons = new List<UIElement>();
            foreach (var tool in tools)
            {
                var button = new Button
                {
                    Content = $"{tool.Name}",
                    Tag = tool,
                    Margin = new Thickness(0, 0, 8, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                button.Click += ToolButton_Click;

                var stackPanel = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 12)
                };

                stackPanel.Children.Add(button);

                if (!string.IsNullOrEmpty(tool.Description))
                {
                    var description = new TextBlock
                    {
                        Text = tool.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 0),
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                    };
                    stackPanel.Children.Add(description);
                }

                toolButtons.Add(stackPanel);
            }

            ToolsList.ItemsSource = toolButtons;
            ToolsPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load tools: {ex.Message}");
        }
    }

    private async void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            ShowError($"Sender is not a button. Sender type: {sender?.GetType().Name ?? "null"}");
            return;
        }

        if (button.Tag is not McpClientTool tool)
        {
            ShowError($"Button tag is not a McpClientTool object. Tag type: {button.Tag?.GetType().Name ?? "null"}, Tag value: {button.Tag}");
            return;
        }

        if (mcpClient == null)
        {
            ShowError("MCP Client is not connected.");
            return;
        }

        try
        {
            button.IsEnabled = false;
            ResultPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;

            // Show a loading indicator
            ResultTextBlock.Text = $"Executing {tool.Name}...";
            ResultPanel.Visibility = Visibility.Visible;

            // Call the tool with empty arguments dictionary
            var result = await mcpClient.CallToolAsync(tool.Name, new Dictionary<string, object>());

            if (result.Content != null && result.Content.Count > 0)
            {
                var resultText = string.Empty;
                foreach (var content in result.Content)
                {
                    if (content is TextContentBlock textBlock)
                    {
                        resultText += textBlock.Text + "\n\n";
                    }
                    else if (content is ImageContentBlock imageBlock)
                    {
                        resultText += $"[Image: {imageBlock.Data}]\n\n";
                    }
                    else
                    {
                        resultText += $"[Content Type: {content.Type}]\n\n";
                    }
                }

                // Try to format as JSON if it looks like JSON
                try
                {
                    if (resultText.TrimStart().StartsWith("{") || resultText.TrimStart().StartsWith("["))
                    {
                        var jsonDoc = JsonDocument.Parse(resultText);
                        resultText = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                catch
                {
                    // Not JSON or invalid JSON, keep as-is
                }

                ResultTextBlock.Text = resultText;
                ResultPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ResultTextBlock.Text = "No content returned from the tool.";
                ResultPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to execute tool '{tool.Name}': {ex.Message}\n\nStack trace: {ex.StackTrace}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }
}
