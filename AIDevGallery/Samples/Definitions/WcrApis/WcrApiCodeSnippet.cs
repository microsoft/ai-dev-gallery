// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System.Collections.Generic;

namespace AIDevGallery.Samples;

internal static class WcrApiCodeSnippet
{
    public static readonly Dictionary<ModelType, string> Snippets = new Dictionary<ModelType, string>
    {
        {
            ModelType.PhiSilica, """"
            using Microsoft.Windows.AI;
            using Microsoft.Windows.AI.Text;

            var readyState = LanguageModel.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await LanguageModel.EnsureReadyAsync();
                }

                using LanguageModel languageModel = await LanguageModel.CreateAsync();

                string prompt = "Tell me a short story";

                var result = languageModel.GenerateResponseAsync(prompt);

                result.Progress += (sender, args) =>
                {
                    Console.Write(args);
                };

                await result;
            }
            """"
        },
        {
            ModelType.PhiSilicaLora, """"
            using Microsoft.Windows.AI;
            using Microsoft.Windows.AI.Text;
            using Microsoft.Windows.AI.Text.Experimental;

            var readyState = LanguageModel.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await LanguageModel.EnsureReadyAsync();
                }

                using LanguageModel languageModel = await LanguageModel.CreateAsync();
                using LanguageModelExperimental loraModel = new LanguageModelExperimental(languageModel);

                string adapterFilePath = "path_to_your_adapter_file";
                LowRankAdaptation loraAdapter = loraModel.LoadAdapter(adapterFilePath);

                var options = new LanguageModelOptionsExperimental
                {
                    LoraAdapter = loraAdapter
                };

                string prompt = "Provide the molecular formula for glucose.";

                var result = await loraModel.GenerateResponseAsync(prompt, options);

                Console.WriteLine(result.Text);
            }
            """"
        },
        {
            ModelType.TextRecognitionOCR, """"
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;

            var readyState = TextRecognizer.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await TextRecognizer.EnsureReadyAsync();
                }

                using TextRecognizer textRecognizer = await TextRecognizer.CreateAsync();

                ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);
                RecognizedText? result = textRecognizer?.RecognizeTextFromImage(imageBuffer);

                Console.WriteLine(string.Join("\n", result.Lines.Select(l => l.Text)));
            }
            """"
        },
        {
            ModelType.ImageScaler, """"
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;
            using Windows.Graphics.Imaging;

            var readyState = ImageScaler.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageScaler.EnsureReadyAsync();
                }

                ImageScaler imageScaler = await ImageScaler.CreateAsync();
                SoftwareBitmap finalImage = imageScaler.ScaleSoftwareBitmap(softwareBitmap, targetWidth, targetHeight);
            }
            """"
        },
        {
            ModelType.BackgroundRemover, """"
            using Microsoft.Windows.AI;
            using Microsoft.Windows.AI.Imaging;
            using Windows.Graphics;
            using Windows.Graphics.Imaging;

            var readyState = ImageObjectExtractor.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageObjectExtractor.EnsureReadyAsync();
                }

                ImageObjectExtractor imageObjectExtractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

                ImageObjectExtractorHint hint = new
                (
                    includeRects: null,
                    includePoints: new List<PointInt32> { new PointInt32(306, 212), new PointInt32(216, 336) },
                    excludePoints: null
                );

                SoftwareBitmap finalImage = imageObjectExtractor.GetSoftwareBitmapObjectMask(hint);
            }
            """"
        },
        {
            ModelType.ObjectRemover, """"
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;
            using Windows.Graphics.Imaging;
            using System.Runtime.InteropServices.WindowsRuntime;
            using Windows.Graphics;

            var readyState = ImageObjectRemover.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageObjectRemover.EnsureReadyAsync();
                }

                readyState = ImageObjectRemover.GetReadyState();

                ImageObjectRemover imageObjectRemover = await ImageObjectRemover.CreateAsync();

                // Create a bitmap mask from the foreground rectangle - Gray Image with white rectangle
                // The white rectangle is the area to be removed
                var maskBitmap = CreateMaskFromRect(softwareBitmap.PixelWidth, softwareBitmap.PixelHeight, rect);

                SoftwareBitmap finalImage = imageObjectRemover.RemoveFromSoftwareBitmap(softwareBitmap, maskBitmap);
            }

            // example of creating a mask from a rectangle
            SoftwareBitmap CreateMaskFromRect(int width, int height, RectInt32 rect)
            {
                byte[] bitmapBuffer = new byte[width * height]; // Gray image hence 1-Byte per pixel.

                for (var row = rect.Y; row < rect.Y + rect.Height; row++)
                {
                    for (var col = rect.X; col < rect.X + rect.Width; col++)
                    {
                        bitmapBuffer[row * width + col] = 255;
                    }
                }

                SoftwareBitmap bitmap = new SoftwareBitmap(BitmapPixelFormat.Gray8, width, height, BitmapAlphaMode.Ignore);
                bitmap.CopyFromBuffer(bitmapBuffer.AsBuffer());
                return bitmap;
            }
            
            """"
        },
        {
            ModelType.ImageDescription, """"
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.ContentSafety;
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;

            var readyState = ImageDescriptionGenerator.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageDescriptionGenerator.EnsureReadyAsync();
                }

                ImageDescriptionGenerator imageDescriptionGenerator = await ImageDescriptionGenerator.CreateAsync();

                ImageBuffer inputImage = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

                ContentFilterOptions filterOptions = new ContentFilterOptions();
                filterOptions.PromptMaxAllowedSeverityLevel.Violent = SeverityLevel.Medium;
                filterOptions.ResponseMaxAllowedSeverityLevel.Violent = SeverityLevel.Medium;

                ImageDescriptionResult languageModelResponse = await imageDescriptionGenerator.DescribeAsync(inputImage, ImageDescriptionKind.DiagramDescription, filterOptions);

                Console.WriteLine(languageModelResponse.Description);
            }
            """"
        }
    };
}