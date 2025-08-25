// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;
using System;

namespace AIDevGallery.Converters
{
    public sealed class SplitLinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is null)
            {
                return Array.Empty<string>();
            }

            var text = value.ToString() ?? string.Empty;
            // Split on all common newline variants and preserve empty entries to keep blank lines
            var lines = text.Split(new[] { "\r\n", "\n\n", "\r" }, StringSplitOptions.None);
            return lines;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}


