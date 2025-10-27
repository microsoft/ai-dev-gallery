// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AIDevGallery.Pages;

internal sealed partial class FileOperationsMCPPage : Page
{
    private const string FileServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server";
    private readonly Dictionary<string, FrameworkElement> currentParameterInputs = new();
    private McpClient? mcpClient;
    private McpClientTool? selectedTool;

    public FileOperationsMCPPage()
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

            // Create transport options for odr.exe with proxy to File server
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "File-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    FileServerId
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
            ShowError($"Failed to connect to File MCP Server. Make sure you're running on Windows MCP Private Preview 3 or later with odr.exe available.\n\nError: {ex.Message}");
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
            if (SelectedToolPanel != null)
            {
                SelectedToolPanel.Visibility = Visibility.Collapsed;
            }

            if (ExecuteToolButton != null)
            {
                ExecuteToolButton.Visibility = Visibility.Collapsed;
                ExecuteToolButton.IsEnabled = false;
            }

            if (SelectedToolNameText != null)
            {
                SelectedToolNameText.Text = string.Empty;
            }

            if (SelectedToolDescriptionText != null)
            {
                SelectedToolDescriptionText.Text = string.Empty;
            }

            ToolsList.ItemsSource = null;
            selectedTool = null;
            currentParameterInputs.Clear();
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
                ShowError("No tools found in File MCP Server.");
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

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            ShowError($"Sender is not a button. Sender type: {sender?.GetType().Name ?? "null"}");
            return new ValueTask();
        }

        if (button.Tag is not McpClientTool tool)
        {
            ShowError($"Button tag is not a McpClientTool object. Tag type: {button.Tag?.GetType().Name ?? "null"}, Tag value: {button.Tag}");
            return new ValueTask();
        }

        if (mcpClient == null)
        {
            ShowError("MCP Client is not connected.");
            return new ValueTask();
        }

        try
        {
            button.IsEnabled = false;
            ResultPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;

            // Build and show dynamic input form from tool schema
            selectedTool = tool;
            if (SelectedToolNameText != null)
            {
                SelectedToolNameText.Text = tool.Name ?? string.Empty;
            }

            if (SelectedToolDescriptionText != null)
            {
                SelectedToolDescriptionText.Text = string.IsNullOrWhiteSpace(tool.Description) ? string.Empty : tool.Description;
            }

            if (SelectedToolPanel != null)
            {
                SelectedToolPanel.Visibility = Visibility.Visible;
            }

            var hasParams = BuildDynamicFormFromSchema(tool);
            InputPanel.Visibility = hasParams ? Visibility.Visible : Visibility.Collapsed;
            if (ExecuteToolButton != null)
            {
                ExecuteToolButton.Visibility = Visibility.Visible;
                ExecuteToolButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to execute tool '{tool.Name}': {ex.Message}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private bool BuildDynamicFormFromSchema(McpClientTool tool)
    {
        // Clear previous form
        currentParameterInputs.Clear();
        DynamicFormPanel.Children.Clear();
        ExecuteToolButton.IsEnabled = true;

        try
        {
            // If schema not present, show a note and allow execute with empty args
            var schema = tool.JsonSchema; // Expected to be JsonElement
            if (schema.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // required array
            var requiredSet = new HashSet<string>(StringComparer.Ordinal);
            if (schema.TryGetProperty("required", out var requiredProp) && requiredProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        requiredSet.Add(item.GetString() ?? string.Empty);
                    }
                }
            }

            // properties object
            if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in properties.EnumerateObject())
            {
                var name = property.Name;
                var def = property.Value;
                var isRequired = requiredSet.Contains(name);

                string? type = null;
                if (def.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                {
                    type = typeProp.GetString();
                }

                string? description = null;
                if (def.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                {
                    description = descProp.GetString();
                }

                // Label
                var label = new TextBlock
                {
                    Text = isRequired ? $"{name} *" : name,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                };
                DynamicFormPanel.Children.Add(label);

                // Control selection by type and enum
                FrameworkElement inputControl;
                if (def.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array && enumProp.GetArrayLength() > 0)
                {
                    var combo = new ComboBox
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    foreach (var opt in enumProp.EnumerateArray())
                    {
                        if (opt.ValueKind == JsonValueKind.String)
                        {
                            combo.Items.Add(opt.GetString());
                        }
                        else
                        {
                            combo.Items.Add(opt.ToString());
                        }
                    }

                    inputControl = combo;
                }
                else if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase) && name.Contains("path", StringComparison.OrdinalIgnoreCase))
                {
                    // File path helper: textbox + browse button
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var pathTextBox = new TextBox
                    {
                        PlaceholderText = description ?? string.Empty
                    };
                    Grid.SetColumn(pathTextBox, 0);

                    var browseButton = new Button
                    {
                        Content = "Browse...",
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    Grid.SetColumn(browseButton, 1);

                    browseButton.Click += async (s, e2) =>
                    {
                        try
                        {
                            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                            bool pickDirectory = !string.IsNullOrEmpty(description) && description!.Contains("directory", StringComparison.OrdinalIgnoreCase);

                            if (pickDirectory)
                            {
                                var folderPicker = new FolderPicker();
                                InitializeWithWindow.Initialize(folderPicker, hwnd);
                                var folder = await folderPicker.PickSingleFolderAsync();
                                if (folder != null)
                                {
                                    pathTextBox.Text = folder.Path;
                                }
                            }
                            else
                            {
                                var filePicker = new FileOpenPicker();
                                InitializeWithWindow.Initialize(filePicker, hwnd);
                                filePicker.FileTypeFilter.Add("*");
                                var file = await filePicker.PickSingleFileAsync();
                                if (file != null)
                                {
                                    pathTextBox.Text = file.Path;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Failed to open picker: {ex.Message}");
                        }
                    };

                    row.Children.Add(pathTextBox);
                    row.Children.Add(browseButton);

                    // store the textbox as the input control for value collection
                    inputControl = pathTextBox;
                    DynamicFormPanel.Children.Add(row);
                }
                else if (string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase))
                {
                    inputControl = new CheckBox();
                }
                else
                {
                    // default to text input for string/number/integer/unknown
                    inputControl = new TextBox
                    {
                        PlaceholderText = description ?? string.Empty
                    };
                }

                currentParameterInputs[name] = inputControl;
                if (inputControl is not TextBox || !name.Contains("path", StringComparison.OrdinalIgnoreCase))
                {
                    DynamicFormPanel.Children.Add(inputControl);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    DynamicFormPanel.Children.Add(new TextBlock
                    {
                        Text = description,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                }
            }

            // If we created no inputs, hide parameters panel
            return currentParameterInputs.Count > 0;
        }
        catch (Exception ex)
        {
            DynamicFormPanel.Children.Clear();
            DynamicFormPanel.Children.Add(new TextBlock
            {
                Text = $"Failed to build parameter form: {ex.Message}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
            });
            return false;
        }
    }

    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
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

            // Validate and collect arguments according to schema
            var arguments = new Dictionary<string, object>();
            var schema = selectedTool.JsonSchema;

            var requiredSet = new HashSet<string>(StringComparer.Ordinal);
            if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var requiredProp) && requiredProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        requiredSet.Add(item.GetString() ?? string.Empty);
                    }
                }
            }

            foreach (var kvp in currentParameterInputs)
            {
                var key = kvp.Key;
                var control = kvp.Value;
                object? value = null;

                switch (control)
                {
                    case TextBox tb:
                        var text = tb.Text?.Trim() ?? string.Empty;
                        if (requiredSet.Contains(key) && string.IsNullOrEmpty(text))
                        {
                            ShowError($"Please enter a value for '{key}'.");
                            ExecuteToolButton.IsEnabled = true;
                            return;
                        }

                        if (!string.IsNullOrEmpty(text))
                        {
                            value = text;
                        }

                        break;
                    case CheckBox cb:
                        value = cb.IsChecked == true;
                        break;
                    case ComboBox combo:
                        if (combo.SelectedItem is string s)
                        {
                            value = s;
                        }
                        else if (combo.SelectedItem != null)
                        {
                            value = combo.SelectedItem.ToString();
                        }

                        if (requiredSet.Contains(key) && value == null)
                        {
                            ShowError($"Please select a value for '{key}'.");
                            ExecuteToolButton.IsEnabled = true;
                            return;
                        }

                        break;
                }

                if (value != null)
                {
                    arguments[key] = value;
                }
            }

            // Show tool call
            var toolCallJson = JsonSerializer.Serialize(
                new Dictionary<string, object>
            {
                ["tool"] = selectedTool.Name,
                ["arguments"] = arguments
            }, new JsonSerializerOptions { WriteIndented = true });

            var displayText = $"Tool Call:\n{toolCallJson}\n\n";
            displayText += $"Executing {selectedTool.Name}...\n\n";
            ResultTextBlock.Text = displayText;
            ResultPanel.Visibility = Visibility.Visible;

            // Execute
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
                    // Not JSON, keep as-is
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
            ShowError($"Failed to execute tool '{selectedTool?.Name}': {ex.Message}");
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