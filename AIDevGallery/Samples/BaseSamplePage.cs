// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;

namespace AIDevGallery.Samples;

internal partial class BaseSamplePage : Page
{
    private BaseSampleNavigationParameters? SampleParams { get; set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is SampleNavigationParameters sampleParams)
        {
            SampleParams = sampleParams;
            await LoadModelAsync(sampleParams);
        }
        else if (e.Parameter is MultiModelSampleNavigationParameters sampleParams2)
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
}