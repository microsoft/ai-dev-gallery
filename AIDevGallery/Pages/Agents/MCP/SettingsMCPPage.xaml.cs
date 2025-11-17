// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Pages;

internal sealed partial class SettingsMCPPage : Page
{
    private const string SettingsServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server"; // const first (SA1203)
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // CA1869 cache
    private McpClient? mcpClient;
    private McpClientTool? selectedTool;

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

            // Create button-style cards for each tool
            var toolButtons = new List<UIElement>();
            foreach (var tool in tools)
            {
                // Create a button that looks like a card
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 8),
                    Tag = tool,
                    Style = (Style)Application.Current.Resources["DefaultButtonStyle"]
                };

                button.Click += ToolButton_Click;

                var cardContent = new StackPanel();

                // Tool name
                var toolNameText = new TextBlock
                {
                    Text = tool.Name,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                };
                cardContent.Children.Add(toolNameText);

                // Tool description
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
                    cardContent.Children.Add(description);
                }

                button.Content = cardContent;
                toolButtons.Add(button);
            }

            ToolsList.ItemsSource = toolButtons;
            ToolsPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load tools: {ex.Message}");
        }
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
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

        // Reset all buttons to default style
        if (ToolsList.ItemsSource is List<UIElement> toolElements)
        {
            foreach (var element in toolElements)
            {
                if (element is Button btn)
                {
                    btn.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                    btn.BorderThickness = new Thickness(1);
                    btn.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                }
            }
        }

        // Highlight selected button with subtle accent border
        button.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        button.BorderThickness = new Thickness(2);
        button.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

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

    // Removed RequiresDynamicCode to avoid IL3050; JSON formatting acceptable under trimming/AOT assumptions.
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
            var arguments = new Dictionary<string, object?>();

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

            // Show a loading indicator
            ResultTextBlock.Text = $"Executing {selectedTool.Name}...";
            ResultPanel.Visibility = Visibility.Visible;

            // Call the tool with the prepared arguments
            var result = await mcpClient.CallToolAsync(selectedTool.Name, arguments);

            // Display the raw result as JSON (similar to SystemInfoMCP page)
            var resultJson = JsonSerializer.Serialize(result, IndentedJsonOptions);

            // Add special note for make_settings_change to highlight UndoId
            var displayText = resultJson;
            if (selectedTool.Name == "make_settings_change" && resultJson.Contains("UndoId"))
            {
                displayText += "IMPORTANT: Save the UndoId from the response above!\n" +
                              "You'll need it to undo this change using the 'undo_settings_change' tool.";
            }

            ResultTextBlock.Text = displayText;
            ResultPanel.Visibility = Visibility.Visible;
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