// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock;

internal class DefaultSVGRenderer : ISVGRenderer
{
    public async Task<Image> SvgToImage(string svgString)
    {
        // SvgImageSource ownership is transferred to Image.Source, so it should not be disposed here
#pragma warning disable IDISP004 // Don't ignore created IDisposable
        SvgImageSource svgImageSource = new SvgImageSource();
#pragma warning restore IDISP004
        var image = new Image();

        // Create a MemoryStream object and write the SVG string to it
        using (var memoryStream = new MemoryStream())
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            await streamWriter.WriteAsync(svgString);
            await streamWriter.FlushAsync();

            // Rewind the MemoryStream
            memoryStream.Position = 0;

            // Load the SVG from the MemoryStream
            // AsRandomAccessStream() returns a wrapper around memoryStream that should not be disposed separately
            // The wrapper is valid as long as the underlying memoryStream is alive
            // The await ensures SetSourceAsync completes before memoryStream is disposed at the end of the using block
#pragma warning disable IDISP001 // Dispose created
            var randomAccessStream = memoryStream.AsRandomAccessStream();
#pragma warning restore IDISP001
            await svgImageSource.SetSourceAsync(randomAccessStream);
        }

        // Set the Source property of the Image control to the SvgImageSource object
        image.Source = svgImageSource;
        var size = Extensions.GetSvgSize(svgString);
        if (size.Width != 0)
        {
            image.Width = size.Width;
        }

        if (size.Height != 0)
        {
            image.Height = size.Height;
        }

        return image;
    }
}