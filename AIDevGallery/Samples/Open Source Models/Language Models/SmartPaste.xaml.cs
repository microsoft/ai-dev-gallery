// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Smart Paste",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Id = "cdd824f9-2472-4aac-bce9-f2b06f7e6b14",
    Icon = "\uE8D4",
    Scenario = ScenarioType.SmartControlsSmartPaste,
    NugetPackageReferences = [
        "CommunityToolkit.Mvvm",
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    SharedCode = [
        SharedCodeEnum.SmartPasteFormCs,
        SharedCodeEnum.SmartPasteFormXaml,
        SharedCodeEnum.GenAIModel
    ])]
internal sealed partial class SmartPaste : BaseSamplePage
{
    private IChatClient? model;
    public List<string> FieldLabels { get; set; } = ["Name", "Address", "City", "State", "Zip"];

    public SmartPaste()
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
        model = await sampleParams.GetIChatClientAsync();
        if (model != null)
        {
            this.SmartForm.Model = model;
        }

        sampleParams.NotifyCompletion();
    }

    private void CleanUp()
    {
        model?.Dispose();
    }
}