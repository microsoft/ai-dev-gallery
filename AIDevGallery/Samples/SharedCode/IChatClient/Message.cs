// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Samples.SharedCode;

internal partial class Message : ObservableObject
{
    [ObservableProperty]
    public partial string Content { get; set; }
    
    [ObservableProperty]
    public partial List<ContentBlock> ContentBlocks { get; set; }
    
    public DateTime MsgDateTime { get; private set; }

    public ChatRole Role { get; set; }

    public Message(string content, DateTime dateTime, ChatRole role)
    {
        Content = content;
        ContentBlocks = new List<ContentBlock>();
        MsgDateTime = dateTime;
        Role = role;
    }

    public override string ToString()
    {
        return $"{MsgDateTime} {Content}";
    }
}

public class ContentBlock
{
    public string Text { get; set; }
    public ContentBlockType Type { get; set; }
    
    public ContentBlock(string text, ContentBlockType type)
    {
        Text = text;
        Type = type;
    }
}

public enum ContentBlockType
{
    Normal,
    Think
}

// 扩展方法，直接返回FontStyle
public static class ContentBlockTypeExtensions
{
    public static FontStyle ToFontStyle(this ContentBlockType blockType)
    {
        return blockType switch
        {
            ContentBlockType.Think => FontStyle.Italic,
            _ => FontStyle.Normal
        };
    }
}