// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal interface IAddChild
{
    TextElement TextElement { get; }
    void AddChild(IAddChild child);
}