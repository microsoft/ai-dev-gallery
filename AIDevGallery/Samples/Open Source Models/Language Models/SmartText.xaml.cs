// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Extensions.AI;
using System;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Smart Text Box",
    Model1Types = [ModelType.LanguageModels],
    Id = "6663205f-af6e-4608-a95c-03264b6cfc34",
    Icon = "\uE8D4",
    Scenario = ScenarioType.SmartControlsSmartTextBox,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    SharedCode = [
        SharedCodeEnum.SmartTextBoxCs,
        SharedCodeEnum.SmartTextBoxXaml
    ])]
internal sealed partial class SmartText : BaseSamplePage
{
    private IChatClient? _model;

    public SmartText()
    {
        this.Unloaded += (s, e) => CleanUp();
        try
        {
            this.InitializeComponent();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e.Message);
        }
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        _model = await sampleParams.GetIChatClientAsync();
        if (_model != null)
        {
            this.SmartTextBox.ChatClient = _model;
        }

        sampleParams.NotifyCompletion();
    }

    private void CleanUp()
    {
        _model?.Dispose();
    }
}