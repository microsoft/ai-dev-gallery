// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class LineBreakInlineRenderer : UWPObjectRenderer<LineBreakInline>
{
    protected override void Write(WinUIRenderer renderer, LineBreakInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        if (obj.IsHard)
        {
            renderer.WriteInline(new MyLineBreak());
        }
        else
        {
            // Soft line break.
            renderer.WriteText(" ");
        }
    }
}