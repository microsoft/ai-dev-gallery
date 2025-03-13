// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock;

internal class LinkClickedEventArgs : EventArgs
{
    public Uri Uri { get; }

    public LinkClickedEventArgs(Uri uri)
    {
        this.Uri = uri;
    }
}