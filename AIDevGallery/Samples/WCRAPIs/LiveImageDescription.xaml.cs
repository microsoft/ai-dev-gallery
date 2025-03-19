// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using CommunityToolkit.WinUI.Controls;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
Name = "Describe Live Image WCR",
Model1Types = [ModelType.ImageDescription],
Scenario = ScenarioType.ImageDescribeLiveImage,
NugetPackageReferences =
[
    "CommunityToolkit.WinUI.Helpers",
    "CommunityToolkit.WinUI.Controls.CameraPreview",
    "Microsoft.Graphics.Win2D"
],
Id = "a1b1f64f-bc57-41a3-8fb3-ac8f1536d799",
Icon = "\uEE6F")]

internal sealed partial class LiveImageDescription : BaseSamplePage
{
    private ImageDescriptionGenerator? _imageDescriptor;
    private CancellationTokenSource? _cts;
    private bool stopped;

    public LiveImageDescription()
    {
        this.Unloaded += LiveImageDescriptionUnloaded;
        this.InitializeComponent();
    }

    private async void LiveImageDescriptionUnloaded(object sender, RoutedEventArgs e)
    {
        lock (this)
        {
            CameraPreviewControl.CameraHelper.FrameArrived -= CameraPreviewControl_FrameArrived!;
            CameraPreviewControl.PreviewFailed -= CameraPreviewControl_PreviewFailed!;
            CameraPreviewControl.Stop();
        }

        await CameraPreviewControl.CameraHelper.CleanUpAsync();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (!ImageDescriptionGenerator.IsAvailable())
        {
            var operation = await ImageDescriptionGenerator.MakeAvailableAsync();

            if (operation.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                // TODO: handle error
            }
        }

        // Load camera
        this.InitializeCameraPreviewControl();

        sampleParams.NotifyCompletion();
    }

    private async void InitializeCameraPreviewControl()
    {
        var cameraHelper = CameraPreviewControl.CameraHelper;

        CameraPreviewControl.PreviewFailed += CameraPreviewControl_PreviewFailed!;
        await CameraPreviewControl.StartAsync(cameraHelper!);
        CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived!;
    }

    private void CameraPreviewControl_PreviewFailed(object sender, PreviewFailedEventArgs e)
    {
        var errorMessage = e.Error;
    }

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private async void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs e)
    {
        if (e.VideoFrame?.SoftwareBitmap == null || stopped)
        {
            return;
        }

        SoftwareBitmap keyFrame = e.VideoFrame!.SoftwareBitmap;
        keyFrame = SoftwareBitmap.Convert(keyFrame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        await DescribeImageAsync(keyFrame, ImageDescriptionScenario.DetailedNarration);
    }

    private async Task DescribeImageAsync(SoftwareBitmap bitmap, ImageDescriptionScenario scenario)
    {
        if (stopped || !_semaphore.Wait(0))
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        DispatcherQueue?.TryEnqueue(() =>
        {
            Loader.Visibility = Visibility.Visible;
            StopBtn.IsEnabled = true;
        });

        var isFirstWord = true;
        try
        {
            using var bitmapBuffer = ImageBuffer.CreateCopyFromBitmap(bitmap);
            _imageDescriptor ??= await ImageDescriptionGenerator.CreateAsync();

            var describeTask = _imageDescriptor.DescribeAsync(bitmapBuffer, scenario);

            if (describeTask != null)
            {
                describeTask.Progress += (asyncInfo, delta) =>
                {
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (isFirstWord)
                        {
                            Loader.Visibility = Visibility.Collapsed;
                            ResponseTxt.Visibility = Visibility.Visible;
                            isFirstWord = false;
                        }

                        if (!stopped)
                        {
                            ResponseTxt.Text = delta;
                        }
                    });
                    if (_cts?.IsCancellationRequested == true && stopped)
                    {
                        describeTask.Cancel();
                    }
                };

                await describeTask.AsTask();
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ResponseTxt.Text = ex.Message;
            });
        }
        finally
        {
            _semaphore.Release();
            if (stopped)
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    StopBtn.IsEnabled = true;
                    ToggleButtonText.Text = "Start";
                    ToggleIcon.Glyph = "\uF5B0";
                });
            }
        }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!stopped)
        {
            _cts?.Cancel();
            StopBtn.IsEnabled = false;
            ToggleButtonText.Text = "Stopping...";
            ToggleIcon.Glyph = "\uE73B";
        }
        else
        {
            ToggleButtonText.Text = "Stop";
            ToggleIcon.Glyph = "\uE73B";
            ResponseTxt.Text = string.Empty;
        }

        stopped = !stopped;
    }
}