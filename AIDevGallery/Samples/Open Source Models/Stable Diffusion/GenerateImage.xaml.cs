// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Samples.SharedCode.StableDiffusionCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.StableDiffusionImageGeneration
{
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
        NugetPackageReferences = [
            "MathNet.Numerics",
            "NumSharp",
            "System.Drawing.Common",
            "Microsoft.ML.OnnxRuntime.Extensions",
            "Microsoft.ML.OnnxRuntime.DirectML"
        ],
        Icon = "\uEE71")]

    internal sealed partial class GenerateImage : Page
    {
        private string prompt = string.Empty;
        private bool modelReady;
        private CancellationTokenSource cts = new();
        private StableDiffusion? stableDiffusion;
        private bool isCanceling;
        private Task? inferenceTask;

        public GenerateImage()
        {
            this.Unloaded += (s, e) => CleanUp();
            this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is SampleNavigationParameters sampleParams)
            {
                var hardwareAccelerator = sampleParams.HardwareAccelerator;
                var parentFolder = sampleParams.ModelPath;

                await Task.Run(() =>
                {
                    stableDiffusion = new StableDiffusion(parentFolder, hardwareAccelerator);
                });

                modelReady = true;

                sampleParams.NotifyCompletion();
            }
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
            if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && InputBox.Text.Length > 0)
            {
                await DoStableDiffusion();
            }
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

            GenerateButton.Content = "Stop";

            prompt = InputBox.Text;

            Loader.IsActive = true;
            Loader.Visibility = Visibility.Visible;
            DefaultImage.Visibility = Visibility.Collapsed;
            InputBox.IsEnabled = false;

            CancellationToken token = CancelGenerationAndGetNewToken();

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
                        this.DispatcherQueue.TryEnqueue(async () =>
                        {
                            ErrorDialog.CloseButtonText = "OK";
                            ErrorDialog.Title = "Error";
                            ErrorDialog.Content = ex.Message;
                            await ErrorDialog.ShowAsync();
                        });
                    }

                    return Task.CompletedTask;
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
    }
}