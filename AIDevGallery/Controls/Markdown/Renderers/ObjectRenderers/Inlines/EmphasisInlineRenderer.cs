// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class EmphasisInlineRenderer : UWPObjectRenderer<EmphasisInline>
{
    protected override void Write(WinUIRenderer renderer, EmphasisInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        MyEmphasisInline? span = null;

        switch (obj.DelimiterChar)
        {
            case '*':
            case '_':
                span = new MyEmphasisInline(obj);
                if (obj.DelimiterCount == 2)
                {
                    span.SetBold();
                }
                else
                {
                    span.SetItalic();
                }

                break;
            case '~':
                span = new MyEmphasisInline(obj);
                if (obj.DelimiterCount == 2)
                {
                    span.SetStrikeThrough();
                }
                else
                {
                    span.SetSubscript();
                }

                break;
            case '^':
                span = new MyEmphasisInline(obj);
                span.SetSuperscript();
                break;
        }

        if (span != null)
        {
            renderer.Push(span);
            renderer.WriteChildren(obj);
            renderer.Pop();
        }
        else
        {
            renderer.WriteChildren(obj);
        }
    }
}