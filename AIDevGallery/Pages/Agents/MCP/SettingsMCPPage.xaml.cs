// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Pages;

internal sealed partial class SettingsMCPPage : Page
{
    private McpClient? mcpClient;
    private McpClientTool? selectedTool;
    private const string SettingsServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server";

    public SettingsMCPPage()
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

            // Create transport options for odr.exe with proxy to Settings server
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "Settings-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    SettingsServerId
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
            ShowError($"Failed to connect to Settings MCP Server. Make sure you're running on Windows MCP Private Preview 3 or later with odr.exe available.\n\nError: {ex.Message}");
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
            InputPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Collapsed;
            ToolsList.ItemsSource = null;
            selectedTool = null;
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
                ShowError("No tools found in Settings MCP Server.");
                return;
            }

            // Create radio buttons for each tool (only one can be selected at a time)
            var toolButtons = new List<UIElement>();
            foreach (var tool in tools)
            {
                var radioButton = new RadioButton
                {
                    Content = $"{tool.Name}",
                    Tag = tool,
                    GroupName = "SettingsTools",
                    Margin = new Thickness(0, 0, 8, 8)
                };
                radioButton.Click += ToolRadioButton_Click;

                var stackPanel = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 12)
                };

                stackPanel.Children.Add(radioButton);

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

    private void ToolRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton)
        {
            ShowError($"Sender is not a radio button. Sender type: {sender?.GetType().Name ?? "null"}");
            return;
        }

        if (radioButton.Tag is not McpClientTool tool)
        {
            ShowError($"RadioButton tag is not a McpClientTool object. Tag type: {radioButton.Tag?.GetType().Name ?? "null"}, Tag value: {radioButton.Tag}");
            return;
        }

        selectedTool = tool;
        ShowInputPanelForTool(tool);
    }

    private void ShowInputPanelForTool(McpClientTool tool)
    {
        // Hide all input panels first
        SettingsChangeRequestPanel.Visibility = Visibility.Collapsed;
        UndoIdPanel.Visibility = Visibility.Collapsed;
        
        // Clear previous input
        SettingsChangeRequestTextBox.Text = string.Empty;
        UndoIdTextBox.Text = string.Empty;
        
        // Show appropriate input panel based on tool
        switch (tool.Name)
        {
            case "undo_settings_change":
                UndoIdPanel.Visibility = Visibility.Visible;
                break;
            case "is_settings_change_applicable":
            case "make_settings_change":
            case "open_settings_page":
                SettingsChangeRequestPanel.Visibility = Visibility.Visible;
                break;
        }

        InputPanel.Visibility = Visibility.Visible;
        ExecuteToolButton.IsEnabled = true;
        ResultPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private async void ExecuteToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTool == null)
        {
            ShowError("No tool selected.");
            return;
        }

        if (mcpClient == null)
        {
            ShowError("MCP Client is not connected.");
            return;
        }

        try
        {
            ExecuteToolButton.IsEnabled = false;
            ResultPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;

            // Prepare arguments based on the tool
            var arguments = new Dictionary<string, object>();

            switch (selectedTool.Name)
            {
                case "undo_settings_change":
                    var undoId = UndoIdTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(undoId))
                    {
                        ShowError("Please enter an Undo ID.");
                        return;
                    }
                    arguments["UndoId"] = undoId;
                    break;

                case "is_settings_change_applicable":
                case "make_settings_change":
                case "open_settings_page":
                    var settingsRequest = SettingsChangeRequestTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(settingsRequest))
                    {
                        ShowError("Please enter a settings change request.");
                        return;
                    }
                    arguments["SettingsChangeRequest"] = settingsRequest;
                    break;

                default:
                    ShowError($"Unknown tool: {selectedTool.Name}");
                    return;
            }

            // Show tool call information
            var toolCallJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["tool"] = selectedTool.Name,
                ["arguments"] = arguments
            }, new JsonSerializerOptions { WriteIndented = true });

            var displayText = $"🔧 Tool Call:\n{toolCallJson}\n\n";

            // Show a loading indicator
            displayText += $"⏳ Executing {selectedTool.Name}...\n\n";
            ResultTextBlock.Text = displayText;
            ResultPanel.Visibility = Visibility.Visible;

            // Call the tool with the prepared arguments
            var result = await mcpClient.CallToolAsync(selectedTool.Name, arguments);

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

                // Build the complete output with tool call and response
                var completeOutput = $"🔧 Tool Call:\n{toolCallJson}\n\n";
                completeOutput += $"📋 Response:\n{resultText}";

                // Add special note for make_settings_change to highlight UndoId
                if (selectedTool.Name == "make_settings_change" && resultText.Contains("UndoId"))
                {
                    completeOutput += "\n\n⚠️ IMPORTANT: Save the UndoId from the response above!\n" +
                                     "You'll need it to undo this change using the 'undo_settings_change' tool.";
                }

                ResultTextBlock.Text = completeOutput;
                ResultPanel.Visibility = Visibility.Visible;
            }
            else
            {
                var completeOutput = $"🔧 Tool Call:\n{toolCallJson}\n\n";
                completeOutput += "📋 Response:\nNo content returned from the tool.";
                ResultTextBlock.Text = completeOutput;
                ResultPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to execute tool '{selectedTool.Name}': {ex.Message}\n\nStack trace: {ex.StackTrace}");
        }
        finally
        {
            ExecuteToolButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }
}
