// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.OpenSourceModels.MultimodalModels
{
    [GallerySample(
        Model1Types = [ModelType.MultimodalModels],
        Scenario = ScenarioType.ImageDescribeImage,
        Id = "58c3565d-dbe0-46c7-accc-2c088db2bdf9",
        Icon = "\uE8D4",
        NugetPackageReferences = [
            "Microsoft.ML.OnnxRuntimeGenAI.DirectML"
        ],
        Name = "Describe Image")]
    internal sealed partial class DescribeImage : BaseSamplePage
    {
        private Model? model;
        private MultiModalProcessor? processor;
        private TokenizerStream? tokenizerStream;
        private CancellationTokenSource? _cts;

        public DescribeImage()
        {
            this.InitializeComponent();

            this.Unloaded += (sender, args) => Dispose();
        }

        protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
        {
            await InitModel(sampleParams.ModelPath, sampleParams.CancellationToken);
            sampleParams.NotifyCompletion();

            if (!sampleParams.CancellationToken.IsCancellationRequested)
            {
                await LoadAndDescribeImage(await StorageFile.GetFileFromPathAsync(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + "\\Assets\\team.jpg"));
            }
        }

        private async Task InitModel(string modelPath, CancellationToken ct)
        {
            await Task.Run(
                () =>
                {
                    try
                    {
                        model = new Model(modelPath);
                        ct.ThrowIfCancellationRequested();

                        processor = new MultiModalProcessor(model);
                        ct.ThrowIfCancellationRequested();

                        tokenizerStream = processor.CreateStream();
                        ct.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Dispose();
                    }
                },
                ct);
        }

        private void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            tokenizerStream?.Dispose();
            processor?.Dispose();
            model?.Dispose();
        }

        private static async Task<StorageFile> PickFileAsync()
        {
            var window = new Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            var picker = new FileOpenPicker();

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".jpg");

            picker.ViewMode = PickerViewMode.Thumbnail;

            var file = await picker.PickSingleFileAsync();
            return file;
        }

        private async IAsyncEnumerable<string> InferStreaming(string question, string imagePath, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (model == null || processor == null || tokenizerStream == null)
            {
                yield break;
            }

            var images = Images.Load(imagePath);

            var prompt = $@"<|user|>\n<|image_1|>\n{question}<|end|>\n<|assistant|>\n";
            string[] stopTokens = ["</s>", "<|user|>", "<|end|>", "<|assistant|>"];

            var inputTensors = processor.ProcessImages(prompt, images);

            using GeneratorParams generatorParams = new(model);
            generatorParams.SetSearchOption("max_length", 2500);
            generatorParams.SetInputs(inputTensors);

            using var generator = new Generator(model, generatorParams);
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();

                await Task.Delay(0, ct).ConfigureAwait(false);

                generator.ComputeLogits();
                ct.ThrowIfCancellationRequested();

                generator.GenerateNextToken();
                ct.ThrowIfCancellationRequested();

                var part = tokenizerStream.Decode(generator.GetSequence(0)[^1]);

                if (stopTokens.Contains(part))
                {
                    break;
                }

                ct.ThrowIfCancellationRequested();
                yield return part;
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                SelectImageButton.IsEnabled = false;
                ButtonTextBlock.Text = "Canceling";
                return;
            }

            var file = await PickFileAsync();
            if (file != null)
            {
                SelectImageButton.Focus(FocusState.Programmatic);
                await LoadAndDescribeImage(file);
            }
        }

        private async Task LoadAndDescribeImage(StorageFile file)
        {
            ButtonTextBlock.Text = "Cancel";
            ToolTipService.SetToolTip(SelectImageButton, "Cancel generation");
            AutomationProperties.SetName(SelectImageButton, "Cancel generation");
            Output.Text = string.Empty;
            Loader.IsActive = true;
            Loader.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();

            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapImage bitmapImage = new();
                await bitmapImage.SetSourceAsync(fileStream);
                DefaultImage.Source = bitmapImage;
                NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload"); // <exclude-line>
            }

            await Task.Run(async () =>
            {
                try
                {
                    await foreach (var part in InferStreaming("What is this image", file.Path, _cts.Token))
                    {
                        DispatcherQueue.TryEnqueue(() => Output.Text += part);
                    }
                }
                catch
                {
                }
            });

            ResetState();
        }

        private void ResetState()
        {
            _cts = null;
            ButtonTextBlock.Text = "Select image";
            ToolTipService.SetToolTip(SelectImageButton, "Select image"); // <exclude-line>
            AutomationProperties.SetName(SelectImageButton, "Select image"); // <exclude-line>
            SelectImageButton.IsEnabled = true;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
        }
    }
}