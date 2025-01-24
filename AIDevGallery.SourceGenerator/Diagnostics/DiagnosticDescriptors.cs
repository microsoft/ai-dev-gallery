// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace AIDevGallery.SourceGenerator.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NonUniqueId = new(
        id: "AIDevGallery0001",
        title: "Duplicate Id for [GallerySample] sample",
        messageFormat: "Id '{0}' is used more than once",
        category: nameof(SamplesSourceGenerator),
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All gallery samples must have unique ids.");

    public static readonly DiagnosticDescriptor NugetPackageNotUsed = new(
        id: "AIDevGallery0002",
        title: "Nuget package not used",
        messageFormat: "Nuget package '{0}' is not used in the Gallery app",
        category: nameof(SamplesSourceGenerator),
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Nuget package references must be used in the sample app to be used by a sample.");
}