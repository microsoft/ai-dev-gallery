// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements.Html;
using HtmlAgilityPack;
using System.Linq;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock;

internal class HtmlWriter
{
    public static void WriteHtml(WinUIRenderer renderer, HtmlNodeCollection nodes)
    {
        if (nodes == null || nodes.Count == 0)
        {
            return;
        }

        foreach (var node in nodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                renderer.WriteText(node.InnerText);
            }
            else if (node.NodeType == HtmlNodeType.Element && node.Name.TagToType() == TextElements.HtmlElementType.Inline)
            {
                // detect br here
                var inlineTagName = node.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture);
                if (inlineTagName == "br")
                {
                    renderer.WriteInline(new MyLineBreak());
                }
                else if (inlineTagName == "a")
                {
                    IAddChild hyperLink;
                    var url = node.GetAttribute("href", "#");
                    if (node.ChildNodes.Any(n => n.Name != "#text"))
                    {
                        var myHyperlinkButton = new MyHyperlinkButton(node, renderer.Config.BaseUrl);
                        myHyperlinkButton.ClickEvent += (sender, e) =>
                        {
                            renderer.MarkdownTextBlock.RaiseLinkClickedEvent(url);
                        };
                        hyperLink = myHyperlinkButton;
                    }
                    else
                    {
                        var myHyperlink = new MyHyperlink(node, renderer.Config.BaseUrl);
                        myHyperlink.ClickEvent += (sender, e) =>
                        {
                            renderer.MarkdownTextBlock.RaiseLinkClickedEvent(url);
                        };
                        hyperLink = myHyperlink;
                    }

                    renderer.Push(hyperLink);
                    WriteHtml(renderer, node.ChildNodes);
                    renderer.Pop();
                }
                else if (inlineTagName == "img")
                {
                    var image = new MyImage(node, renderer.Config);
                    renderer.WriteInline(image);
                }
                else
                {
                    var inline = new MyInline(node);
                    renderer.Push(inline);
                    WriteHtml(renderer, node.ChildNodes);
                    renderer.Pop();
                }
            }
            else if (node.NodeType == HtmlNodeType.Element && node.Name.TagToType() == TextElements.HtmlElementType.Block)
            {
                IAddChild block;
                var tag = node.Name.ToLowerInvariant();
                if (tag == "details")
                {
                    block = new MyDetails(node);
                    if (node.ChildNodes.FirstOrDefault(x => x.Name == "summary" || x.Name == "header") is HtmlNode item)
                    {
                        node.ChildNodes.Remove(item);
                    }

                    renderer.Push(block);
                    WriteHtml(renderer, node.ChildNodes);
                }
                else if (tag.IsHeading())
                {
                    var heading = new MyHeading(node, renderer.Config);
                    renderer.Push(heading);
                    WriteHtml(renderer, node.ChildNodes);
                }
                else
                {
                    block = new MyBlock(node);
                    renderer.Push(block);
                    WriteHtml(renderer, node.ChildNodes);
                }

                renderer.Pop();
            }
        }
    }
}