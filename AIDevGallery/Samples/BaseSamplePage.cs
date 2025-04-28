// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    internal void ShowException(Exception? ex, string? optionalMessage = null)
    {
        var msg = optionalMessage ?? ex switch
        {
            COMException
                when ex.Message.Contains("the rpc server is unavailable", StringComparison.CurrentCultureIgnoreCase) =>
                    "The WCL is in an unstable state.\nRebooting the machine will restart the WCL.",
            _ => $"Error:\n{ex?.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}"
        };

        this.DispatcherQueue.TryEnqueue(async () =>
        {
            var errorText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = msg,
                IsTextSelectionEnabled = true,
            };

            ContentDialog exceptionDialog = new()
            {
                Title = "Something went wrong",
                Content = errorText,
                PrimaryButtonText = "Copy error details",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                CloseButtonText = "Close",
                PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
            };

            if (SampleParams != null)
            {
                exceptionDialog.SecondaryButtonText = "Reload";
            }

            var result = await exceptionDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                CopyExceptionToClipboard(ex, optionalMessage);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                if (SampleParams is BaseSampleNavigationParameters sampleParams)
                {
                    sampleParams.SampleLoadedCompletionSource = new();
                }

                await LoadSample(SampleParams);
            }
        });

    }

    public void CopyExceptionToClipboard(Exception? ex, string? optionalMessage)
    {
        string exceptionDetails = string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n";

        if (ex != null)
        {
          exceptionDetails += GetExceptionDetails(ex);
        }

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