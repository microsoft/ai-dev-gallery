// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock;

internal class LinkClickedEventArgs : EventArgs
{
    public string Url { get; }

    public LinkClickedEventArgs(string url)
    {
        this.Url = url;
    }
}