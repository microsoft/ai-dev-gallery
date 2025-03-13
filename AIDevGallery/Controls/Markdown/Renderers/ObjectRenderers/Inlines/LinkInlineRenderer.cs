// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class LinkInlineRenderer : UWPObjectRenderer<LinkInline>
{
    protected override void Write(WinUIRenderer renderer, LinkInline link)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(link);

        var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? link.Url : link.Url;

        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
        {
            url = "#";
        }

        if (link.IsImage)
        {
            var image = new MyImage(link, CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Extensions.GetUri(url, renderer.Config.BaseUrl), renderer.Config);
            renderer.WriteInline(image);
        }
        else
        {
            if (link.FirstChild is LinkInline linkInlineChild && linkInlineChild.IsImage)
            {
                var myHyperlinkButton = new MyHyperlinkButton(link, renderer.Config.BaseUrl);
                myHyperlinkButton.ClickEvent += (sender, e) =>
                {
                    renderer.MarkdownTextBlock.RaiseLinkClickedEvent(((HyperlinkButton)sender).NavigateUri);
                };
                renderer.Push(myHyperlinkButton);
            }
            else
            {
                var hyperlink = new MyHyperlink(link, renderer.Config.BaseUrl);
                hyperlink.ClickEvent += (sender, e) =>
                {
                    renderer.MarkdownTextBlock.RaiseLinkClickedEvent(sender.NavigateUri);
                };

                renderer.Push(hyperlink);
            }

            renderer.WriteChildren(link);
            renderer.Pop();
        }
    }
}