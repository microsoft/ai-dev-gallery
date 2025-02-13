// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Diagnostics;
using AIDevGallery.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable RS1035 // Do not use APIs banned for analyzers

namespace AIDevGallery.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal class SamplesSourceGenerator : IIncrementalGenerator
{
    private void ExecuteSharedCodeEnumGeneration(SourceProductionContext context, ImmutableArray<INamedTypeSymbol?> typeSymbols)
    {
        // Filter types by the target namespace
        var typesInNamespace = typeSymbols
            .Where(typeSymbol => typeSymbol != null &&
                (typeSymbol.ContainingNamespace.ToDisplayString().StartsWith("AIDevGallery.Samples.SharedCode", StringComparison.Ordinal) ||
                typeSymbol.GetFullyQualifiedName().Equals("global::AIDevGallery.Utils.DeviceUtils", StringComparison.Ordinal)))
            .ToList();

        if (!typesInNamespace.Any())
        {
            return;
        }

        // Generate the enum source
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("#nullable enable");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine($"namespace AIDevGallery.Samples");
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine("    internal enum SharedCodeEnum");
        sourceBuilder.AppendLine("    {");

        List<string> filePaths = [];

        foreach (var type in typesInNamespace)
        {
            var filePath = type!.Locations[0].SourceTree?.FilePath;

            if (filePath != null && !filePath.Contains(@"\obj\"))
            {
                if (!filePaths.Contains(filePath))
                {
                    filePaths.Add(filePath);
                }
            }
        }

        filePaths.Add("NativeMethods.txt");

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var filePathWithoutExtension = filePath.Substring(0, filePath.Length - extension.Length);
            if (fileName.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase) && File.Exists(filePathWithoutExtension))
            {
                fileName = Path.GetFileNameWithoutExtension(filePathWithoutExtension);
                sourceBuilder.AppendLine($"        {fileName}Cs,");
                sourceBuilder.AppendLine($"        {fileName}Xaml,");
            }
            else if (File.Exists(Path.ChangeExtension(filePath, ".xaml")))
            {
                sourceBuilder.AppendLine($"        {fileName}Cs,");
                sourceBuilder.AppendLine($"        {fileName}Xaml,");
            }
            else
            {
                sourceBuilder.AppendLine($"        {fileName},");
            }
        }

        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("    internal static class SharedCodeHelpers");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine("        internal static string GetName(SharedCodeEnum sharedCode)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            return sharedCode switch");
        sourceBuilder.AppendLine("            {");
        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var filePathXaml = Path.ChangeExtension(filePath, ".xaml");
            var extension = Path.GetExtension(filePath);
            var filePathWithoutExtension = filePath.Substring(0, filePath.Length - extension.Length);
            if (fileName.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase) && File.Exists(filePathWithoutExtension))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
                sourceBuilder.AppendLine($"                 SharedCodeEnum.{fileName}Cs => \"{fileName}.xaml.cs\",");
                sourceBuilder.AppendLine($"                 SharedCodeEnum.{fileName}Xaml => \"{fileName}.xaml\",");
            }
            else if (File.Exists(filePathXaml))
            {
                sourceBuilder.AppendLine($"                 SharedCodeEnum.{fileName}Xaml => \"{Path.GetFileName(filePathXaml)}\",");
                sourceBuilder.AppendLine($"                 SharedCodeEnum.{fileName}Cs => \"{Path.GetFileName(filePath)}\",");
            }
            else
            {
                sourceBuilder.AppendLine($"                 SharedCodeEnum.{fileName} => \"{Path.GetFileName(filePath)}\",");
            }
        }

        sourceBuilder.AppendLine("                _ => string.Empty,");
        sourceBuilder.AppendLine("            };");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("        internal static string GetSource(SharedCodeEnum sharedCode)");
        sourceBuilder.AppendLine("        {");
        sourceBuilder.AppendLine("            return sharedCode switch");
        sourceBuilder.AppendLine("            {");
        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var filePathXaml = Path.ChangeExtension(filePath, ".xaml");
            var extension = Path.GetExtension(filePath);
            var filePathWithoutExtension = filePath.Substring(0, filePath.Length - extension.Length);

            // handle .xaml.cs files
            if (File.Exists(filePathWithoutExtension))
            {
                filePathXaml = filePathWithoutExtension;
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }


            if (File.Exists(filePathXaml))
            {
                var fileContentXaml = XamlSourceCleanUp(File.ReadAllText(filePathXaml));
                sourceBuilder.AppendLine(
                    $$""""""
                                    SharedCodeEnum.{{fileName}}Xaml => 
                    """
                    {{fileContentXaml}}
                    """,
                    """""");

                fileName = $"{fileName}Cs";
            }

            string fileContent;
            if (fileName == "NativeMethods")
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("AIDevGallery.SourceGenerator.NativeMethods.txt"))
                {
                    using (StreamReader reader = new(stream))
                    {
                        fileContent = reader.ReadToEnd().Trim();
                    }
                }
            }
            else
            {
                fileContent = SampleSourceCleanUp(File.ReadAllText(filePath), filePath);
            }

            sourceBuilder.AppendLine(
                $$""""""
                                SharedCodeEnum.{{fileName}} => 
                """
                {{fileContent}}
                """,
                """""");
        }

        sourceBuilder.AppendLine("                _ => string.Empty,");
        sourceBuilder.AppendLine("            };");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("    }");

        sourceBuilder.AppendLine("}");

        context.AddSource("SharedCodeEnum.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private static readonly Regex GallerySampleAttributeRemovalRegex = new(@"\n(\s)*\[GallerySample\((?>[^()]+|\((?<DEPTH>)|\)(?<-DEPTH>))*(?(DEPTH)(?!))\)\]", RegexOptions.Compiled);
    private static readonly Regex ExcludedElementXamlRemovalRegex = new(@"<EXCLUDE:(([^<]*\/>)|(.*<\/EXCLUDE:[a-zA-Z]*>))", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ExcludedAttrbitueXamlRemovalRegex = new(@"EXCLUDE:[^""]*""[^""]*""", RegexOptions.Singleline | RegexOptions.Compiled);

    private static string SampleSourceCleanUp(string input, string filePath)
    {
        var header = @"// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.";
        if (input.StartsWith(header, StringComparison.Ordinal))
        {
            input = input.Substring(header.Length)
                .TrimStart(Environment.NewLine.ToCharArray())
                .TrimStart();
        }

        input = GallerySampleAttributeRemovalRegex.Replace(input, string.Empty);
        input = RemoveExcludedLinesCs(input, filePath);

        return input;
    }

    private static string XamlSourceCleanUp(string input)
    {
        if (input.Contains("xmlns:EXCLUDE"))
        {
            input = ExcludedElementXamlRemovalRegex.Replace(input, string.Empty);
            input = ExcludedAttrbitueXamlRemovalRegex.Replace(input, string.Empty);
            input = RemoveEmptyLines(input);
        }

        return input;
    }

    private static string RemoveEmptyLines(string input)
    {
        var lines = input.Split([Environment.NewLine], StringSplitOptions.None);
        var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, nonEmptyLines);
    }

    private static string RemoveExcludedLinesCs(string input, string filePath)
    {
        List<string> lines = new(input.Split([Environment.NewLine], StringSplitOptions.None));

        for (int i = 0; i < lines.Count;)
        {
            if (lines[i].Contains("//<exclude>") || lines[i].Contains("// <exclude>"))
            {
                while (!lines[i].Contains("//</exclude>") && !lines[i].Contains("// </exclude>"))
                {
                    lines.RemoveAt(i);
                    if (i >= lines.Count)
                    {
                        throw new InvalidOperationException($"<exclude> block is never closed in file '{filePath}'");
                    }
                }

                lines.RemoveAt(i);
            }
            else if (lines[i].Contains("// <exclude-line>") || lines[i].Contains("//<exclude-line>"))
            {
                lines.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static INamedTypeSymbol? GetTypeSymbol(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        return context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is TypeDeclarationSyntax,
                transform: static (ctx, _) => GetTypeSymbol(ctx))
            .Where(static m => m != null)
            .Collect();

        // Combine the results of the syntax provider and add the source
        context.RegisterSourceOutput(typeDeclarations, ExecuteSharedCodeEnumGeneration);

        var gallerySamplePipeline = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WellKnownTypeNames.GallerySampleAttribute,
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: static (context, cancellationToken) =>
                {
                    INamedTypeSymbol typeSymbol = (INamedTypeSymbol)context.TargetSymbol;

                    if (context.TargetSymbol!.Locations[0].SourceTree == null)
                    {
                        return null;
                    }

                    WellKnownTypeSymbols typeSymbols = new(context.SemanticModel.Compilation);

                    if (!typeSymbol.TryGetAttributeWithType(typeSymbols.GallerySampleAttribute, out AttributeData? attributeData))
                    {
                        return null;
                    }

                    var filePath = context.TargetSymbol!.Locations[0].SourceTree!.FilePath;
                    var folder = Path.GetDirectoryName(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    if (fileNameWithoutExtension.EndsWith(".xaml"))
                    {
                        fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithoutExtension);
                    }

                    var sampleXamlFile = Directory.GetFiles(folder).Where(f => f.EndsWith($"\\{fileNameWithoutExtension}.xaml", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                    var sampleXamlFileContent = XamlSourceCleanUp(File.ReadAllText(sampleXamlFile));

                    var pageType = context.TargetSymbol.GetFullyQualifiedName();

                    var sampleXamlCsFile = Directory.GetFiles(folder).Where(f => f.EndsWith($"\\{fileNameWithoutExtension}.xaml.cs", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                    var sampleXamlCsFileContent = SampleSourceCleanUp(File.ReadAllText(sampleXamlCsFile), sampleXamlCsFile);

                    if (attributeData == null)
                    {
                        return null;
                    }

                    try
                    {
                        string name = (string)attributeData.NamedArguments.First(a => a.Key == "Name").Value.Value!;
                        string id = attributeData.NamedArguments.FirstOrDefault(a => a.Key == "Id").Value.Value as string ?? string.Empty;
                        string icon = attributeData.NamedArguments.FirstOrDefault(a => a.Key == "Icon").Value.Value as string ?? string.Empty;
                        string? scenario = attributeData.NamedArguments.FirstOrDefault(a => a.Key == "Scenario").Value.Value?.ToString();
                        string[]? nugetPackageReferences = null;
                        var nugetPackageReferencesRef = attributeData.NamedArguments.FirstOrDefault(a => a.Key == "NugetPackageReferences");
                        if (!nugetPackageReferencesRef.Value.IsNull)
                        {
                            nugetPackageReferences = nugetPackageReferencesRef.Value.Values.Select(v => (string)v.Value!).ToArray();
                        }

                        string[]? assetFilenames = null;
                        var assetFilenamesRef = attributeData.NamedArguments.FirstOrDefault(a => a.Key == "AssetFilenames");
                        if(!assetFilenamesRef.Value.IsNull)
                        {
                            assetFilenames = assetFilenamesRef.Value.Values.Select(v => (string)v.Value!).ToArray();
                        }

                        return new SampleModel(
                            Owner: typeSymbol.GetFullyQualifiedName(),
                            Name: name,
                            PageType: pageType,
                            XAMLCode: sampleXamlFileContent,
                            CSCode: sampleXamlCsFileContent,
                            Id: id,
                            Icon: icon,
                            Scenario: scenario,
                            NugetPackageReferences: nugetPackageReferences,
                            AssetFilenames: assetFilenames,
                            attributeData.GetLocation());
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException($"Error when processing {typeSymbol.GetFullyQualifiedName()} - GallerySampleAttribute: {attributeData}");
                    }
                })
            .Where(static m => m != null)
            .Select(static (m, _) => m!)
            .Collect();

        context.RegisterImplementationSourceOutput(gallerySamplePipeline, static (context, samples) =>
        {
            var sourceBuilder = new StringBuilder();

            sourceBuilder.AppendLine(
                $$""""
                #nullable enable

                using System.Collections.Generic;
                using System.Linq;
                using AIDevGallery.Models;
                using AIDevGallery.Samples.Attributes;

                namespace AIDevGallery.Samples;

                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                internal static class SampleDetails
                {
                    private static List<SharedCodeEnum> GetSharedCodeFrom(System.Type type)
                    {
                        return type.GetCustomAttributes(typeof(GallerySampleAttribute), false)
                            .Cast<GallerySampleAttribute>()
                            .First().SharedCode?.ToList() ?? new ();
                    }

                    private static List<ModelType>? GetModelTypesFrom(int index, System.Type type)
                    {
                        if (index == 2)
                        {
                            return type.GetCustomAttributes(typeof(GallerySampleAttribute), false)
                                .Cast<GallerySampleAttribute>()
                                .First().Model2Types?.ToList();
                        }

                        return type.GetCustomAttributes(typeof(GallerySampleAttribute), false)
                            .Cast<GallerySampleAttribute>()
                            .First().Model1Types.ToList();
                    }

                    internal static List<Sample> Samples = [
                """");

            var packageVersions = Helpers.GetPackageVersions();

            foreach (var sample in samples)
            {
                if (sample.Scenario == null)
                {
                    // TODO: Remove when APIs are added, and mark scenario as required on GallerySampleAttribute
                    Debug.WriteLine($"Scenario is null for {sample.Name}");
                }
                else
                {
                    var nugetPackageReferences = sample.NugetPackageReferences != null && sample.NugetPackageReferences.Length > 0
                        ? string.Join(", ", sample.NugetPackageReferences.Select(r => $"\"{r}\""))
                        : string.Empty;

                    if (sample.NugetPackageReferences != null && sample.NugetPackageReferences.Length > 0)
                    {
                        foreach (var packageReference in sample.NugetPackageReferences)
                        {
                            if (!packageVersions.ContainsKey(packageReference))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.NugetPackageNotUsed,
                                    sample.Location,
                                    packageReference));
                            }
                        }
                    }

                    var assetFilenames = sample.AssetFilenames != null && sample.AssetFilenames.Length > 0
                        ? string.Join(", ", sample.AssetFilenames.Select(a => $"\"{a}\""))
                        : string.Empty;

                    sourceBuilder.AppendLine(
                        $$""""""
                            new Sample
                            {
                                Name = "{{sample.Name}}",
                                PageType = typeof({{sample.PageType}}),
                                XAMLCode =
                        """
                        {{sample.XAMLCode}}
                        """,
                                CSCode =
                        """"
                        {{sample.CSCode}}
                        """",
                                Id = "{{sample.Id}}",
                                Icon = {{Helpers.EscapeUnicodeString(sample.Icon)}},
                                Scenario = (ScenarioType){{sample.Scenario}},
                                Model1Types = GetModelTypesFrom(1, typeof({{sample.Owner}}))!,
                                Model2Types = GetModelTypesFrom(2, typeof({{sample.Owner}})),
                                SharedCode = GetSharedCodeFrom(typeof({{sample.Owner}})),
                                NugetPackageReferences = [ {{nugetPackageReferences}} ],
                                AssetFilenames = [ {{assetFilenames}} ]
                            },
                        """""");
                }
            }

            sourceBuilder.AppendLine(
                """
                    ];
                }
                """);

            context.AddSource($"SampleDetails.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        });
    }

    private record ScenarioModel(string EnumName, string ScenarioCategoryType, string Id, string Name, string Description);
    private record SampleModel(string Owner, string Name, string PageType, string XAMLCode, string CSCode, string Id, string Icon, string? Scenario, string[]? NugetPackageReferences, string[]? AssetFilenames, Location? Location);
    private record ModelDefinitionModel(string EnumName, string Parent, string Name, string Id, string Description, string Url, string HardwareAccelerator, long Size, string ParameterSize);
}
#pragma warning restore RS1035 // Do not use APIs banned for analyzers