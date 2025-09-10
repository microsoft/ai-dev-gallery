// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.GamePlay;

[GallerySample(
    Name = "Infinite Rebirth",
    Model1Types = [ModelType.Phi4Mini],
    Scenario = ScenarioType.GameInfiniteRebirthGame,
    SharedCode = [
        SharedCodeEnum.StringData
    ],
    NugetPackageReferences = [
        "NAudio.WinMM",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Id = "infinite-rebirth-game",
    Icon = "\uE7FC")]
internal sealed partial class InfiniteRebirthGame : BaseSamplePage
{
    public InfiniteRebirthGame()
    {
        this.Unloaded += (s, e) => DisposeMemory();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
        PopulateLanguageComboBoxes();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        // todo
    }

    // <exclude>
    private void Page_Loaded()
    {
        StartStopButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private void PopulateLanguageComboBoxes()
    {
        // Populate SourceLanguageComboBox
        foreach (var language in WhisperWrapper.LanguageCodes.Keys)
        {
            SourceLanguageComboBox.Items.Add(language);
        }

        // Select default source language
        SourceLanguageComboBox.SelectedIndex = 0;
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        // todo
    }

    private void UpdateTranscription(byte[] audioData)
    {
        // todo
    }

    private void DisposeMemory()
    {
        // todo
    }
}