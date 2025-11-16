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
            ModelType.TextSummarizer, """"
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
                using TextSummarizer textSummarizer = new TextSummarizer(languageModel);

                string prompt = @"Phi Silica is a local language model that you can integrate into your Windows apps.

            As Microsoft's most powerful NPU-tuned local language model, Phi Silica is optimized for efficiency and 
            performance on Windows Copilot+ PCs devices while still offering many of the capabilities found in Large Language Models (LLMs).";

                var result = await textSummarizer.SummarizeParagraphAsync(prompt);

                Debug.WriteLine(result.Text);
            }
            """"
        },
        {
            ModelType.TextRewriter, """"
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
                using TextRewriter textRewriter = new TextRewriter(languageModel);

                string prompt = @"Phi Silica is a local language model that you can integrate into your Windows apps.

            As Microsoft's most powerful NPU-tuned local language model, Phi Silica is optimized for efficiency and 
            performance on Windows Copilot+ PCs devices while still offering many of the capabilities found in Large Language Models (LLMs).";

                var result = await textRewriter.RewriteAsync(prompt);

                Debug.WriteLine(result.Text);
            }
            """"
        },
        {
            ModelType.TextToTableConverter, """"
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
                using TextToTableConverter textToTableConverter = new TextToTableConverter(languageModel);

                string prompt = @"{"colors":[{"name":"Red","hex":"#FF0000","rgb":{"r":255,"g":0,"b":0}},{"name":"Green","hex":"#00FF00","rgb":{"r":0,"g":255,"b":0}},
                {"name":"Blue","hex":"#0000FF","rgb":{"r":0,"g":0,"b":255}},{"name":"Yellow","hex":"#FFFF00","rgb":{"r":255,"g":255,"b":0}},
                {"name":"Black","hex":"#000000","rgb":{"r":0,"g":0,"b":0}},{"name":"White","hex":"#FFFFFF","rgb":{"r":255,"g":255,"b":255}}]}";

                var result = await textToTableConverter.ConvertAsync(prompt);
               
                Debug.WriteLine(string.Join("\n", result.GetRows().Select(r => string.Join("\t", r.GetColumns()))));
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

                Debug.WriteLine(string.Join("\n", result.Lines.Select(l => l.Text)));
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
            ModelType.ForegroundExtractor, """"
            using Microsoft.Windows.AI;
            using Microsoft.Windows.AI.Imaging;
            using Windows.Graphics;
            using Windows.Graphics.Imaging;

            var readyState = ImageForegroundExtractor.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageForegroundExtractor.EnsureReadyAsync();
                }

                ImageForegroundExtractor imageForegroundExtractor = await ImageForegroundExtractor.CreateAsync();

                SoftwareBitmap mask = imageForegroundExtractor.GetMaskFromSoftwareBitmap(inputBitmap);
                SoftwareBitmap finalBitmap = ApplyMask(inputBitmap, mask);
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

                ImageDescriptionResult languageModelResponse = await imageDescriptionGenerator.DescribeAsync(inputImage, 
                        ImageDescriptionKind.DiagramDescription, filterOptions);

                Debug.WriteLine(languageModelResponse.Description);
            }
            """"
        },
        {
            ModelType.SemanticSearch, """"
            using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;

            // This is some text data that we want to add to the index:
            Dictionary<string, string> simpleTextData = new Dictionary<string, string>
            {
                {"item1", "Here is some information about Cats: Cats are cute and fluffy. Young cats are very playful." },
                {"item2", "Dogs are loyal and affectionate animals known for their companionship, intelligence, and diverse breeds." },
                {"item3", "Fish are aquatic creatures that breathe through gills and come in a vast variety of shapes, sizes, and colors." },
                {"item4", "Broccoli is a nutritious green vegetable rich in vitamins, fiber, and antioxidants." },
                {"item5", "Computers are powerful electronic devices that process information, perform calculations, and enable communication worldwide." },
                {"item6", "Music is a universal language that expresses emotions, tells stories, and connects people through rhythm and melody." },
            };

            public void SimpleTextIndexingSample()
            {
                AppContentIndexer indexer = GetIndexerForApp();

                // Add some text data to the index:
                foreach (var item in simpleTextData)
                {
                    IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(item.Key, item.Value);
                    indexer.AddOrUpdate(textContent);
                }
            }

            public void SimpleTextQueryingSample()
            {
                AppContentIndexer indexer = GetIndexerForApp();

                // We search the index using a semantic query:
                AppIndexQuery queryCursor = indexer.CreateQuery("Facts about kittens.");
                IReadOnlyList<TextQueryMatch> textMatches = queryCursor.GetNextMatches(5);

                // Nothing in the index exactly matches what we queried but item1 is similar to the query so we expect
                // that to be the first match.
                foreach (var match in textMatches)
                {
                    Debug.WriteLine(match.ContentId);
                    if (match.ContentKind == QueryMatchContentKind.AppManagedText)
                    {
                        AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;

                        // Only part of the original string may match the query. So we can use TextOffset and TextLength to extract the match.
                        // In this example, we might imagine that the substring "Cats are cute and fluffy" from "item1" is the top match for the query.
                        string matchingData = simpleTextData[match.ContentId];
                        string matchingString = matchingData.Substring(textResult.TextOffset, textResult.TextLength);
                        Debug.WriteLine(matchingString);
                    }
                }
            }

            // We load the image data from a set of known files and send that image data to the indexer.
            // The image data does not need to come from files on disk, it can come from anywhere.
            Dictionary<string, string> imageFilesToIndex = new Dictionary<string, string>
                {
                    {"item1", "Cat.jpg" },
                    {"item2", "Dog.jpg" },
                    {"item3", "Fish.jpg" },
                    {"item4", "Broccoli.jpg" },
                    {"item5", "Computer.jpg" },
                    {"item6", "Music.jpg" },
                };

            public void SimpleImageIndexingSample()
            {
                AppContentIndexer indexer = GetIndexerForApp();

                // Add some image data to the index.
                foreach (var item in imageFilesToIndex)
                {
                    var file = item.Value;
                    var softwareBitmap = Helpers.GetSoftwareBitmapFromFile(file);
                    IndexableAppContent imageContent = AppManagedIndexableAppContent.CreateFromBitmap(item.Key, softwareBitmap);

                    indexer.AddOrUpdate(imageContent);
                }
            }

            public void SimpleImageIndexingSample_RunQuery()
            {
                AppContentIndexer indexer = GetIndexerForApp();

                // We query the index for some data to match our text query.
                AppIndexQuery query = indexer.CreateQuery("cute pictures of kittens");
                IReadOnlyList<ImageQueryMatch> imageMatches = query.GetImageMatches(5);

                // One of the images that we indexed was a photo of a cat. We expect this to be the first match to match the query.
                foreach (var match in imageMatches)
                {
                    Debug.WriteLine(match.ContentId);
                    if (match.ContentKind == QueryMatchContentKind.AppManagedImage)
                    {
                        AppManagedImageQueryMatch imageResult = (AppManagedImageQueryMatch)match;
                        var matchingFileName = imageFilesToIndex[match.ContentId];

                        // It might be that the match is at a particular region in the image. The result includes
                        // the subregion of the image that includes the match.
                        Debug.WriteLine($"Matching file: '{matchingFileName}' at location {imageResult.Subregion}");
                    }
                }
            }
            """"
        },
        {
            ModelType.KnowledgeRetrieval, """"
            using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
            
            public void SimpleRAGScenario()
            {
                AppContentIndexer indexer = GetIndexerForApp();

                // These are some text files that had previously been added to the index.
                // The key is the contentId of the item.
                Dictionary<string, string> data = new Dictionary<string, string>
                {
                    {"file1", "File1.txt" },
                    {"file2", "File2.txt" },
                    {"file3", "File3.txt" },
                };

                string userPrompt = Helpers.GetUserPrompt();

                // We execute a query against the index using the user's prompt string as the query text.
                AppIndexQuery query = indexer.CreateQuery(userPrompt);
                IReadOnlyList<TextQueryMatch> textMatches = query.GetTextMatches(5);

                StringBuilder promptStringBuilder = new StringBuilder();
                promptStringBuilder.AppendLine("Please refer to the following pieces of information when responding to the user's prompt:");

                // For each of the matches found, we include the relevant snippets of the text files in the augmented query that we send to the language model
                foreach (var match in textMatches)
                {
                    if (match is AppManagedTextQueryMatch textResult)
                    {
                        // We load the content of the file that contains the match:
                        string matchingFilename = data[match.ContentId];
                        string fileContent = File.ReadAllText(matchingFilename);

                        // Find the substring within the loaded text that contains the match:
                        string matchingString = fileContent.Substring(textResult.TextOffset, textResult.TextLength);
                        promptStringBuilder.AppendLine(matchingString);
                        promptStringBuilder.AppendLine();
                    }
                }

                promptStringBuilder.AppendLine("Please provide a response to the following user prompt:");
                promptStringBuilder.AppendLine(userPrompt);

                var response = Helpers.GetResponseFromChatAgent(promptStringBuilder.ToString());

                Debug.WriteLine(response);
            }
            """"
        },
        {
            ModelType.AppIndexCapability, """"
            using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;

            // Get index capabilities of current system
            public void SimpleCapabilitiesSample()
            {
                IndexCapabilitiesOfCurrentSystem capabilities = AppContentIndexer.GetIndexCapabilitiesOfCurrentSystem();

                // Status is one of: Ready, NotReady, DisabledByPolicy or NotSupported.
                Debug.WriteLine($"Lexical Text Capability Status: {capabilities.GetIndexCapabilityStatus(IndexCapability.TextLexical)}");
                Debug.WriteLine($"Semantic Text Capability Status: {capabilities.GetIndexCapabilityStatus(IndexCapability.TextSemantic)}");
                Debug.WriteLine($"OCR Capability Status: {capabilities.GetIndexCapabilityStatus(IndexCapability.ImageOcr)}");
                Debug.WriteLine($"Semantic Image Capability Status: {capabilities.GetIndexCapabilityStatus(IndexCapability.ImageSemantic)}");
            }

            // Get index capabilities of current index instance
            public async void IndexCapabilitiesSample()
            {
                using AppContentIndexer indexer = AppContentIndexer.GetOrCreateIndex("myindex").Indexer;

                // Some capabilities will initially be unavailable and the indexer will automatically load them in the background.
                // Wait for the indexer to attempt to load the required components.
                // Note that this may take a significant amount of time as components may need to be downloaded and installed by Windows.
                await indexer.WaitForIndexCapabilitiesAsync();

                IndexCapabilities capabilities = indexer.GetIndexCapabilities();

                // Each status will be one of: Unknown, Initialized, Initializing, Suppressed, Unsupported, DisabledByPolicy, InitializationError
                // If status is Initialized, that capability is ready for use

                if (capabilities.GetCapabilityState(IndexCapability.TextLexical).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
                {
                    Debug.WriteLine("Lexical text indexing and search is available.");
                }
                else
                {
                    Debug.WriteLine("Text indexing and search is not currenlty possible.");
                }

                if (capabilities.GetCapabilityState(IndexCapability.TextSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
                {
                    Debug.WriteLine("Semantic text indexing and search is available.");
                }
                else
                {
                    Debug.WriteLine("Only lexical text search is currently possible.");
                }

                if (capabilities.GetCapabilityState(IndexCapability.ImageSemantic).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
                {
                    Debug.WriteLine("Semantic image indexing and search is available.");
                }
                else
                {
                    Debug.WriteLine("Semantic image search is not currently possible");
                }

                if (capabilities.GetCapabilityState(IndexCapability.ImageOcr).InitializationStatus == IndexCapabilityInitializationStatus.Initialized)
                {
                    Debug.WriteLine("OCR is available. Searching text within images is possible.");
                }
                else
                {
                    Debug.WriteLine("Search for text within images is not currently possible.");
                }
            }
            """"
        },
        {
            ModelType.SDXL, """"
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;

            var readyState = ImageGenerator.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageGenerator.EnsureReadyAsync();
                }

                ImageGenerator imageGenerator = await ImageGenerator.CreateAsync();

                var result = imageGenerator.GenerateImageFromTextPrompt(prompt, new ImageGenerationOptions());
                if (result.Status == ImageGeneratorResultStatus.Success)
                {
                    var imageBuffer = result.Image;
                    var softwareBitmap = imageBuffer.CopyToSoftwareBitmap();
                    var convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, 
                            BitmapAlphaMode.Premultiplied);
                    if (convertedImage != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(convertedImage);
                        var finalImage = source;
                    }
                    else
                    {
                        Console.WriteLine("Failed to convert the image.");
                    }
                }
                else
                {
                    Console.WriteLine($"Image generation failed with status: {result.Status}");
                }
            }
            """"
        },
        {
            ModelType.RestyleImage, """""
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;

            var readyState = ImageGenerator.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageGenerator.EnsureReadyAsync();
                }

                ImageGenerator imageGenerator = await ImageGenerator.CreateAsync();
                ImageFromImageGenerationOptions imageFromImageGenerationOption = new()
                {
                    Style = ImageFromImageGenerationStyle.Restyle
                };

                using var inputBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

                var result = imageGenerator.GenerateImageFromImageBuffer(inputBuffer, prompt,
                        new ImageGenerationOptions(), imageFromImageGenerationOption);
                if (result.Status == ImageGeneratorResultStatus.Success)
                {
                    var imageBuffer = result.Image;
                    var softwareBitmap = imageBuffer.CopyToSoftwareBitmap();
                    var convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, 
                            BitmapAlphaMode.Premultiplied);
                    if (convertedImage != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(convertedImage);
                        var finalImage = source;
                    }
                    else
                    {
                        Console.WriteLine("Failed to convert the image.");
                    }
                }
                else
                {
                    Console.WriteLine($"Image generation failed with status: {result.Status}");
                }
            }
            """""
        },
        {
            ModelType.ColoringBook, """""
            using Microsoft.Graphics.Imaging;
            using Microsoft.Windows.AI.Imaging;
            using Microsoft.Windows.AI;

            var readyState = ImageGenerator.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await ImageGenerator.EnsureReadyAsync();
                }

                ImageGenerator imageGenerator = await ImageGenerator.CreateAsync();
                
                using var inputBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);

                var result = imageGenerator.GenerateImageFromImageBufferAndMask(inputBuffer, inputMask, prompt, new ImageGenerationOptions());
                if (result.Status == ImageGeneratorResultStatus.Success)
                {
                    var imageBuffer = result.Image;
                    var softwareBitmap = imageBuffer.CopyToSoftwareBitmap();
                    var convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, 
                            BitmapAlphaMode.Premultiplied);
                    if (convertedImage != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(convertedImage);
                        var finalImage = source;
                    }
                    else
                    {
                        Console.WriteLine("Failed to convert the image.");
                    }
                }
                else
                {
                    Console.WriteLine($"Image generation failed with status: {result.Status}");
                }
            }
            """""
        }
    };
}