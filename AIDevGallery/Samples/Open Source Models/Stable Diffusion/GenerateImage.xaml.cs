// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Samples.SharedCode.StableDiffusionCode;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.OpenSourceModels.StableDiffusionImageGeneration;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
[GallerySample(
    Model1Types = [ModelType.StableDiffusion],
    Scenario = ScenarioType.ImageGenerateImage,
    Name = "Generate Image",
    Id = "1574f6ad-d7ba-49f8-bd57-34e0d98ce4e1",
    SharedCode = [
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.LMSDiscreteScheduler,
        SharedCodeEnum.SafetyChecker,
        SharedCodeEnum.TensorHelper,
        SharedCodeEnum.TextProcessing,
        SharedCodeEnum.StableDiffusion,
        SharedCodeEnum.VaeDecoder,
        SharedCodeEnum.StableDiffusionConfig,
        SharedCodeEnum.Prediction,
        SharedCodeEnum.DeviceUtils
    ],
    AssetFilenames = [
        "cliptokenizer.onnx"
    ],
    NugetPackageReferences = [
        "MathNet.Numerics",
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.Extensions",
        "Microsoft.Windows.AI.MachineLearning"
    ],
    Icon = "\uEE71")]

internal sealed partial class GenerateImage : BaseSamplePage
{
    private string prompt = string.Empty;
    private bool modelReady;
    private CancellationTokenSource cts = new();
    private StableDiffusion? stableDiffusion;
    private bool isCanceling;
    private Task? inferenceTask;

    private bool isImeActive = true;

    public GenerateImage()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var parentFolder = sampleParams.ModelPath;

        var policy = sampleParams.WinMlSampleOptions.Policy;
        var device = sampleParams.WinMlSampleOptions.Device;
        bool compileOption = sampleParams.WinMlSampleOptions.CompileModel;

        try
        {
            stableDiffusion = new StableDiffusion(parentFolder);
            await stableDiffusion.InitializeAsync(policy, device, compileOption);
        }
        catch(Exception ex)
        {
            ShowException(ex);
        }

        modelReady = true;

        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        InputBox.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private void CleanUp()
    {
        cts?.Cancel();
        cts?.Dispose();
        stableDiffusion?.Dispose();
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        await DoStableDiffusion();
    }

    private async void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && InputBox.Text.Length > 0 && isImeActive == false)
        {
            await DoStableDiffusion();
        }

        isImeActive = true;
    }

    private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        isImeActive = false;
    }

    private async Task DoStableDiffusion()
    {
        if (!modelReady || isCanceling)
        {
            return;
        }

        if (inferenceTask != null)
        {
            cts.Cancel();
            isCanceling = true;
            GenerateButton.Content = "Canceling...";
            await inferenceTask;
            isCanceling = false;
            return;
        }

        SaveButton.IsEnabled = false;
        GenerateButton.Content = "Stop";

        prompt = InputBox.Text;

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        DefaultImage.Visibility = Visibility.Collapsed;
        InputBox.IsEnabled = false;

        CancellationToken token = CancelGenerationAndGetNewToken();
        SendSampleInteractedEvent("GenerateImage"); // <exclude-line>

        inferenceTask = Task.Run(
            () =>
            {
                try
                {
                    if (stableDiffusion!.Inference(prompt, token) is Bitmap image)
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            BitmapImage bitmapImage = BitmapFunctions.ConvertBitmapToBitmapImage(image);
                            DefaultImage.Source = bitmapImage;
                            SaveButton.IsEnabled = true;
                            NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new image generated."); // <exclude-line>
                            DefaultImage.Visibility = Visibility.Visible;
                        });
                    }
                    else
                    {
                        throw new ArgumentException("The inference did not return a valid image.");
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                    {
                        this.DispatcherQueue.TryEnqueue(async () =>
                        {
                            ErrorDialog.CloseButtonText = "OK";
                            ErrorDialog.Title = "Error";
                            TextBlock errorTextBlock = new TextBlock()
                            {
                                Text = ex.Message,
                                IsTextSelectionEnabled = true,
                                TextWrapping = TextWrapping.WrapWholeWords
                            };
                            ErrorDialog.Content = errorTextBlock;
                            await ErrorDialog.ShowAsync();
                        });
                    }
                }

                this.DispatcherQueue.TryEnqueue(() => GenerateButton.Content = "Generate");
            },
            token);

        await inferenceTask;
        inferenceTask = null;

        Loader.IsActive = false;
        Loader.Visibility = Visibility.Collapsed;
        InputBox.IsEnabled = true;
        NarratorHelper.Announce(DefaultImage, "Image has finished generating.", "SDDoneAnnouncementActivityId"); // <exclude-line>
    }

    private void CloseButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs e)
    {
        sender.Hide();
        DefaultImage.Visibility = Visibility.Visible;
        GenerateButton.Content = "Generate";
        InputBox.IsEnabled = true;
    }

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(new Window());
        FileSavePicker picker = new FileSavePicker
        {
           SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedFileName = "image.png";
        picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });

        StorageFile file = await picker.PickSaveFileAsync();

        if(file != null && DefaultImage.Source != null)
        {
            SendSampleInteractedEvent("SaveFile"); // <exclude-line>
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(DefaultImage);

            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();

            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, fileStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)renderTargetBitmap.PixelWidth, (uint)renderTargetBitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
        }
    }
}