// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class LoggingHttpMessageHandler : DelegatingHandler
{
    public LoggingHttpMessageHandler() : base(new HttpClientHandler())
    {
    }

    public LoggingHttpMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[HTTP] Request: {request.Method} {request.RequestUri}");
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            if (content.Length <= 500)
            {
                Debug.WriteLine($"[HTTP] Request body: {content}");
            }
            else
            {
                Debug.WriteLine($"[HTTP] Request body: {content.Substring(0, 500)}... ({content.Length} chars total)");
            }
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        
        try
        {
            response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            
            Debug.WriteLine($"[HTTP] Response: {(int)response.StatusCode} {response.StatusCode} (took {stopwatch.ElapsedMilliseconds}ms)");
            Debug.WriteLine($"[HTTP] Response Headers:");
            foreach (var header in response.Headers)
            {
                Debug.WriteLine($"[HTTP]   {header.Key}: {string.Join(", ", header.Value)}");
            }
            foreach (var header in response.Content.Headers)
            {
                Debug.WriteLine($"[HTTP]   {header.Key}: {string.Join(", ", header.Value)}");
            }
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Debug.WriteLine($"[HTTP] ERROR after {stopwatch.ElapsedMilliseconds}ms: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
