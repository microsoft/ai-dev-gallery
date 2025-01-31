// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AIDevGallery.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal class ScenariosSourceGenerator : IIncrementalGenerator
{
    private Dictionary<string, ScenarioCategory>? scenarioCategories = null;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        string scenarioJson;
        var assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("AIDevGallery.SourceGenerator.scenarios.json"))
        {
            using (StreamReader reader = new(stream))
            {
                scenarioJson = reader.ReadToEnd().Trim();
            }
        }

        scenarioCategories = JsonSerializer.Deserialize(scenarioJson, SourceGenerationContext.Default.DictionaryStringScenarioCategory);
        context.RegisterPostInitializationOutput(Execute);
    }

    public void Execute(IncrementalGeneratorPostInitializationContext context)
    {
        if (scenarioCategories == null)
        {
            return;
        }

        GenerateScenarioCategoryTypeFile(context, scenarioCategories);

        GenerateScenariosTypeFile(context, scenarioCategories);

        GenerateScenarioHelpersFile(context, scenarioCategories);
    }

    private void GenerateScenarioHelpersFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, ScenarioCategory> scenarioCategories)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Samples;

            internal static partial class ScenarioCategoryHelpers
            {
            """");

        sourceBuilder.AppendLine("    internal static List<ScenarioCategory> AllScenarioCategories { get; } = [");
        foreach (var scenarioCategory in scenarioCategories)
        {
            string icon = Helpers.EscapeUnicodeString(scenarioCategory.Value.Icon);

            sourceBuilder.AppendLine(
                $$""""
                        new ScenarioCategory
                        {
                            Name = "{{scenarioCategory.Value.Name}}",
                            Icon = {{icon}},
                            Description = "{{scenarioCategory.Value.Description}}",
                            Scenarios = new List<Scenario>
                            {
                """");
            foreach (var scenario in scenarioCategory.Value.Scenarios)
            {
                sourceBuilder.AppendLine(
                    $$""""""
                                    new Scenario
                                    {
                                        ScenarioType = ScenarioType.{{scenarioCategory.Key}}{{scenario.Key}},
                                        Name = "{{scenario.Value.Name}}",
                                        Description = "{{scenario.Value.Description}}",
                                        Id = "{{scenario.Value.Id}}",
                                        Icon = {{icon}}
                                    },
                    """""");
            }

            sourceBuilder.AppendLine(
                $$""""
                            }
                        },
                """");
        }

        sourceBuilder.AppendLine("    ];");

        sourceBuilder.AppendLine("}");

        context.AddSource($"ScenarioCategoryHelpers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private void GenerateScenariosTypeFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, ScenarioCategory> scenarioCategories)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;

            namespace AIDevGallery.Models;
            
            internal enum ScenarioType
            {
            """");
        foreach (var scenarioCategory in scenarioCategories)
        {
            foreach (var scenario in scenarioCategory.Value.Scenarios)
            {
                sourceBuilder.AppendLine($"    {scenarioCategory.Key}{scenario.Key},");
            }
        }

        sourceBuilder.AppendLine(
            """
            }
            """);

        context.AddSource("ScenarioType.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private static void GenerateScenarioCategoryTypeFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, ScenarioCategory> scenarioCategories)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Models;

            internal enum ScenarioCategoryType
            {
            """");

        foreach (var scenarioCategory in scenarioCategories)
        {
            sourceBuilder.AppendLine($"    {scenarioCategory.Key},");
        }

        sourceBuilder.AppendLine(
            $$""""
            }
            """");

        context.AddSource("ScenarioCategoryType.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }
}