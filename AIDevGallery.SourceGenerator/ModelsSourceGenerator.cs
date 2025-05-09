// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

#pragma warning disable RS1035 // Do not use APIs banned for analyzers

namespace AIDevGallery.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal class ModelSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<AdditionalText> modelJsons = context.AdditionalTextsProvider.Where(
                static file => file.Path.EndsWith(".json") && file.Path.Contains(@"\Samples\Definitions\"));

        var pathsAndContents = modelJsons.Select((text, cancellationToken) =>
                (text.Path, Content: text.GetText(cancellationToken)!.ToString(), CancellationToken: cancellationToken))
            .Collect();

        context.RegisterSourceOutput(pathsAndContents, Execute);
    }

    public void Execute(SourceProductionContext context, ImmutableArray<(string Path, string Content, CancellationToken CancellationToken)> modelJsons)
    {
        Dictionary<string, object> modelTypes = [];
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;

            namespace AIDevGallery.Models;
            
            internal enum ModelType
            {
            """");

        foreach (var modelJson in modelJsons)
        {
            try
            {
                var success = true;
                switch (modelJson.Path)
                {
                    case var path when path.EndsWith("apis.json"):
                        var apiGroups = JsonSerializer.Deserialize(modelJson.Content, SourceGenerationContext.Default.DictionaryStringApiGroup);
                        if (apiGroups == null)
                        {
                            throw new InvalidOperationException("Failed to deserialize api.json");
                        }

                        AddApis(sourceBuilder, apiGroups);

                        break;

                    case var path when path.EndsWith(".model.json"):
                        var modelFamilies = JsonSerializer.Deserialize(modelJson.Content, SourceGenerationContext.Default.DictionaryStringModelFamily);
                        if (modelFamilies == null)
                        {
                            throw new InvalidOperationException("Failed to deserialize model.json");
                        }

                        foreach (var modelFamily in modelFamilies)
                        {
                            if (!AddEnumValue(sourceBuilder, modelFamily.Key, modelFamily))
                            {
                                success = false;
                            }

                            foreach (var model in modelFamily.Value.Models)
                            {
                                if (!AddEnumValue(sourceBuilder, $"{modelFamily.Key}{model.Key}", model))
                                {
                                    success = false;
                                }
                            }
                        }

                        if (!success)
                        {
                            File.WriteAllText(modelJson.Path, Helpers.FixModelFamiliesAsync(modelFamilies, modelJson.CancellationToken).Result);
                        }

                        break;

                    case var path when path.EndsWith(".modelgroup.json"):
                        var modelGroups = JsonSerializer.Deserialize(modelJson.Content, SourceGenerationContext.Default.DictionaryStringModelGroup);
                        if (modelGroups == null)
                        {
                            throw new InvalidOperationException("Failed to deserialize modelgroup.json");
                        }

                        foreach (var modelGroup in modelGroups)
                        {
                            if (!AddEnumValue(sourceBuilder, modelGroup.Key, modelGroup))
                            {
                                success = false;
                            }

                            foreach (var modelFamily in modelGroup.Value.Models)
                            {
                                if (!AddEnumValue(sourceBuilder, modelFamily.Key, modelFamily))
                                {
                                    success = false;
                                }

                                foreach (var model in modelFamily.Value.Models)
                                {
                                    if (!AddEnumValue(sourceBuilder, $"{modelFamily.Key}{model.Key}", model))
                                    {
                                        success = false;
                                    }
                                }
                            }
                        }

                        if (!success)
                        {
                            File.WriteAllText(modelJson.Path, Helpers.FixModelGroupAsync(modelGroups, modelJson.CancellationToken).Result);
                        }

                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new Exception($"Error when processing '{modelJson.Path}' - Internal error: '{e.Message}'", e);
            }
        }

        sourceBuilder.AppendLine(
            """
            }
            """);

        context.AddSource("ModelType.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));

        bool AddEnumValue<T>(StringBuilder sourceBuilder, string enumValueName, KeyValuePair<string, T> dict)
        {
            if (dict.Value != null)
            {
                sourceBuilder.AppendLine($"    {enumValueName},");
                modelTypes.Add(enumValueName, dict.Value);
                if (dict.Value is Model model &&
                    (model.Size == null || model.Size == 0 ||
                    string.IsNullOrWhiteSpace(model.Id)))
                {
                    return false;
                }
            }

            return true;
        }

        void AddApis(StringBuilder sourceBuilder, Dictionary<string, ApiGroup> apiGroups)
        {
            foreach (var apiGroup in apiGroups)
            {
                AddEnumValue(sourceBuilder, apiGroup.Key, apiGroup);
                if (apiGroup.Value.Apis != null)
                {
                    foreach (var apiDefinition in apiGroup.Value.Apis)
                    {
                        AddEnumValue(sourceBuilder, apiDefinition.Key, apiDefinition);
                    }
                }
            }
        }

        GenerateModelTypeHelpersFile(context, modelTypes);
    }

    private void GenerateModelTypeHelpersFile(SourceProductionContext context, Dictionary<string, object> modelTypes)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Samples;

            internal static class ModelTypeHelpers
            {
            """");

        GenerateModelDetails(
            sourceBuilder,
            modelTypes
                .Where(kp => kp.Value is Model)
                .ToDictionary(kp => kp.Key, kp => (Model)kp.Value));

        var modelFamilies = modelTypes
                .Where(kp => kp.Value is ModelFamily)
                .ToDictionary(kp => kp.Key, kp => (ModelFamily)kp.Value);
        GenerateModelFamilyDetails(sourceBuilder, modelFamilies);

        var apiDefinitions = modelTypes
                .Where(kp => kp.Value is ApiDefinition)
                .ToDictionary(kp => kp.Key, kp => (ApiDefinition)kp.Value);
        GenerateApiDefinitionDetails(sourceBuilder, apiDefinitions);

        var modelGroups = modelTypes
                .Where(kp => kp.Value is IModelGroup)
                .ToDictionary(kp => kp.Key, kp => (IModelGroup)kp.Value);
        GenerateModelGroupDetails(sourceBuilder, modelGroups);

        GenerateModelParentMapping(sourceBuilder, modelGroups, modelFamilies, apiDefinitions);

        GenerateGetModelOrder(sourceBuilder, modelGroups, modelFamilies);

        sourceBuilder.AppendLine("}");

        context.AddSource($"ModelTypeHelpers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private void GenerateModelDetails(StringBuilder sourceBuilder, Dictionary<string, Model> modelTypes)
    {
        sourceBuilder.AppendLine("    internal static Dictionary<ModelType, ModelDetails> ModelDetails { get; } = new ()");
        sourceBuilder.AppendLine("    {");
        foreach (var modelType in modelTypes)
        {
            var modelDefinition = modelType.Value;
            var promptTemplate = modelDefinition.PromptTemplate != null ?
                $"PromptTemplateHelpers.PromptTemplates[PromptTemplateType.{modelDefinition.PromptTemplate}]" :
                "null";
            var hardwareAccelerator = string.Join(", ", modelDefinition.HardwareAccelerators.Select(ha => $"HardwareAccelerator.{ha}"));
            var supportedOnQualcomm = modelDefinition.SupportedOnQualcomm.HasValue ? modelDefinition.SupportedOnQualcomm.Value.ToString().ToLower() : "null";
            var icon = !string.IsNullOrEmpty(modelDefinition.Icon) ? $"\"{modelDefinition.Icon}\"" : "string.Empty";
            var fileFilters = modelDefinition.FileFilters != null ? string.Join(", ", modelDefinition.FileFilters.Select(ff => $"\"{ff}\"")) : string.Empty;
            var aiToolkitActions = modelDefinition.AIToolkitActions != null ? string.Join(", ", modelDefinition.AIToolkitActions.Select(action => $"AIToolkitAction.{action}")) : string.Empty;
            var aiToolkitId = !string.IsNullOrEmpty(modelDefinition.AIToolkitId) ? $"\"{modelDefinition.AIToolkitId}\"" : "null";
            var aiToolkitFinetuningId = !string.IsNullOrEmpty(modelDefinition.AIToolkitFinetuningId) ? $"\"{modelDefinition.AIToolkitFinetuningId}\"" : "null";
            var inputDimensions = modelDefinition.InputDimensions != null ? "[ " + string.Join(", ", modelDefinition.InputDimensions.Select(dimension => "[" + string.Join(", ", dimension.Select(d => d.ToString())) + "]")) + "]" : "null";
            var outputDimensions = modelDefinition.OutputDimensions != null ? "[ " + string.Join(", ", modelDefinition.OutputDimensions.Select(dimension => "[" + string.Join(", ", dimension.Select(d => d.ToString())) + "]")) + "]" : "null";

            sourceBuilder.AppendLine(
                $$""""
                        {
                            ModelType.{{modelType.Key}},
                            new ModelDetails
                            {
                                Name = "{{modelDefinition.Name}}",
                                Id = "{{modelDefinition.Id}}",
                                Description = "{{modelDefinition.Description}}",
                                Url = "{{modelDefinition.Url}}",
                                HardwareAccelerators = [ {{hardwareAccelerator}} ],
                                SupportedOnQualcomm = {{supportedOnQualcomm}},
                                Size = {{modelDefinition.Size}},
                                ParameterSize = "{{modelDefinition.ParameterSize}}",
                                PromptTemplate = {{promptTemplate}},
                                Icon = {{icon}},
                                License = "{{modelDefinition.License}}",
                                FileFilters = [ {{fileFilters}} ],
                                AIToolkitActions = [ {{aiToolkitActions}} ],
                                AIToolkitId = {{aiToolkitId}},
                                AIToolkitFinetuningId = {{aiToolkitFinetuningId}},
                                InputDimensions = {{inputDimensions}},
                                OutputDimensions = {{outputDimensions}}
                            }
                        },
                """");
        }

        sourceBuilder.AppendLine("    };");
    }

    private void GenerateModelFamilyDetails(StringBuilder sourceBuilder, Dictionary<string, ModelFamily> modelFamily)
    {
        sourceBuilder.AppendLine("    internal static Dictionary<ModelType, ModelFamily> ModelFamilyDetails { get; } = new ()");
        sourceBuilder.AppendLine("    {");
        foreach (var modelFamilyType in modelFamily)
        {
            var modelFamilyDefinition = modelFamilyType.Value;
            sourceBuilder.AppendLine(
                $$""""
                        {
                            ModelType.{{modelFamilyType.Key}},
                            new ModelFamily
                            {
                                Id = "{{modelFamilyDefinition.Id}}",
                                Name = "{{modelFamilyDefinition.Name}}",
                                Description = "{{modelFamilyDefinition.Description}}",
                                DocsUrl = "{{modelFamilyDefinition.DocsUrl}}",
                                ReadmeUrl = "{{modelFamilyDefinition.ReadmeUrl}}",
                            }
                        },
                """");
        }

        sourceBuilder.AppendLine("    };");

        sourceBuilder.AppendLine();
    }

    private void GenerateApiDefinitionDetails(StringBuilder sourceBuilder, Dictionary<string, ApiDefinition> apiDefinitions)
    {
        sourceBuilder.AppendLine("    internal static Dictionary<ModelType, ApiDefinition> ApiDefinitionDetails { get; } = new ()");
        sourceBuilder.AppendLine("    {");
        foreach (var apiDefinitionType in apiDefinitions)
        {
            var apiDefinition = apiDefinitionType.Value;
            sourceBuilder.AppendLine(
            $$""""
                        {
                            ModelType.{{apiDefinitionType.Key}},
                            new ApiDefinition
                            {
                                Id = "{{apiDefinition.Id}}",
                                Name = "{{apiDefinition.Name}}",
                                Icon = "{{apiDefinition.Icon}}",
                                IconGlyph = "{{apiDefinition.IconGlyph}}",
                                Description = "{{apiDefinition.Description}}",
                                ReadmeUrl = "{{apiDefinition.ReadmeUrl}}",
                                License = "{{apiDefinition.License}}",
                                {{(!string.IsNullOrWhiteSpace(apiDefinition.SampleIdToShowInDocs) ? $"SampleIdToShowInDocs = \"{apiDefinition.SampleIdToShowInDocs}\"" : string.Empty)}}
                            }
                        },
                """");
        }

        sourceBuilder.AppendLine("    };");

        sourceBuilder.AppendLine();
    }

    private void GenerateModelGroupDetails(StringBuilder sourceBuilder, Dictionary<string, IModelGroup> modelGroups)
    {
        sourceBuilder.AppendLine("    internal static Dictionary<ModelType, ModelGroup> ModelGroupDetails { get; } = new ()");
        sourceBuilder.AppendLine("    {");

        foreach (var modelGroupType in modelGroups)
        {
            var modelGroupDefinition = modelGroupType.Value;
            sourceBuilder.AppendLine(
                $$""""
                        {
                            ModelType.{{modelGroupType.Key}},
                            new ModelGroup
                            {
                                Id = "{{modelGroupDefinition.Id}}",
                                Name = "{{modelGroupDefinition.Name}}",
                                Icon = {{Helpers.EscapeUnicodeString(modelGroupDefinition.Icon)}},
                                IsApi = {{(modelGroupDefinition is ApiGroup).ToString().ToLower()}}
                            }
                        },
                """");
        }

        sourceBuilder.AppendLine("    };");

        sourceBuilder.AppendLine();
    }

    private void GenerateModelParentMapping(StringBuilder sourceBuilder, Dictionary<string, IModelGroup> modelGroups, Dictionary<string, ModelFamily> modelFamilies, Dictionary<string, ApiDefinition> apiDefinitions)
    {
        sourceBuilder.AppendLine("    internal static Dictionary<ModelType, List<ModelType>> ParentMapping { get; } = new ()");
        sourceBuilder.AppendLine("    {");

        var addedKeys = new HashSet<string>();

        void Print(string key, IEnumerable<string> values)
        {
            if (addedKeys.Contains(key))
            {
                return;
            }

            addedKeys.Add(key);

            sourceBuilder.AppendLine($$"""        { ModelType.{{key}}, [""");
            foreach (var value in values)
            {
                sourceBuilder.AppendLine($$"""        ModelType.{{value}}, """);
            }

            sourceBuilder.AppendLine($$"""        ] },""");
        }

        foreach (var modelGroupType in modelGroups.Where(kp => kp.Value is ModelGroup).ToDictionary(kp => kp.Key, kp => (ModelGroup)kp.Value))
        {
            Print(modelGroupType.Key, modelGroupType.Value.Models.Select(m => m.Key));

            foreach (var modelFamilyType in modelGroupType.Value.Models)
            {
                Print(modelFamilyType.Key, modelFamilyType.Value.Models.Select(m => $"{modelFamilyType.Key}{m.Key}"));
            }
        }

        foreach (var apiGroup in modelGroups.Where(kp => kp.Value is ApiGroup).ToDictionary(kp => kp.Key, kp => (ApiGroup)kp.Value))
        {
            Print(apiGroup.Key, apiGroup.Value.Apis.Select(m => m.Key));
        }

        foreach (var modelFamilyType in modelFamilies)
        {
            Print(modelFamilyType.Key, modelFamilyType.Value.Models.Select(m => $"{modelFamilyType.Key}{m.Key}"));
        }

        foreach (var apiDefinition in apiDefinitions)
        {
            Print(apiDefinition.Key, []);
        }

        sourceBuilder.AppendLine("    };");
    }

    private void GenerateGetModelOrder(StringBuilder sourceBuilder, Dictionary<string, IModelGroup> modelGroups, Dictionary<string, ModelFamily> modelFamilies)
    {
        var addedOrders = new Dictionary<string, int>();

        foreach (var modelGroupType in modelGroups)
        {
            addedOrders[modelGroupType.Key] = modelGroupType.Value.Order ?? int.MaxValue;

            if (modelGroupType.Value is ModelGroup modelGroup)
            {
                foreach (var modelFamilyType in modelGroup.Models)
                {
                    addedOrders[modelFamilyType.Key] = modelFamilyType.Value.Order ?? int.MaxValue;
                }
            }
        }

        foreach (var modelFamilyType in modelFamilies)
        {
            addedOrders[modelFamilyType.Key] = modelFamilyType.Value.Order ?? int.MaxValue;
        }

        sourceBuilder.AppendLine("    internal static int GetModelOrder(ModelType modelType)");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        return modelType switch");
        sourceBuilder.AppendLine("        {");
        foreach (var keyOrder in addedOrders.OrderBy(kvp => kvp.Value))
        {
            sourceBuilder.AppendLine(
                $$""""
                        ModelType.{{keyOrder.Key}} => {{keyOrder.Value}},
                """");
        }

        sourceBuilder.AppendLine("            _ => int.MaxValue,");
        sourceBuilder.AppendLine("        };");

        sourceBuilder.AppendLine("    }");
    }
}

#pragma warning restore RS1035 // Do not use APIs banned for analyzers