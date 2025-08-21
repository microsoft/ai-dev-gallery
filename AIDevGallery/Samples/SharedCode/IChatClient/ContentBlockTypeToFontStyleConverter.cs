// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace AIDevGallery.Samples.SharedCode;

// 内容块类型到字体样式的转换器
public class ContentBlockTypeToFontStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ContentBlockType blockType)
        {
            return blockType == ContentBlockType.Think ? FontStyle.Italic : FontStyle.Normal;
        }
        return FontStyle.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
} 