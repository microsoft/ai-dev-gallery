// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<float>? progress = null, IProgress<long>? progressBytesRead = null, CancellationToken cancellationToken = default)
    {
        // Get the http headers first to examine the content length
        using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            var contentLength = response.Content.Headers.ContentLength;

            using (var download = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                // Ignore progress reporting when no progress reporter was
                // passed or when the content length is unknown
                if (!contentLength.HasValue)
                {
                    await download.CopyToAsync(destination, cancellationToken);
                    return;
                }

                // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                var relativeProgress = new Progress<long>(totalBytes =>
                {
                    progress?.Report((float)totalBytes / contentLength.Value);
                    progressBytesRead?.Report(totalBytes);
                });

                // Use extension method to report progress while downloading
                await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
                progress?.Report(1);
            }
        }
    }

    public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException("Has to be readable", nameof(source));
        }

        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Has to be writable", nameof(destination));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;
        try
        {
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("Download cancelled");
        }
    }
}