// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry.Events;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal sealed partial class SemanticComboBox : Control
{
    private VectorStore? _vectorStore;
    private VectorStoreCollection<object, Dictionary<string, object?>>? _stringsCollection;

    public IEmbeddingGenerator<string, Embedding<float>>? EmbeddingGenerator
    {
        get { return (IEmbeddingGenerator<string, Embedding<float>>?)GetValue(ModelPathProperty); }
        set { SetValue(ModelPathProperty, value); }
    }

    public static readonly DependencyProperty ModelPathProperty =
        DependencyProperty.Register(
            nameof(EmbeddingGenerator),
            typeof(IEmbeddingGenerator<string, Embedding<float>>),
            typeof(SemanticComboBox),
            new PropertyMetadata(null, OnEmbeddingGeneratorChanged));

    public List<string> Items
    {
        get { return (List<string>)GetValue(ItemsProperty); }
        set { SetValue(ItemsProperty, value); }
    }

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items),
            typeof(List<string>),
            typeof(SemanticComboBox),
            new PropertyMetadata(null, OnItemsChanged));

    public SemanticComboBox()
    {
        this.DefaultStyleKey = typeof(SemanticComboBox);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        this.Unloaded += OnUnloaded;
        if (GetTemplateChild("SemanticSuggestBox") is AutoSuggestBox semanticSuggestBox)
        {
            semanticSuggestBox.TextChanged += SuggestBox_TextChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (EmbeddingGenerator != null)
        {
            EmbeddingGenerator.Dispose();
            EmbeddingGenerator = null;
        }
    }

    public async Task<IEnumerable<Dictionary<string, object?>>> Search(string searchTerm)
    {
        if (EmbeddingGenerator == null || _stringsCollection == null)
        {
            return [];
        }

        SampleInteractionEvent.SendSampleInteractedEvent(EmbeddingGenerator.GetService<EmbeddingGeneratorMetadata>(), Models.ScenarioType.SmartControlsSemanticComboBox, "Search"); // <exclude-line>
        GeneratedEmbeddings<Embedding<float>> results = [];

        var searchVectors = await EmbeddingGenerator.GenerateAsync([searchTerm]);
        return _stringsCollection.SearchAsync(searchVectors[0].Vector, 5)
            .ToBlockingEnumerable()
            .Select(r => r.Record);
    }

    private async void SuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Since selecting an item will also change the text,
        // only listen to changes caused by user entering text.
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var searchResults = await Search(sender.Text);
            sender.ItemsSource = searchResults.Select(item => item["Text"]);
        }
    }

    private static void OnEmbeddingGeneratorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is IEmbeddingGenerator<string, Embedding<float>> oldEmbeddingGenerator)
        {
            oldEmbeddingGenerator.Dispose();
        }
    }

    private static async void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SemanticComboBox semanticComboBox = (SemanticComboBox)d;
        List<string> newItems = (List<string>)e.NewValue;
        if (newItems != null && semanticComboBox.EmbeddingGenerator != null)
        {
            if (semanticComboBox._vectorStore == null || semanticComboBox._stringsCollection == null)
            {
                semanticComboBox._vectorStore = new InMemoryVectorStore();
                semanticComboBox._stringsCollection = semanticComboBox._vectorStore.GetDynamicCollection("strings", StringData.VectorStoreDefinition);
                await semanticComboBox._stringsCollection.EnsureCollectionExistsAsync().ConfigureAwait(false);
            }

            var sourceVectors = await semanticComboBox.EmbeddingGenerator.GenerateAsync(newItems).ConfigureAwait(false);

            await semanticComboBox._stringsCollection.UpsertAsync(
                    sourceVectors.Select((x, i) => new Dictionary<string, object?>
                    {
                        ["Key"] = i,
                        ["Text"] = newItems[i],
                        ["Vector"] = x.Vector
                    })).ConfigureAwait(false);
        }
    }
}