// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace AIDevGallery.SourceGenerator.Extensions;

internal static class AttributeDataExtensions
{
    public static Location? GetLocation(this AttributeData attributeData)
    {
        if (attributeData.ApplicationSyntaxReference is { } syntaxReference)
        {
            return syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span);
        }

        return null;
    }
}