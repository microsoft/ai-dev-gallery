// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using ColorCode;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Text;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyCodeBlock : IAddChild
{
    private CodeBlock _codeBlock;
    private Paragraph _paragraph;
    private MarkdownConfig _config;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyCodeBlock(CodeBlock codeBlock, MarkdownConfig config)
    {
        _codeBlock = codeBlock;
        _config = config;
        _paragraph = new Paragraph();

        var richTextBlock = new RichTextBlock()
        {
            FontFamily = new FontFamily("Cascadia Code"),
            IsTextSelectionEnabled = true
        };

        if (codeBlock is FencedCodeBlock fencedCodeBlock)
        {
            var formatter = new RichTextBlockFormatter(AppUtils.GetCodeHighlightingStyleFromElementTheme(richTextBlock.ActualTheme));
            var stringBuilder = new StringBuilder();

            // go through all the lines backwards and only add the lines to a stack if we have encountered the first non-empty line
            var lines = fencedCodeBlock.Lines.Lines;
            var stack = new Stack<string>();
            var encounteredFirstNonEmptyLine = false;
            if (lines != null)
            {
                for (var i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line.ToString()) && !encounteredFirstNonEmptyLine)
                    {
                        continue;
                    }

                    encounteredFirstNonEmptyLine = true;
                    stack.Push(line.ToString());
                }

                // append all the lines in the stack to the string builder
                while (stack.Count > 0)
                {
                    stringBuilder.AppendLine(stack.Pop());
                }
            }

            formatter.FormatRichTextBlock(stringBuilder.ToString(), fencedCodeBlock.ToLanguage(), richTextBlock);
        }
        else
        {
            foreach (var line in codeBlock.Lines.Lines)
            {
                var paragraph = new Paragraph();
                var lineString = line.ToString();
                if (!string.IsNullOrWhiteSpace(lineString))
                {
                    paragraph.Inlines.Add(new Run() { Text = lineString });
                }

                richTextBlock.Blocks.Add(paragraph);
            }
        }

        var container = new InlineUIContainer()
        {
            Child = new ScrollViewer()
            {
                HorizontalScrollMode = ScrollMode.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 16),
                Content = new Border()
                {
                    Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseAltBrush"],
                    Padding = _config.Themes.Padding,
                    Margin = _config.Themes.InternalMargin,
                    CornerRadius = _config.Themes.CornerRadius,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = richTextBlock
                }
            }
        };

        _paragraph.Inlines.Add(container);
    }

    public void AddChild(IAddChild child)
    {
    }
}