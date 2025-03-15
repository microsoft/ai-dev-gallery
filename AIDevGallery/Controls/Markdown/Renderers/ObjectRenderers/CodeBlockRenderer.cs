// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class CodeBlockRenderer : UWPObjectRenderer<CodeBlock>
{
    protected override void Write(WinUIRenderer renderer, CodeBlock obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        var code = new MyCodeBlock(obj, renderer.Config);
        renderer.Push(code);
        renderer.Pop();
    }
}