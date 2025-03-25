// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class ThematicBreakRenderer : UWPObjectRenderer<ThematicBreakBlock>
{
    protected override void Write(WinUIRenderer renderer, ThematicBreakBlock obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        var thematicBreak = new MyThematicBreak(obj);

        renderer.WriteBlock(thematicBreak);
    }
}