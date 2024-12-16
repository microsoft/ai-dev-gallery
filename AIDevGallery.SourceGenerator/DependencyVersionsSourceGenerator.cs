// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace AIDevGallery.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal class DependencyVersionsSourceGenerator : IIncrementalGenerator
{
    private Dictionary<string, string>? packageVersions = null;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        packageVersions = Helpers.GetPackageVersions();

        context.RegisterPostInitializationOutput(Execute);
    }

    public void Execute(IncrementalGeneratorPostInitializationContext context)
    {
        if (packageVersions == null)
        {
            return;
        }

        GeneratePackageVersionsFile(context, packageVersions);
    }

    private static void GeneratePackageVersionsFile(IncrementalGeneratorPostInitializationContext context, Dictionary<string, string> packageVersions)
    {
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine(
            $$""""
            #nullable enable

            using System.Collections.Generic;
            using AIDevGallery.Models;

            namespace AIDevGallery.Samples;

            internal static partial class PackageVersionHelpers
            {
            """");

        sourceBuilder.AppendLine("    internal static Dictionary<string, string> PackageVersions { get; } = new ()");
        sourceBuilder.AppendLine("    {");
        foreach (var packageVersion in packageVersions)
        {
            sourceBuilder.AppendLine(
                    $$""""
                            { "{{packageVersion.Key}}", "{{packageVersion.Value}}" },
                    """");
        }

        sourceBuilder.AppendLine("    };");

        sourceBuilder.AppendLine("}");

        context.AddSource($"PackageVersionHelpers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }
}