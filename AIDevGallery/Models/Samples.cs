// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
namespace AIDevGallery.Models;

internal class ModelGroup
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Icon { get; init; }
    public required bool IsApi { get; init; }
}

internal class Sample
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Icon { get; init; } = null!;
    public ScenarioType Scenario { get; init; }
    public List<ModelType> Model1Types { get; init; } = null!;
    public List<ModelType>? Model2Types { get; init; }
    public Type PageType { get; init; } = null!;
    public string CSCode { get; init; } = null!;
    public string XAMLCode { get; init; } = null!;
    public List<Samples.SharedCodeEnum> SharedCode { get; init; } = null!;
    public List<string> NugetPackageReferences { get; init; } = null!;
    public List<string> AssetFilenames { get; init; } = null!;
}

internal class ModelFamily
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string? DocsUrl { get; set; }
    public string ReadmeUrl { get; init; } = null!;
}

internal class ApiDefinition
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Icon { get; init; } = null!;
    public string IconGlyph { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string ReadmeUrl { get; init; } = null!;
    public string License { get; init; } = null!;
    public string SampleIdToShowInDocs { get; set; } = null!;
}

internal class ModelDetails
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;

    public string Description { get; set; } = null!;
    [JsonConverter(typeof(SingleOrListOfHardwareAcceleratorConverter))]
    [JsonPropertyName("HardwareAccelerator")]
    public List<HardwareAccelerator> HardwareAccelerators { get; set; } = null!;
    public long Size { get; set; }
    public bool? SupportedOnQualcomm { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParameterSize { get; set; }
    public bool IsUserAdded { get; set; }
    public PromptTemplate? PromptTemplate { get; set; }
    public string? ReadmeUrl { get; set; }
    public string? License { get; set; }
    public List<string>? FileFilters { get; set; }
    public List<AIToolkitAction>? AIToolkitActions { get; set; }
    public string? AIToolkitId { get; set; }
    public string? AIToolkitFinetuningId { get; set; }

    private ModelCompatibility? compatibility;
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ModelCompatibility Compatibility
    {
        get
        {
            compatibility ??= ModelCompatibility.GetModelCompatibility(this);

            return compatibility;
        }
    }

    private string? icon;
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Icon
    {
        get
        {
            // Full path is already set
            if (string.IsNullOrEmpty(icon))
            {
                if (Url.StartsWith("https://github", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (App.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Light)
                    {
                        icon = "GitHub.light.svg";
                    }
                    else
                    {
                        icon = "GitHub.dark.svg";
                    }
                }
                else
                {
                    icon = "HuggingFace.svg";
                }
            }

            // In some cases the full path is already set
            if (!icon.StartsWith("ms-appx", StringComparison.InvariantCultureIgnoreCase))
            {
                icon = "ms-appx:///Assets/ModelIcons/" + icon;
            }

            return icon;
        }

        set => icon = value;
    }
}

internal class PromptTemplate
{
    public string? System { get; set; }
    public string? User { get; init; }
    public string? Assistant { get; set; }
    public string[]? Stop { get; init; }
}

internal class ScenarioCategory
{
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Description { get; init; }
    public required List<Scenario> Scenarios { get; init; }
}

internal class Scenario
{
    public string Name { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Id { get; init; } = null!;

    public string? Icon { get; init; }
    public ScenarioType ScenarioType { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<HardwareAccelerator>))]
internal enum HardwareAccelerator
{
    CPU,
    DML,
    QNN,
    WCRAPI
}

[JsonConverter(typeof(JsonStringEnumConverter<AIToolkitAction>))]
internal enum AIToolkitAction
{
    FineTuning,
    PromptBuilder,
    BulkRun,
    Playground
}

#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1649 // File name should match first type name