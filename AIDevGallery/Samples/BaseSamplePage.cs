// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AIDevGallery.Samples;

internal partial class BaseSamplePage : Page
{
    private BaseSampleNavigationParameters? SampleParams { get; set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.Current.UnhandledException += Current_UnhandledException;
        await LoadSample(e.Parameter as BaseSampleNavigationParameters);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        App.Current.UnhandledException -= Current_UnhandledException;
    }

    private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ShowException(e.Exception, null);
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
        ContentDialog exceptionDialog = new()
        {
            Title = "Error",
            Content = msg,
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
}