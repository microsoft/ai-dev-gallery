// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyLineBreak : IAddChild
{
    private LineBreak _lineBreak;

    public TextElement TextElement
    {
        get => _lineBreak;
    }

    public MyLineBreak()
    {
        _lineBreak = new LineBreak();
    }

    public void AddChild(IAddChild child)
    {
    }
}