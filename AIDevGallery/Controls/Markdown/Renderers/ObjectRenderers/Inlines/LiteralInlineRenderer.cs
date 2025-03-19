// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class LiteralInlineRenderer : UWPObjectRenderer<LiteralInline>
{
    protected override void Write(WinUIRenderer renderer, LiteralInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        if (obj.Content.IsEmpty)
        {
            return;
        }

        renderer.WriteText(ref obj.Content);
    }
}