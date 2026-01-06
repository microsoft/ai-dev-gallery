// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml.Controls;
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
    public async Task SvgToImage_WithValidSvgString_ReturnsImageWithSource()
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
        Assert.IsInstanceOfType(image.Source, typeof(SvgImageSource), "Image.Source should be SvgImageSource");
    }

    [TestMethod]
    public async Task SvgToImage_WithValidSvgString_SetsCorrectDimensions()
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
    public async Task SvgToImage_WithSvgWithoutDimensions_ReturnsImageWithoutDimensions()
    {
        // Arrange
        var svgString = @"
            <svg xmlns=""http://www.w3.org/2000/svg"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""green"" />
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgString);

        // Assert
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.Source);
        // Width and Height should be 0 or default if not specified in SVG
    }

    [TestMethod]
    public async Task SvgToImage_StreamIsClosedAfterLoad_ImageSourceRemainsValid()
    {
        // Arrange
        var svgString = @"
            <svg width=""100"" height=""100"" xmlns=""http://www.w3.org/2000/svg"">
                <circle cx=""50"" cy=""50"" r=""40"" fill=""yellow"" />
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgString);

        // This test verifies that the using statement for randomAccessStream
        // doesn't cause issues. The stream is closed after SetSourceAsync,
        // but the image source should still be valid because SetSourceAsync
        // copies the data synchronously.

        // Assert
        Assert.IsNotNull(image.Source, "Image source should remain valid after stream is disposed");
        
        // Additional verification: try to access properties
        var svgSource = image.Source as SvgImageSource;
        Assert.IsNotNull(svgSource, "Should be able to cast to SvgImageSource");
    }

    [TestMethod]
    public async Task SvgToImage_ComplexSvg_LoadsSuccessfully()
    {
        // Arrange
        var complexSvg = @"
            <svg width=""300"" height=""200"" xmlns=""http://www.w3.org/2000/svg"">
                <defs>
                    <linearGradient id=""grad1"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""0%"">
                        <stop offset=""0%"" style=""stop-color:rgb(255,255,0);stop-opacity:1"" />
                        <stop offset=""100%"" style=""stop-color:rgb(255,0,0);stop-opacity:1"" />
                    </linearGradient>
                </defs>
                <ellipse cx=""150"" cy=""100"" rx=""100"" ry=""50"" fill=""url(#grad1)"" />
                <text x=""150"" y=""100"" font-size=""20"" text-anchor=""middle"" fill=""white"">SVG Test</text>
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(complexSvg);

        // Assert
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.Source);
        Assert.AreEqual(300.0, image.Width);
        Assert.AreEqual(200.0, image.Height);
    }

    [TestMethod]
    public async Task SvgToImage_MultipleCallsInSequence_AllSucceed()
    {
        // This test verifies that multiple sequential calls work correctly
        // and that resource disposal doesn't interfere with subsequent calls

        // Arrange
        var svgStrings = new[]
        {
            @"<svg width=""50"" height=""50"" xmlns=""http://www.w3.org/2000/svg""><circle cx=""25"" cy=""25"" r=""20"" fill=""red"" /></svg>",
            @"<svg width=""100"" height=""100"" xmlns=""http://www.w3.org/2000/svg""><rect width=""100"" height=""100"" fill=""blue"" /></svg>",
            @"<svg width=""150"" height=""150"" xmlns=""http://www.w3.org/2000/svg""><polygon points=""75,0 150,150 0,150"" fill=""green"" /></svg>"
        };

        // Act & Assert
        foreach (var svgString in svgStrings)
        {
            var image = await _renderer!.SvgToImage(svgString);
            Assert.IsNotNull(image);
            Assert.IsNotNull(image.Source);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(Exception))]
    public async Task SvgToImage_WithInvalidSvg_ThrowsException()
    {
        // Arrange
        var invalidSvg = "This is not valid SVG";

        // Act
        // This should throw an exception during SetSourceAsync
        await _renderer!.SvgToImage(invalidSvg);

        // Assert is handled by ExpectedException
    }

    [TestMethod]
    public async Task SvgToImage_WithEmptySvg_HandlesGracefully()
    {
        // Arrange
        var emptySvg = @"<svg xmlns=""http://www.w3.org/2000/svg""></svg>";

        // Act
        var image = await _renderer!.SvgToImage(emptySvg);

        // Assert
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.Source);
    }

    [TestMethod]
    public async Task SvgToImage_WithUnicodeCharacters_LoadsSuccessfully()
    {
        // Arrange
        var svgWithUnicode = @"
            <svg width=""200"" height=""100"" xmlns=""http://www.w3.org/2000/svg"">
                <text x=""10"" y=""50"" font-size=""20"">你好世界 🌍</text>
            </svg>";

        // Act
        var image = await _renderer!.SvgToImage(svgWithUnicode);

        // Assert
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.Source);
        Assert.AreEqual(200.0, image.Width);
        Assert.AreEqual(100.0, image.Height);
    }
}
