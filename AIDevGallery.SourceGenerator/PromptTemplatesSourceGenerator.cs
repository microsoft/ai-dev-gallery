// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AIDevGallery.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal class PromptTemplatesSourceGenerator : IIncrementalGenerator
{
    private Dictionary<string, PromptTemplate>? promptTemplates = null;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        string promptTemplateJson;
        var assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("AIDevGallery.SourceGenerator.promptTemplates.json"))
        {
            using (StreamReader reader = new(stream))
            {
                promptTemplateJson = reader.ReadToEnd().Trim();
            }
        }

        promptTemplates = JsonSerializer.Deserialize(promptTemplateJson, SourceGenerationContext.Default.DictionaryStringPromptTemplate);
        context.RegisterPostInitializationOutput(Execute);
    }

    public void Execute(IncrementalGeneratorPostInitializationContext context)
    {
        if (promptTemplates == null)
        {
            return;
        }

        GeneratePromptTemplatesTypeFile(context, promptTemplates);

        GeneratePromptTemplatesHelpersFile(context, promptTemplates);
    }

    private void GeneratePromptTemplatesHelpersFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, PromptTemplate> promptTemplates)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Samples;

            internal static partial class PromptTemplateHelpers
            {
            """");

        sourceBuilder.AppendLine("    internal static Dictionary<PromptTemplateType, PromptTemplate> PromptTemplates { get; } = new ()");
        sourceBuilder.AppendLine("    {");
        foreach (var promptTemplate in promptTemplates)
        {
            sourceBuilder.AppendLine(
                    $$""""
                            {
                                PromptTemplateType.{{promptTemplate.Key}},
                                new PromptTemplate
                                {
                    """");
            if (promptTemplate.Value.System != null)
            {
                sourceBuilder.AppendLine($$"""                System = {{Helpers.EscapeUnicodeString(promptTemplate.Value.System)}},""");
            }

            sourceBuilder.AppendLine(
                    $$""""
                                    User = {{Helpers.EscapeUnicodeString(promptTemplate.Value.User)}},
                    """");
            if (promptTemplate.Value.Assistant != null)
            {
                sourceBuilder.AppendLine($$"""                Assistant = {{Helpers.EscapeUnicodeString(promptTemplate.Value.Assistant)}},""");
            }

            sourceBuilder.AppendLine(
                    $$""""
                                    Stop = [{{string.Join(", ", promptTemplate.Value.Stop.Select(Helpers.EscapeUnicodeString))}}]
                                }
                            },
                    """");
        }

        sourceBuilder.AppendLine("    };");

        sourceBuilder.AppendLine("}");

        context.AddSource($"PromptTemplateHelpers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private static void GeneratePromptTemplatesTypeFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, PromptTemplate> promptTemplates)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Models;

            internal enum PromptTemplateType
            {
            """");

        foreach (var promptTemplate in promptTemplates)
        {
            sourceBuilder.AppendLine($"    {promptTemplate.Key},");
        }

        sourceBuilder.AppendLine(
            $$""""
            }
            """");

        context.AddSource("PromptTemplateType.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }
}