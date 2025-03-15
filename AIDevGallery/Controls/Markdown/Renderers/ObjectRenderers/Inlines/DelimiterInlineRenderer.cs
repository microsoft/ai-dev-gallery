// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class DelimiterInlineRenderer : UWPObjectRenderer<DelimiterInline>
{
    protected override void Write(WinUIRenderer renderer, DelimiterInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        // delimiter's children are emphasized text, we don't need to explicitly render them
        // Just need to render the children of the delimiter, I think..
        // renderer.WriteText(obj.ToLiteral());
        renderer.WriteChildren(obj);
    }
}