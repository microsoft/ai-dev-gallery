// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.SharedCode
{
    internal class BitmapFunctions
    {
        private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
        private static readonly float[] StdDev = [0.229f, 0.224f, 0.225f];

        public static Bitmap ResizeBitmap(Bitmap originalBitmap, int newWidth, int newHeight)
        {
            Bitmap resizedBitmap = new(newWidth, newHeight);
            using (Graphics graphics = Graphics.FromImage(resizedBitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
            }

            return resizedBitmap;
        }

        public static Bitmap ResizeWithPadding(Bitmap originalBitmap, int targetWidth, int targetHeight)
        {
            // Determine the scaling factor to fit the image within the target dimensions
            float scale = Math.Min((float)targetWidth / originalBitmap.Width, (float)targetHeight / originalBitmap.Height);

            // Calculate the new width and height based on the scaling factor
            int scaledWidth = (int)(originalBitmap.Width * scale);
            int scaledHeight = (int)(originalBitmap.Height * scale);

            // Center the image within the target dimensions
            int offsetX = (targetWidth - scaledWidth) / 2;
            int offsetY = (targetHeight - scaledHeight) / 2;

            // Create a new bitmap with the target dimensions and a white background for padding
            Bitmap paddedBitmap = new(targetWidth, targetHeight);
            using (Graphics graphics = Graphics.FromImage(paddedBitmap))
            {
                graphics.Clear(Color.White); // Set background color for padding to white
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Draw the scaled image onto the center of the new bitmap
                graphics.DrawImage(originalBitmap, offsetX, offsetY, scaledWidth, scaledHeight);
            }

            return paddedBitmap;
        }

        public static Tensor<float> PreprocessBitmapWithStdDev(Bitmap bitmap, Tensor<float> input)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(stride) * height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 3;
                    byte blue = rgbValues[index];
                    byte green = rgbValues[index + 1];
                    byte red = rgbValues[index + 2];

                    input[0, 0, y, x] = ((red / 255f) - Mean[0]) / StdDev[0];
                    input[0, 1, y, x] = ((green / 255f) - Mean[1]) / StdDev[1];
                    input[0, 2, y, x] = ((blue / 255f) - Mean[2]) / StdDev[2];
                }
            }

            bitmap.UnlockBits(bmpData);

            return input;
        }

        public static Tensor<float> PreprocessBitmapWithoutNormalization(Bitmap bitmap, Tensor<float> input)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(stride) * height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 3;
                    byte blue = rgbValues[index];
                    byte green = rgbValues[index + 1];
                    byte red = rgbValues[index + 2];

                    input[0, 0, y, x] = red / 255f;
                    input[0, 1, y, x] = green / 255f;
                    input[0, 2, y, x] = blue / 255f;
                }
            }

            bitmap.UnlockBits(bmpData);

            return input;
        }

        public static Tensor<float> PreprocessBitmapForYOLO(Bitmap bitmap, Tensor<float> input)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(stride) * height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 3;
                    byte blue = rgbValues[index];
                    byte green = rgbValues[index + 1];
                    byte red = rgbValues[index + 2];

                    input[0, y, x, 0] = red / 255f;
                    input[0, y, x, 1] = green / 255f;
                    input[0, y, x, 2] = blue / 255f;
                }
            }

            bitmap.UnlockBits(bmpData);

            return input;
        }

        public static DenseTensor<float> PreprocessBitmapForObjectDetection(Bitmap bitmap, int paddedHeight, int paddedWidth)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            DenseTensor<float> input = new([3, paddedHeight, paddedWidth]);

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(stride) * height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int y = paddedHeight - height; y < height; y++)
            {
                for (int x = paddedWidth - width; x < width; x++)
                {
                    int index = (y - (paddedHeight - height)) * stride + (x - (paddedWidth - width)) * 3;
                    byte blue = rgbValues[index];
                    byte green = rgbValues[index + 1];
                    byte red = rgbValues[index + 2];

                    input[0, y, x] = blue - Mean[0];
                    input[1, y, x] = green - Mean[1];
                    input[2, y, x] = red - Mean[2];
                }
            }

            bitmap.UnlockBits(bmpData);

            return input;
        }

        public static void DrawPredictions(Bitmap bitmap, List<Prediction> predictions)
        {
            using Graphics g = Graphics.FromImage(bitmap);
            int markerSize = (int)((bitmap.Width + bitmap.Height) * 0.02 / 2);
            int fontSize = (int)((bitmap.Width + bitmap.Height) * .02 / 2);
            fontSize = Math.Max(fontSize, 1);
            using Pen pen = new(Color.Red, markerSize / 10);
            using Brush brush = new SolidBrush(Color.White);
            using Font font = new("Arial", fontSize);
            foreach (var p in predictions)
            {
                if (p == null || p.Box == null)
                {
                    continue;
                }

                // Draw the box
                g.DrawLine(pen, p.Box.Xmin, p.Box.Ymin, p.Box.Xmax, p.Box.Ymin);
                g.DrawLine(pen, p.Box.Xmax, p.Box.Ymin, p.Box.Xmax, p.Box.Ymax);
                g.DrawLine(pen, p.Box.Xmax, p.Box.Ymax, p.Box.Xmin, p.Box.Ymax);
                g.DrawLine(pen, p.Box.Xmin, p.Box.Ymax, p.Box.Xmin, p.Box.Ymin);

                // Draw the label and confidence
                string labelText = $"{p.Label}, {p.Confidence:0.00}";
                g.DrawString(labelText, font, brush, new PointF(p.Box.Xmin, p.Box.Ymin));
            }
        }

        public static Bitmap CropAndScale(Bitmap paddedBitmap, int originalWidth, int originalHeight, int modelScalingFactor)
        {
            float scale = Math.Min(128f / originalWidth, 128f / originalHeight);

            // Calculate the dimensions of the cropped area
            int cropWidth = (int)(originalWidth * scale * modelScalingFactor);
            int cropHeight = (int)(originalHeight * scale * modelScalingFactor);

            // Calculate the offset to locate the padded content in the 512x512 image
            int offsetX = (paddedBitmap.Width - cropWidth) / 2;
            int offsetY = (paddedBitmap.Height - cropHeight) / 2;

            // Crop the region containing the actual content
            Rectangle cropArea = new(offsetX, offsetY, cropWidth, cropHeight);
            using Bitmap croppedBitmap = paddedBitmap.Clone(cropArea, paddedBitmap.PixelFormat);

            // Scale the cropped bitmap to {modelScalingFactor} times the original image dimensions
            int finalWidth = originalWidth * modelScalingFactor;
            int finalHeight = originalHeight * modelScalingFactor;
            Bitmap scaledBitmap = new(finalWidth, finalHeight);

            using (Graphics graphics = Graphics.FromImage(scaledBitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(croppedBitmap, 0, 0, finalWidth, finalHeight);
            }

            return scaledBitmap;
        }

        public static Bitmap TensorToBitmap(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> tensor)
        {
            var outputTensor = tensor[0].AsTensor<float>();
            int height = outputTensor.Dimensions[2];  // 512
            int width = outputTensor.Dimensions[3];   // 512

            // Create the bitmap
            Bitmap bitmap = new(width, height, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            IntPtr ptr = bmpData.Scan0;
            byte[] pixelData = new byte[Math.Abs(stride) * height];

            // Fill the pixel data
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + (x * 3);

                    // Extract RGB values from the tensor (assume [0,1] range)
                    float rVal = outputTensor[0, 0, y, x];  // Red
                    float gVal = outputTensor[0, 1, y, x];  // Green
                    float bVal = outputTensor[0, 2, y, x];  // Blue

                    // Scale to [0, 255] and clamp
                    byte r = (byte)Math.Clamp(rVal * 255, 0, 255);
                    byte g = (byte)Math.Clamp(gVal * 255, 0, 255);
                    byte b = (byte)Math.Clamp(bVal * 255, 0, 255);

                    // Store pixel values in BGR order
                    pixelData[index] = b;
                    pixelData[index + 1] = g;
                    pixelData[index + 2] = r;
                }
            }

            // Copy the pixel data to the bitmap
            Marshal.Copy(pixelData, 0, ptr, pixelData.Length);
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        public static BitmapImage ConvertBitmapToBitmapImageAsync(Bitmap bitmap)
        {
            using var stream = new InMemoryRandomAccessStream();

            // Save the bitmap to a stream
            bitmap.Save(stream.AsStream(), ImageFormat.Png);
            stream.Seek(0);

            // Create a BitmapImage from the stream
            BitmapImage bitmapImage = new();
            bitmapImage.SetSource(stream);

            return bitmapImage;
        }
    }
}