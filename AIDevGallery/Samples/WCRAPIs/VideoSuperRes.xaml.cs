// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using CommunityToolkit.WinUI.Controls;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AI.Video;
using Microsoft.Windows.AI.Video.Projection;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Video Super Resolution",
    Model1Types = [ModelType.VideoSuperRes],
    Scenario = ScenarioType.AudioAndVideoVideoSuperResolution,
    NugetPackageReferences = [
        "CommunityToolkit.WinUI.Controls.CameraPreview",
        "System.Drawing.Common"
    ],
    Id = "c3252e18-1d47-4689-adae-78fc66968650",
    Icon = "\uE714")]
internal sealed partial class VideoSuperRes : BaseSamplePage
{
    private readonly SemaphoreSlim _frameProcessingLock = new(1, 1);
    private bool _isProcessing;
    private VideoScaler? _videoScaler;
    private IDirect3DSurface? _outputD3dSurface;
    private int _outputWidth;
    private int _outputHeight;
    private int _originalImageWidth = 1280;
    private int _originalImageHeight = 720;

    public VideoSuperRes()
    {
        this.Unloaded += VideoSuperResUnloaded;
        this.InitializeComponent();
    }

    private async void VideoSuperResUnloaded(object sender, RoutedEventArgs e)
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
        try
        {
            await InitializeCameraPreviewControl();

            var catalog = ExecutionProviderCatalog.GetDefault();
            await catalog.EnsureAndRegisterCertifiedAsync();

            var readyState = VideoScaler.GetReadyState();
            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
            {
                ShowException(null, "Video Scaler is not supported on current system (hardware requirements not met).");
            }

            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await VideoScaler.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, "Video Scaler is not available.");
                }
            }

            _videoScaler = await VideoScaler.CreateAsync();
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load model.");
        }

        sampleParams.NotifyCompletion();
    }

    private async Task InitializeCameraPreviewControl()
    {
        var cameraHelper = CameraPreviewControl.CameraHelper;
        CameraPreviewControl.PreviewFailed += CameraPreviewControl_PreviewFailed!;
        await CameraPreviewControl.StartAsync(cameraHelper!);
        CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived!;
        CameraPreviewControl.SizeChanged += CameraPreviewControl_SizeChanged;
    }

    private void CameraPreviewControl_PreviewFailed(object sender, PreviewFailedEventArgs e)
    {
        // Handle preview failure
    }

    private async void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs e)
    {
        if (e.VideoFrame?.SoftwareBitmap == null || _isProcessing)
        {
            return;
        }

        if (!_frameProcessingLock.Wait(0))
        {
            return;
        }

        try
        {
            _isProcessing = true;
            await ProcessFrame(e.VideoFrame);
        }
        finally
        {
            _isProcessing = false;
            _frameProcessingLock.Release();
        }
    }

    private async Task ProcessFrame(VideoFrame videoFrame)
    {
        // Process the frame with super resolution model
        var processedBitmap = await Task.Run(async () =>
        {
            int width = 0;
            int height = 0;
            var inputD3dSurface = videoFrame.Direct3DSurface;
            if (inputD3dSurface != null)
            {
                Debug.Assert(inputD3dSurface.Description.Format == Windows.Graphics.DirectX.DirectXPixelFormat.NV12, "input in NV12 format");
                width = inputD3dSurface.Description.Width;
                height = inputD3dSurface.Description.Height;
            }
            else
            {
                var softwareBitmap = videoFrame.SoftwareBitmap;
                if (softwareBitmap == null)
                {
                    return null;
                }

                Debug.Assert(softwareBitmap.BitmapPixelFormat == BitmapPixelFormat.Nv12, "input in NV12 format");

                width = softwareBitmap.PixelWidth;
                height = softwareBitmap.PixelHeight;
            }

            try
            {
                if (inputD3dSurface == null)
                {
                    // Create Direct3D11-backed VideoFrame for input
                    using var inputVideoFrame = VideoFrame.CreateAsDirect3D11SurfaceBacked(
                        Windows.Graphics.DirectX.DirectXPixelFormat.NV12,
                        width,
                        height);

                    if (inputVideoFrame.Direct3DSurface == null)
                    {
                        return null;
                    }

                    // Copy the software bitmap to the Direct3D-backed frame
                    await videoFrame.CopyToAsync(inputVideoFrame);

                    inputD3dSurface = inputVideoFrame.Direct3DSurface;
                }

                // Create or resize output surface (BGRA8 format for display)
                if (_outputD3dSurface == null || _outputWidth != width || _outputHeight != height)
                {
                    _outputD3dSurface?.Dispose();

                    // DXGI_FORMAT_B8G8R8A8_UNORM = 87
                    _outputD3dSurface = Direct3DExtensions.CreateDirect3DSurface(87, width, height);
                    _outputWidth = width;
                    _outputHeight = height;
                }

                // Scale the frame using VideoScaler
                var result = _videoScaler!.ScaleFrame(inputD3dSurface, _outputD3dSurface, new VideoScalerOptions());

                if (result.Status == ScaleFrameStatus.Success)
                {
                    var outputBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                        _outputD3dSurface,
                        BitmapAlphaMode.Premultiplied);

                    return outputBitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessFrame error: {ex.Message}");
            }

            return null;
        });

        if (processedBitmap == null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            using (processedBitmap)
            {
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(processedBitmap);
                ProcessedVideoImage.Source = source;
            }
        });
    }

    private void CameraPreviewControl_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateCameraPreviewSize();
    }

    private void UpdateCameraPreviewSize()
    {
        var ratio = _originalImageWidth / (float)_originalImageHeight;
        var container = (Grid)CameraPreviewControl.Parent;

        if (container.ActualWidth / container.ActualHeight > ratio)
        {
            CameraPreviewControl.Width = container.ActualHeight * ratio;
            CameraPreviewControl.Height = container.ActualHeight;
        }
        else
        {
            CameraPreviewControl.Width = container.ActualWidth;
            CameraPreviewControl.Height = container.ActualWidth / ratio;
        }
    }
}