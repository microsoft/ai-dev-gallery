// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyInlineText : IAddChild
{
    private Run _run;

    public TextElement TextElement
    {
        get => _run;
    }

    public MyInlineText(string text)
    {
        _run = new Run()
        {
            Text = text
        };
    }

    public void AddChild(IAddChild child)
    {
    }
}