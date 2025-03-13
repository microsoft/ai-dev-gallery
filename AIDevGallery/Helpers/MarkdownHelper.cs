// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;

namespace AIDevGallery.Helpers;

internal static class MarkdownHelper
{
    public static string PreprocessMarkdown(string markdown)
    {
        markdown = Regex.Replace(markdown, @"\A---\n[\s\S]*?---\n", string.Empty, RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]", "> **ℹ️ Important:**", RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!NOTE\]", "> **❗ Note:**", RegexOptions.Multiline);
        markdown = Regex.Replace(markdown, @"^>\s*\[!TIP\]", "> **💡 Tip:**", RegexOptions.Multiline);

        return markdown;
    }

    public static MarkdownConfig GetMarkdownConfig()
    {
        return new MarkdownConfig()
        {
            Themes = new CommunityToolkit.WinUI.Controls.MarkdownTextBlockRns.MarkdownThemes()
            {
                HeadingForeground = (SolidColorBrush)App.Current.Resources["TextFillColorPrimaryBrush"],
                H1FontSize = 14,
                H1FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                H1Margin = new Thickness(0, 0, 0, 16),
                H2FontSize = 14,
                H2FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                H2Margin = new Thickness(0, 16, 0, 8),
                H3FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                H3Margin = new Thickness(0, 16, 0, 8),
            }
        };
    }
}