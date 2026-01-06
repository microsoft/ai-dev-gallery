// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
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
    public void SoftwareBitmap_Convert_CreatesNewCopy()
    {
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

        // Assert
        Assert.IsNotNull(convertedBitmap, "Converted bitmap should not be null");
        Assert.AreNotSame(originalBitmap, convertedBitmap, "Convert should create a new instance");
        Assert.AreEqual(originalBitmap.PixelWidth, convertedBitmap.PixelWidth, "Width should match");
        Assert.AreEqual(originalBitmap.PixelHeight, convertedBitmap.PixelHeight, "Height should match");
        Assert.AreEqual(BitmapPixelFormat.Bgra8, convertedBitmap.BitmapPixelFormat, "Format should be converted");

        // Clean up
        originalBitmap.Dispose();
        convertedBitmap.Dispose();
    }

    [TestMethod]
    public void SoftwareBitmap_CanDisposeOriginalAfterConvert()
    {
        // This test verifies that disposing the original bitmap after Convert
        // doesn't affect the converted bitmap (they are independent copies)

        // Arrange
        var originalBitmap = new SoftwareBitmap(
            BitmapPixelFormat.Rgba8,
            50,
            50,
            BitmapAlphaMode.Premultiplied);

        // Act
        var convertedBitmap = SoftwareBitmap.Convert(
            originalBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        // Dispose original
        originalBitmap.Dispose();

        // Assert - converted bitmap should still be usable
        Assert.AreEqual(50, convertedBitmap.PixelWidth);
        Assert.AreEqual(50, convertedBitmap.PixelHeight);
        Assert.AreEqual(BitmapPixelFormat.Bgra8, convertedBitmap.BitmapPixelFormat);

        // Clean up
        convertedBitmap.Dispose();
    }

    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public void SoftwareBitmap_AccessAfterDispose_ThrowsException()
    {
        // Arrange
        var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            100,
            100,
            BitmapAlphaMode.Premultiplied);

        // Act
        bitmap.Dispose();

        // Assert - accessing properties after dispose should throw
        _ = bitmap.PixelWidth; // This should throw ObjectDisposedException
    }

    [TestMethod]
    public void SoftwareBitmap_UsingPattern_DisposesCorrectly()
    {
        // This test verifies the using pattern works correctly
        SoftwareBitmap? bitmapReference = null;

        // Act
        using (var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            100,
            100,
            BitmapAlphaMode.Premultiplied))
        {
            bitmapReference = bitmap;
            Assert.AreEqual(100, bitmap.PixelWidth);
        }

        // Assert - bitmap should be disposed after using block
        try
        {
            _ = bitmapReference!.PixelWidth;
            Assert.Fail("Should have thrown ObjectDisposedException");
        }
        catch (ObjectDisposedException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task SimulateBackgroundRemoverPattern_VerifyLifecycle()
    {
        // This test simulates the pattern used in BackgroundRemover.xaml.cs
        // to verify that disposing outputBitmap after SetImageSource is safe

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
            // This is what happens inside SetImageSource:
            convertedBitmap = SoftwareBitmap.Convert(
                outputBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            // At this point in BackgroundRemover, we would:
            // await bitmapSource.SetBitmapAsync(convertedBitmap);
            // For testing, we just verify the converted bitmap is valid

            Assert.IsNotNull(convertedBitmap);
            Assert.AreEqual(200, convertedBitmap.PixelWidth);

            // Now simulate the using block exiting - dispose outputBitmap
            outputBitmap.Dispose();

            // Assert - convertedBitmap should still be valid because it's a copy
            Assert.AreEqual(200, convertedBitmap.PixelWidth);
            Assert.AreEqual(200, convertedBitmap.PixelHeight);

            await Task.CompletedTask; // Simulate async operation
        }
        finally
        {
            // Clean up
            convertedBitmap?.Dispose();
        }
    }

    [TestMethod]
    public void MultipleConversions_EachCreateIndependentCopies()
    {
        // Arrange
        var originalBitmap = new SoftwareBitmap(
            BitmapPixelFormat.Rgba8,
            100,
            100,
            BitmapAlphaMode.Premultiplied);

        // Act - Convert multiple times
        var converted1 = SoftwareBitmap.Convert(originalBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var converted2 = SoftwareBitmap.Convert(originalBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var converted3 = SoftwareBitmap.Convert(originalBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        // Assert - All should be different instances
        Assert.AreNotSame(converted1, converted2);
        Assert.AreNotSame(converted2, converted3);
        Assert.AreNotSame(converted1, converted3);

        // Dispose one shouldn't affect others
        converted1.Dispose();
        Assert.AreEqual(100, converted2.PixelWidth); // Should still work
        Assert.AreEqual(100, converted3.PixelWidth); // Should still work

        // Clean up
        originalBitmap.Dispose();
        converted2.Dispose();
        converted3.Dispose();
    }
}

/// <summary>
/// Performance tests for HttpClient usage patterns
/// Note: These are integration tests that make actual HTTP calls
/// Mark with [Ignore] if you want to skip them in regular test runs
/// </summary>
[TestClass]
public class HttpClientPerformanceTests
{
    private const string TestImageUrl = "https://via.placeholder.com/150";
    private const int WarmupIterations = 2;
    private const int TestIterations = 10;

    [TestMethod]
    [TestCategory("Performance")]
    public async Task HttpClient_NewInstanceEachTime_MeasurePerformance()
    {
        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(TestImageUrl);
        }

        // Measure
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(TestImageUrl);
            _ = await response.Content.ReadAsByteArrayAsync();
        }
        stopwatch.Stop();

        var avgTime = stopwatch.ElapsedMilliseconds / (double)TestIterations;
        Debug.WriteLine($"New HttpClient each time - Average: {avgTime:F2}ms, Total: {stopwatch.ElapsedMilliseconds}ms");

        // No assertion, just measuring
        Assert.IsTrue(stopwatch.ElapsedMilliseconds > 0);
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task HttpClient_SharedInstance_MeasurePerformance()
    {
        var sharedClient = new HttpClient();

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            using var response = await sharedClient.GetAsync(TestImageUrl);
        }

        // Measure
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < TestIterations; i++)
        {
            using var response = await sharedClient.GetAsync(TestImageUrl);
            _ = await response.Content.ReadAsByteArrayAsync();
        }
        stopwatch.Stop();

        var avgTime = stopwatch.ElapsedMilliseconds / (double)TestIterations;
        Debug.WriteLine($"Shared HttpClient - Average: {avgTime:F2}ms, Total: {stopwatch.ElapsedMilliseconds}ms");

        // No assertion, just measuring
        Assert.IsTrue(stopwatch.ElapsedMilliseconds > 0);

        sharedClient.Dispose();
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task HttpClient_ComparePerformance()
    {
        const int iterations = 5;
        var results = new Dictionary<string, long>();

        // Test 1: New instance each time
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(TestImageUrl);
            _ = await response.Content.ReadAsByteArrayAsync();
        }
        sw1.Stop();
        results["NewInstance"] = sw1.ElapsedMilliseconds;

        // Test 2: Shared instance
        var sharedClient = new HttpClient();
        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var response = await sharedClient.GetAsync(TestImageUrl);
            _ = await response.Content.ReadAsByteArrayAsync();
        }
        sw2.Stop();
        results["SharedInstance"] = sw2.ElapsedMilliseconds;
        sharedClient.Dispose();

        // Report results
        var improvement = ((results["NewInstance"] - results["SharedInstance"]) / (double)results["NewInstance"]) * 100;
        Debug.WriteLine($"\n=== HttpClient Performance Comparison ===");
        Debug.WriteLine($"New instance each time: {results["NewInstance"]}ms");
        Debug.WriteLine($"Shared instance: {results["SharedInstance"]}ms");
        Debug.WriteLine($"Performance improvement: {improvement:F1}%");
        Debug.WriteLine($"=========================================\n");

        // Assert that shared instance is at least not worse
        // (In reality, it should be significantly better)
        Assert.IsTrue(results["SharedInstance"] <= results["NewInstance"] * 1.2,
            $"Shared HttpClient should not be significantly slower. New: {results["NewInstance"]}ms, Shared: {results["SharedInstance"]}ms");
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Ignore] // Ignore by default as it takes time
    public async Task HttpClient_SocketExhaustion_Simulate()
    {
        // This test simulates the socket exhaustion problem
        // by creating many HttpClient instances rapidly

        var tasks = new List<Task>();
        var errorCount = 0;

        try
        {
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var client = new HttpClient();
                        using var response = await client.GetAsync(TestImageUrl);
                    }
                    catch (HttpRequestException)
                    {
                        System.Threading.Interlocked.Increment(ref errorCount);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Debug.WriteLine($"Errors encountered: {errorCount}/100");

            // We expect some errors due to socket exhaustion
            // This demonstrates the problem with creating new HttpClient instances
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception: {ex.Message}");
        }
    }
}
