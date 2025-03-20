// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

﻿using Markdig.Renderers;
﻿using Markdig.Syntax;

﻿namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers;

internal abstract class UWPObjectRenderer<TObject> : MarkdownObjectRenderer<WinUIRenderer, TObject>
    where TObject : MarkdownObject
{
}