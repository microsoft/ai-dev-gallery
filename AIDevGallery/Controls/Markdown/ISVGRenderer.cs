// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock;

internal interface ISVGRenderer
{
    Task<Image> SvgToImage(string svgString);
}