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
            using Microsoft.Windows.AI.Generative;
            
            var readyState = LanguageModel.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
            {
                if (readyState == AIFeatureReadyState.EnsureNeeded)
                {
                    var op = await LanguageModel.EnsureReadyAsync();
                }
            }
            
            using LanguageModel languageModel = LanguageModel.CreateAsync();
            
            string prompt = "Provide the molecular formula for glucose.";
            
            var result = await languageModel.GenerateResponseAsync(prompt);
            
            Console.WriteLine(result.Response);
            """"
        },
        {
            ModelType.TextRecognitionOCR, """"
            using Microsoft.Windows.Vision;
            using Microsoft.Graphics.Imaging;

            var readyState = TextRecognizer.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
            {
                if (readyState == AIFeatureReadyState.EnsureNeeded)
                {
                    var op = await TextRecognizer.EnsureReadyAsync();
                }
            }
            
            using TextRecognizer textRecognizer = TextRecognizer.CreateAsync();
            
            ImageBuffer imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);
            RecognizedText? result = textRecognizer?.RecognizeTextFromImage(imageBuffer, new TextRecognizerOptions());

            Console.WriteLine(string.Join("\n", result.Lines.Select(l => l.Text)));
            """"
        },
        {
            ModelType.ImageScaler, """"
            using Microsoft.Graphics.Imaging;
            using Windows.Graphics.Imaging;

            var readyState = ImageScaler.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
            {
                if (readyState == AIFeatureReadyState.EnsureNeeded)
                {
                    var op = await ImageScaler.EnsureReadyAsync();
                }
            }
            
            ImageScaler imageScaler = await ImageScaler.CreateAsync();
            SoftwareBitmap finalImage = imageScaler.ScaleSoftwareBitmap(softwareBitmap, targetWidth, targetHeight);
            """"
        },
        {
            ModelType.BackgroundRemover, """"
            using Microsoft.Graphics.Imaging;
            using Windows.Graphics.Imaging;

            var readyState = ImageObjectExtractor.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
            {
                if (readyState == AIFeatureReadyState.EnsureNeeded)
                {
                    var op = await ImageObjectExtractor.EnsureReadyAsync();
                }
            }

            ImageObjectExtractor imageObjectExtractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

            ImageObjectExtractorHint hint = new ImageObjectExtractorHint{
                includeRects: null, 
                includePoints:
                    new List<PointInt32> { new PointInt32(306, 212),
                                           new PointInt32(216, 336)},
                excludePoints: null};
            
            SoftwareBitmap finalImage = imageObjectExtractor.GetSoftwareBitmapObjectMask(hint);
            """"
        },
        {
            ModelType.ImageDescription, """"
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.Generative;
            using Microsoft.Windows.AI.ContentModeration;
            using Windows.Graphics.Imaging;
            
            var readyState = ImageDescriptionGenerator.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
            {
                if (readyState == AIFeatureReadyState.EnsureNeeded)
                {
                    var op = await ImageDescriptionGenerator.EnsureReadyAsync();
                }
            }
            
            ImageDescriptionGenerator imageDescriptionGenerator = await ImageDescriptionGenerator.CreateAsync();
            
            ImageBuffer inputImage = ImageBuffer.CreateCopyFromBitmap(softwareBitmap);  
            
            ContentFilterOptions filterOptions = new ContentFilterOptions();
            filterOptions.PromptMinSeverityLevelToBlock.ViolentContentSeverity = SeverityLevel.Medium;
            filterOptions.ResponseMinSeverityLevelToBlock.ViolentContentSeverity = SeverityLevel.Medium;
            
            LanguageModelResponse languageModelResponse = await imageDescriptionGenerator.DescribeAsync(inputImage, ImageDescriptionScenario.Caption, filterOptions);
            
            Console.WriteLine(languageModelResponse.Response);
            """"
        }
    };
}