// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.SentenceEmbeddings.Embeddings;

[GallerySample(
    Name = "Semantic Search",
    Model1Types = [ModelType.EmbeddingModel],
    Scenario = ScenarioType.TextSemanticSearch,
    SharedCode = [
        SharedCodeEnum.EmbeddingGenerator,
        SharedCodeEnum.EmbeddingModelInput,
        SharedCodeEnum.TokenizerExtensions,
        SharedCodeEnum.DeviceUtils,
        SharedCodeEnum.StringData
    ],
    NugetPackageReferences = [
        "System.Numerics.Tensors",
        "Microsoft.ML.Tokenizers",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.Extensions.AI",
        "Microsoft.SemanticKernel.Connectors.InMemory"
    ],
    Id = "41391b3f-f143-4719-a171-b0ce9c4cdcd6",
    Icon = "\uE8D4")]
internal sealed partial class SemanticSearch : BaseSamplePage
{
    private EmbeddingGenerator? _embeddings;
    private CancellationTokenSource cts = new();

    public SemanticSearch()
    {
        this.InitializeComponent();
        this.Unloaded += (s, e) =>
        {
            CleanUp();
        };
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
    }

    protected override Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        _embeddings = new EmbeddingGenerator(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
        sampleParams.NotifyCompletion();

        this.SourceTextBox.Text = _sampleText;
        return Task.CompletedTask;
    }

    // <exclude>
    private void Page_Loaded()
    {
        SearchTextBox.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        CleanUp();
    }

    private void CleanUp()
    {
        _embeddings?.Dispose();
    }

    private void SemanticTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        this.SearchButton.IsEnabled = !string.IsNullOrEmpty(this.SearchTextBox.Text) && !string.IsNullOrEmpty(this.SourceTextBox.Text);
        ErrorMessage.IsOpen = string.IsNullOrEmpty(this.SearchTextBox.Text) || string.IsNullOrEmpty(this.SourceTextBox.Text);
    }

    internal static readonly string[] ParagraphSeparators = ["\n", "\r"];

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var sourceText = this.SourceTextBox.Text;
        var searchText = this.SearchTextBox.Text;

        if (!string.IsNullOrEmpty(sourceText) && !string.IsNullOrEmpty(searchText))
        {
            this.OutputProgressBar.Visibility = Visibility.Visible;
            this.ResultsGrid.Visibility = Visibility.Visible;
            Search(sourceText, searchText);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && SearchTextBox.Text.Length > 0)
        {
            var sourceText = this.SourceTextBox.Text;
            var searchText = this.SearchTextBox.Text;

            if (!string.IsNullOrEmpty(sourceText) && !string.IsNullOrEmpty(searchText))
            {
                this.OutputProgressBar.Visibility = Visibility.Visible;
                this.ResultsGrid.Visibility = Visibility.Visible;
                SearchTextBox.IsEnabled = false;
                Search(sourceText, searchText);
            }
        }
    }

    public void Search(string sourceText, string searchText)
    {
        CancellationToken ct = CancelGenerationAndGetNewToken();

        if (_embeddings == null)
        {
            return;
        }

        SendSampleInteractedEvent("Search"); // <exclude-line>

        Task.Run(
            async () =>
            {
                var sourceParagraphs = sourceText
                                .Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries)
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToList();

                var sourceContent = new List<string>();

                for (int i = 0; i < sourceParagraphs.Count; i++)
                {
                    var paragraph = sourceParagraphs[i];

                    var sourceSentences = paragraph
                                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToList();

                    var maxLength = 1024 / 2;
                    for (int s = 0; s < sourceSentences.Count; s++)
                    {
                        var content = sourceSentences[s];
                        int index = 0;
                        var contentChunks = new List<string>();
                        while (index < content.Length)
                        {
                            if (index + maxLength >= content.Length)
                            {
                                contentChunks.Add(
                                    SentenceEndRegex().Replace(content[index..].Trim(), "."));
                                break;
                            }

                            int lastIndexOfBreak = content.LastIndexOf(' ', index + maxLength, maxLength);
                            if (lastIndexOfBreak <= index)
                            {
                                lastIndexOfBreak = index + maxLength;
                            }

                            contentChunks.Add(
                                Regex.Replace(content[index..lastIndexOfBreak].Trim(), @"(\.){2,}", "."));

                            index = lastIndexOfBreak + 1;
                        }

                        sourceSentences.RemoveAt(s);
                        sourceSentences.InsertRange(s, contentChunks);
                        i += contentChunks.Count - 1;
                    }

                    sourceContent.AddRange(sourceSentences);
                }

                sourceContent = sourceContent
                    .Where(x => x != "\"")
                    .ToList();

                GeneratedEmbeddings<Embedding<float>> searchVectors;
                GeneratedEmbeddings<Embedding<float>> sourceVectors;

#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                IVectorStore? vectorStore = new InMemoryVectorStore();
#pragma warning restore SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                var stringsCollection = vectorStore.GetCollection<int, StringData>("strings");
                await stringsCollection.CreateCollectionIfNotExistsAsync(ct).ConfigureAwait(false);

                searchVectors = await _embeddings.GenerateAsync([searchText], null, ct).ConfigureAwait(false);

                sourceVectors = await _embeddings.GenerateAsync(sourceContent, null, ct).ConfigureAwait(false);

                await foreach (var key in stringsCollection.UpsertBatchAsync(
                    sourceVectors.Select((x, i) => new StringData
                    {
                        Key = i,
                        Text = sourceContent[i],
                        Vector = x.Vector
                    }),
                    null,
                    ct).ConfigureAwait(false))
                {
                }

                var vectorSearchResults = await stringsCollection.VectorizedSearchAsync(
                    searchVectors[0].Vector,
                    new VectorSearchOptions
                    {
                        // Number of results to return
                        Top = 5,
                        VectorPropertyName = nameof(StringData.Vector)
                    },
                    ct).ConfigureAwait(false);

                var resultMessage = string.Join("\n\n", vectorSearchResults.Results.ToBlockingEnumerable().Select(r => r.Record.Text));

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (this.OutputProgressBar.Visibility == Visibility.Visible)
                    {
                        this.OutputProgressBar.Visibility = Visibility.Collapsed;
                        SearchTextBox.IsEnabled = true;
                    }

                    this.ResultsTextBlock.Text = resultMessage;
                });
            },
            ct);
    }

    private CancellationToken CancelGenerationAndGetNewToken()
    {
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }

    private const string _sampleText = @"The Story of Little Red Riding Hood

Once upon a time there was a dear little girl who was loved by every one who looked at her, but most of all by her grandmother, and there was nothing that she would not have given to the child. Once she gave her a little cap of red velvet, which suited her so well that she would never wear anything else. So she was always called Little Red Riding Hood.

One day her mother said to her, ""Come, Little Red Riding Hood, here is a piece of cake and a bottle of wine. Take them to your grandmother, she is ill and weak, and they will do her good. Set out before it gets hot, and when you are going, walk nicely and quietly and do not run off the path, or you may fall and break the bottle, and then your grandmother will get nothing. And when you go into her room, don't forget to say, good-morning, and don't peep into every corner before you do it.""

I will take great care, said Little Red Riding Hood to her mother, and gave her hand on it.

The grandmother lived out in the wood, half a league from the village, and just as Little Red Riding Hood entered the wood, a wolf met her. Little Red Riding Hood did not know what a wicked creature he was, and was not at all afraid of him.

""Good-day, Little Red Riding Hood,"" said he.

""Thank you kindly, wolf.""

""Whither away so early, Little Red Riding Hood?""

""To my grandmother's.""

""What have you got in your apron?""

""Cake and wine. Yesterday was baking-day, so poor sick grandmother is to have something good, to make her stronger.""

""Where does your grandmother live, Little Red Riding Hood?""

""A good quarter of a league farther on in the wood. Her house stands under the three large oak-trees, the nut-trees are just below. You surely must know it,"" replied Little Red Riding Hood.

The wolf thought to himself, ""What a tender young creature. What a nice plump mouthful, she will be better to eat than the old woman. I must act craftily, so as to catch both."" So he walked for a short time by the side of Little Red Riding Hood, and then he said, ""see Little Red Riding Hood, how pretty the flowers are about here. Why do you not look round. I believe, too, that you do not hear how sweetly the little birds are singing. You walk gravely along as if you were going to school, while everything else out here in the wood is merry.""

Little Red Riding Hood raised her eyes, and when she saw the sunbeams dancing here and there through the trees, and pretty flowers growing everywhere, she thought, suppose I take grandmother a fresh nosegay. That would please her too. It is so early in the day that I shall still get there in good time. And so she ran from the path into the wood to look for flowers. And whenever she had picked one, she fancied that she saw a still prettier one farther on, and ran after it, and so got deeper and deeper into the wood.

Meanwhile the wolf ran straight to the grandmother's house and knocked at the door.

""Who is there?""

""Little Red Riding Hood,"" replied the wolf. ""She is bringing cake and wine. Open the door.""

""Lift the latch,"" called out the grandmother, ""I am too weak, and cannot get up.""

The wolf lifted the latch, the door sprang open, and without saying a word he went straight to the grandmother's bed, and devoured her. Then he put on her clothes, dressed himself in her cap, laid himself in bed and drew the curtains.

Little Red Riding Hood, however, had been running about picking flowers, and when she had gathered so many that she could carry no more, she remembered her grandmother, and set out on the way to her.

She was surprised to find the cottage-door standing open, and when she went into the room, she had such a strange feeling that she said to herself, oh dear, how uneasy I feel to-day, and at other times I like being with grandmother so much.

She called out, ""Good morning,"" but received no answer. So she went to the bed and drew back the curtains. There lay her grandmother with her cap pulled far over her face, and looking very strange.

""Oh, grandmother,"" she said, ""what big ears you have.""

""The better to hear you with, my child,"" was the reply.

""But, grandmother, what big eyes you have,"" she said.

""The better to see you with, my dear.""

""But, grandmother, what large hands you have.""

""The better to hug you with.""

""Oh, but, grandmother, what a terrible big mouth you have.""

""The better to eat you with.""

And scarcely had the wolf said this, than with one bound he was out of bed and swallowed up Little Red Riding Hood.

When the wolf had appeased his appetite, he lay down again in the bed, fell asleep and began to snore very loud. The huntsman was just passing the house, and thought to himself, how the old woman is snoring. I must just see if she wants anything.

So he went into the room, and when he came to the bed, he saw that the wolf was lying in it. ""Do I find you here, you old sinner,"" said he. ""I have long sought you.""

Then just as he was going to fire at him, it occurred to him that the wolf might have devoured the grandmother, and that she might still be saved, so he did not fire, but took a pair of scissors, and began to cut open the stomach of the sleeping wolf.

When he had made two snips, he saw the Little Red Riding Hood shining, and then he made two snips more, and the little girl sprang out, crying, ""Ah, how frightened I have been. How dark it was inside the wolf.""

And after that the aged grandmother came out alive also, but scarcely able to breathe. Little Red Riding Hood, however, quickly fetched great stones with which they filled the wolf's belly, and when he awoke, he wanted to run away, but the stones were so heavy that he collapsed at once, and fell dead.

Then all three were delighted. The huntsman drew off the wolf's skin and went home with it. The grandmother ate the cake and drank the wine which Little Red Riding Hood had brought, and revived, but Little Red Riding Hood thought to herself, as long as I live, I will never by myself leave the path, to run into the wood, when my mother has forbidden me to do so.

It is also related that once when Little Red Riding Hood was again taking cakes to the old grandmother, another wolf spoke to her, and tried to entice her from the path. Little Red Riding Hood, however, was on her guard, and went straight forward on her way, and told her grandmother that she had met the wolf, and that he had said good-morning to her, but with such a wicked look in his eyes, that if they had not been on the public road she was certain he would have eaten her up. ""Well,"" said the grandmother, ""we will shut the door, that he may not come in.""

Soon afterwards the wolf knocked, and cried, ""open the door, grandmother, I am Little Red Riding Hood, and am bringing you some cakes.""

But they did not speak, or open the door, so the grey-beard stole twice or thrice round the house, and at last jumped on the roof, intending to wait until Little Red Riding Hood went home in the evening, and then to steal after her and devour her in the darkness. But the grandmother saw what was in his thoughts. In front of the house was a great stone trough, so she said to the child, take the pail, Little Red Riding Hood. I made some sausages yesterday, so carry the water in which I boiled them to the trough. Little Red Riding Hood carried until the great trough was quite full. Then the smell of the sausages reached the wolf, and he sniffed and peeped down, and at last stretched out his neck so far that he could no longer keep his footing and began to slip, and slipped down from the roof straight into the great trough, and was drowned. But Little Red Riding Hood went joyously home, and no one ever did anything to harm her again.";

    [GeneratedRegex(@"(\.){2,}")]
    private static partial Regex SentenceEndRegex();
}