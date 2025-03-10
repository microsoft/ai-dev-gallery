// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal sealed class BackgroundHelpers
{
    public static byte[] GetForegroundMask(IEnumerable<float> output, int maskWidth, int maskHeight, int originalWidth, int originalHeight)
    {
        float[] tensorData = output.ToArray();
        byte[] mask = new byte[originalWidth * originalHeight * 4]; // RGBA format

        // Compute scaling factor (inverse of what was used in ResizeWithPadding)
        float scale = Math.Min((float)maskWidth / originalWidth, (float)maskHeight / originalHeight);

        // Compute padding applied during resizing
        int scaledWidth = (int)(originalWidth * scale);
        int scaledHeight = (int)(originalHeight * scale);
        int offsetX = (maskWidth - scaledWidth) / 2;
        int offsetY = (maskHeight - scaledHeight) / 2;

        Parallel.For(0, originalHeight, y =>
        {
            for (int x = 0; x < originalWidth; x++)
            {
                float scaledX = (float)x / originalWidth * scaledWidth + offsetX;
                float scaledY = (float)y / originalHeight * scaledHeight + offsetY;

                scaledX = Math.Clamp(scaledX, 0, maskWidth - 1);
                scaledY = Math.Clamp(scaledY, 0, maskHeight - 1);

                int x0 = (int)Math.Floor(scaledX);
                int x1 = Math.Min(x0 + 1, maskWidth - 1);
                int y0 = (int)Math.Floor(scaledY);
                int y1 = Math.Min(y0 + 1, maskHeight - 1);

                float xWeight = scaledX - x0;
                float yWeight = scaledY - y0;

                float fg00 = tensorData[y0 * maskWidth + x0 + maskWidth * maskHeight];
                float fg10 = tensorData[y0 * maskWidth + x1 + maskWidth * maskHeight];
                float fg01 = tensorData[y1 * maskWidth + x0 + maskWidth * maskHeight];
                float fg11 = tensorData[y1 * maskWidth + x1 + maskWidth * maskHeight];

                float fgProb = (fg00 * (1 - xWeight) + fg10 * xWeight) * (1 - yWeight) +
                               (fg01 * (1 - xWeight) + fg11 * xWeight) * yWeight;

                float bgProb = (tensorData[y0 * maskWidth + x0] * (1 - xWeight) + tensorData[y0 * maskWidth + x1] * xWeight) * (1 - yWeight) +
                               (tensorData[y1 * maskWidth + x0] * (1 - xWeight) + tensorData[y1 * maskWidth + x1] * xWeight) * yWeight;

                int index = (y * originalWidth + x) * 4;

                if (fgProb < bgProb)
                {
                    // Background (No blur)
                    mask[index] = 255;
                    mask[index + 1] = 255;
                    mask[index + 2] = 255;
                    mask[index + 3] = 255;
                }
                else
                {
                    mask[index] = 0;
                    mask[index + 1] = 0;
                    mask[index + 2] = 0;
                    mask[index + 3] = 0;
                }
            }
        });

        return mask;
    }
}