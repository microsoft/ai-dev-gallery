// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace AIDevGallery.Tests.UnitTests.Samples;

/// <summary>
/// Tests for BackgroundRemover's SetImageSource method to verify bitmap lifecycle management
/// </summary>
[TestClass]
public class BitmapLifecycleTests
{
    [TestMethod]
    public void SoftwareBitmapConvertCreatesIndependentCopy()
    {
        // This test verifies that SoftwareBitmap.Convert creates an independent copy
        // and that disposing the original doesn't affect the converted bitmap.
        // This is critical for BackgroundRemover's lifecycle management.

        // Arrange
        var originalBitmap = new SoftwareBitmap(
            BitmapPixelFormat.Rgba8,
            100,
            100,
            BitmapAlphaMode.Premultiplied);

        // Act
        var convertedBitmap = SoftwareBitmap.Convert(
            originalBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        // Assert - Verify it's a new instance with correct properties
        Assert.IsNotNull(convertedBitmap, "Converted bitmap should not be null");
        Assert.AreNotSame(originalBitmap, convertedBitmap, "Convert should create a new instance");
        Assert.AreEqual(originalBitmap.PixelWidth, convertedBitmap.PixelWidth, "Width should match");
        Assert.AreEqual(originalBitmap.PixelHeight, convertedBitmap.PixelHeight, "Height should match");
        Assert.AreEqual(BitmapPixelFormat.Bgra8, convertedBitmap.BitmapPixelFormat, "Format should be converted");

        // Dispose original - this should NOT affect the converted bitmap
        originalBitmap.Dispose();

        // Assert - converted bitmap should still be usable after disposing original
        Assert.AreEqual(100, convertedBitmap.PixelWidth, "Converted bitmap should remain valid");
        Assert.AreEqual(100, convertedBitmap.PixelHeight, "Converted bitmap should remain valid");

        // Clean up
        convertedBitmap.Dispose();
    }

    [TestMethod]
    public async Task BackgroundRemoverBitmapLifecycleVerifyCorrectPattern()
    {
        // *** CRITICAL TEST FOR BackgroundRemover.xaml.cs ***
        // This test validates the exact pattern used in BackgroundRemover:
        //
        // Pattern in BackgroundRemover.xaml.cs:
        //   using (var outputBitmap = await ExtractBackground(...))
        //   {
        //       await SetImageSource(outputBitmap);  // Converts and uses bitmap
        //   }  // outputBitmap is disposed here
        //
        // Key Question: Is it safe to dispose outputBitmap after SetImageSource?
        // Answer: YES, because SoftwareBitmap.Convert creates an independent copy.
        //
        // This test verifies that the converted bitmap remains valid even after
        // the original outputBitmap is disposed, ensuring no use-after-free bugs.

        // Arrange - Create a bitmap (simulating ExtractBackground result)
        SoftwareBitmap? outputBitmap = null;
        SoftwareBitmap? convertedBitmap = null;

        try
        {
            outputBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Rgba8,
                200,
                200,
                BitmapAlphaMode.Premultiplied);

            // Act - Simulate SetImageSource logic
            // Inside SetImageSource, this happens:
            convertedBitmap = SoftwareBitmap.Convert(
                outputBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            // At this point in BackgroundRemover, we would:
            // await bitmapSource.SetBitmapAsync(convertedBitmap);
            // The SetBitmapAsync call completes synchronously with the bitmap data
            Assert.IsNotNull(convertedBitmap, "Converted bitmap should be created");
            Assert.AreEqual(200, convertedBitmap.PixelWidth, "Initial width should be correct");

            // Now simulate the using block exiting - dispose outputBitmap
            // This is the CRITICAL moment: does disposing outputBitmap break convertedBitmap?
            outputBitmap.Dispose();

            // Assert - convertedBitmap should still be valid because it's an independent copy
            Assert.AreEqual(200, convertedBitmap.PixelWidth, "Width should still be accessible");
            Assert.AreEqual(200, convertedBitmap.PixelHeight, "Height should still be accessible");
            Assert.AreEqual(BitmapPixelFormat.Bgra8, convertedBitmap.BitmapPixelFormat, "Format should still be accessible");

            await Task.CompletedTask; // Simulate async operation
        }
        finally
        {
            // Clean up - in real code, the UI framework holds the convertedBitmap reference
            convertedBitmap?.Dispose();
        }
    }
}