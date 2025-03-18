// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class ListRenderer : UWPObjectRenderer<ListBlock>
{
    protected override void Write(WinUIRenderer renderer, ListBlock listBlock)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(listBlock);

        var list = new MyList(listBlock);

        renderer.Push(list);

        foreach (var item in listBlock)
        {
            var listItemBlock = (ListItemBlock)item;
            var listItem = new MyBlockContainer(listItemBlock);
            renderer.Push(listItem);
            renderer.WriteChildren(listItemBlock);
            renderer.Pop();
        }

        renderer.Pop();
    }
}