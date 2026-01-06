// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests.Controls;

[TestClass]
public class DefaultSVGRendererTests
{
    private DefaultSVGRenderer? _renderer;

    [TestInitialize]
    public void TestInitialize()
    {
        _renderer = new DefaultSVGRenderer();
    }

    [TestMethod]
    public async Task SvgToImageWithValidSvgStringReturnsImageWithSource()
    {
        // Arrange
        var svgString = @"
            <svg width=""100"" height=""100"" xmlns=""http://www.w3.org/2000/svg"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""red"" />
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgString);

        // Assert
        Assert.IsNotNull(image, "Image should not be null");
        Assert.IsNotNull(image.Source, "Image.Source should not be null");
        Assert.IsInstanceOfType<SvgImageSource>(image.Source, "Image.Source should be SvgImageSource");
    }

    [TestMethod]
    public async Task SvgToImageWithValidSvgStringSetsCorrectDimensions()
    {
        // Arrange
        var svgString = @"
            <svg width=""200"" height=""150"" xmlns=""http://www.w3.org/2000/svg"">
                <rect width=""200"" height=""150"" fill=""blue"" />
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgString);

        // Assert
        Assert.AreEqual(200.0, image.Width, "Image width should match SVG width");
        Assert.AreEqual(150.0, image.Height, "Image height should match SVG height");
    }

    [TestMethod]
    public async Task SvgToImageStreamIsClosedAfterLoadImageSourceRemainsValid()
    {
        // *** CRITICAL TEST FOR DefaultSVGRenderer.cs ***
        // This test validates the stream lifecycle in DefaultSVGRenderer.SvgToImage:
        //
        // Pattern in DefaultSVGRenderer.cs:
        //   using (var randomAccessStream = await svgString.AsRandomAccessStream())
        //   {
        //       await svgSource.SetSourceAsync(randomAccessStream);
        //   }  // randomAccessStream is disposed here
        //
        // Key Question: Is it safe to dispose the stream after SetSourceAsync?
        // Answer: YES, because SetSourceAsync copies the data synchronously.
        //
        // This test verifies that the SvgImageSource remains valid even after
        // the stream is disposed, preventing image rendering failures.

        // Arrange
        var svgString = @"
            <svg width=""100"" height=""100"" xmlns=""http://www.w3.org/2000/svg"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""yellow"" />
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgString);

        // The using statement for randomAccessStream has already completed at this point.
        // The stream is closed, but the image source should still be valid because
        // SetSourceAsync copies the data synchronously during the call.

        // Assert - Image source should remain valid after stream is disposed
        Assert.IsNotNull(image.Source, "Image source should remain valid after stream is disposed");

        // Additional verification: try to access and cast the source
        var svgSource = image.Source as SvgImageSource;
        Assert.IsNotNull(svgSource, "Should be able to cast to SvgImageSource");

        // Verify dimensions were parsed correctly
        Assert.AreEqual(100.0, image.Width, "Width should be parsed from SVG");
        Assert.AreEqual(100.0, image.Height, "Height should be parsed from SVG");
    }

    [TestMethod]
    public async Task SvgToImageWithInvalidSvgThrowsException()
    {
        // Arrange
        var invalidSvg = "This is not valid SVG";

        // Act & Assert - should throw an exception during SetSourceAsync
        await Assert.ThrowsExactlyAsync<Exception>(async () =>
            await _renderer!.SvgToImage(invalidSvg));
    }

    [TestMethod]
    public async Task SvgToImageWithEmptySvgHandlesGracefully()
    {
        // Arrange
        var emptySvg = @"<svg xmlns=""http://www.w3.org/2000/svg""></svg>";

        // Act
        var image = await _renderer!.SvgToImage(emptySvg);

        // Assert
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.Source);
    }
}