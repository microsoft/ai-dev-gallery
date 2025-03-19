// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;
using Markdig.Syntax;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class HtmlBlockRenderer : UWPObjectRenderer<HtmlBlock>
{
    protected override void Write(WinUIRenderer renderer, HtmlBlock obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        var stringBuilder = new StringBuilder();
        foreach (var line in obj.Lines.Lines)
        {
            var lineText = line.Slice.ToString().Trim();
            if (string.IsNullOrWhiteSpace(lineText))
            {
                continue;
            }

            stringBuilder.AppendLine(lineText);
        }

        var html = Regex.Replace(stringBuilder.ToString(), @"\t|\n|\r", string.Empty, RegexOptions.Compiled);
        html = Regex.Replace(html, @"&nbsp;", " ", RegexOptions.Compiled);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        HtmlWriter.WriteHtml(renderer, doc.DocumentNode.ChildNodes);
    }
}