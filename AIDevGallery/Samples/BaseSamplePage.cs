// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Samples;

internal partial class BaseSamplePage : Page
{
    private BaseSampleNavigationParameters? SampleParams { get; set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadSample(e.Parameter as BaseSampleNavigationParameters);
    }

    private async Task LoadSample(BaseSampleNavigationParameters? parameter)
    {
        if (parameter is SampleNavigationParameters sampleParams)
        {
            SampleParams = sampleParams;
            await LoadModelAsync(sampleParams);
        }
        else if (parameter is MultiModelSampleNavigationParameters sampleParams2)
        {
            SampleParams = sampleParams2;
            await LoadModelAsync(sampleParams2);
        }
    }

    protected virtual Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        return Task.CompletedTask;
    }

    protected virtual Task LoadModelAsync(MultiModelSampleNavigationParameters sampleParams)
    {
        return Task.CompletedTask;
    }

    internal void SendSampleInteractedEvent(string? customInfo = null)
    {
        SampleParams?.SendSampleInteractionEvent(customInfo);
    }

    internal async void ShowException(Exception ex, string? optionalMessage = null)
    {
        var msg = optionalMessage ?? ex switch
        {
            COMException
                when ex.Message.Contains("the rpc server is unavailable", StringComparison.CurrentCultureIgnoreCase) =>
                    "The WCL is in an unstable state.\nRebooting the machine will restart the WCL.",
            _ => $"Error:\n{ex.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}"
        };
        var contenText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 25, 10, 10),
            Text = msg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var copyTextButton = new Button
        {
            Content = new FontIcon
            {
                FontSize = 16,
                Glyph = "\uF0E3"
            },
            Tag = (ex, optionalMessage)
        };
        ToolTipService.SetToolTip(copyTextButton, "Copy exception info to clipboard");
        copyTextButton.Click += CopyText_Click;
        var copyTextStack = new StackPanel
        {
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        copyTextStack.Children.Add(copyTextButton);
        var contentControl = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = 260,
            Height = 180
        };
        contentControl.Children.Add(contenText);
        contentControl.Children.Add(copyTextStack);
        ContentDialog exceptionDialog = new()
        {
            Title = "Error",
            Content = contentControl,
            PrimaryButtonText = "OK",
            XamlRoot = App.MainWindow.Content.XamlRoot,
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
        };
        if (SampleParams != null)
        {
            exceptionDialog.SecondaryButtonText = "Reload Sample";
        }

        var result = await exceptionDialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            if (SampleParams is BaseSampleNavigationParameters sampleParams)
            {
                sampleParams.SampleLoadedCompletionSource = new();
            }

            await LoadSample(SampleParams);
        }
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is (Exception, string))
        {
            var (ex, optionalMessage) = ((Exception, string))button.Tag;
            CopyExceptionToClipboard(ex, optionalMessage);
        }
    }

    public void CopyExceptionToClipboard(Exception ex, string optionalMessage)
    {
        string exceptionDetails = (string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n") +
            GetExceptionDetails(ex);

        DataPackage dataPackage = new DataPackage();
        dataPackage.SetText(exceptionDetails);

        Clipboard.SetContent(dataPackage);
    }

    private string GetExceptionDetails(Exception ex)
    {
        var innerExceptionData = ex.InnerException == null ? string.Empty :
            $"Inner Exception:\n{GetExceptionDetails(ex.InnerException)}";
        string details = $@"Type: {ex.GetType().Name}
Message: {ex.Message}
StackTrace: {ex.StackTrace}
{innerExceptionData}";
        return details;
    }
}