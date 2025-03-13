// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class AutoLinkInlineRenderer : UWPObjectRenderer<AutolinkInline>
{
    protected override void Write(WinUIRenderer renderer, AutolinkInline link)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(link);

        var url = link.Url;
        if (link.IsEmail)
        {
            url = "mailto:" + url;
        }

        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
        {
            url = "#";
        }

        var autolink = new MyAutolinkInline(link);
        autolink.ClickEvent += (sender, e) =>
        {
            renderer.MarkdownTextBlock.RaiseLinkClickedEvent(sender.NavigateUri);
        };

        renderer.Push(autolink);

        renderer.WriteText(link.Url);
        renderer.Pop();
    }
}