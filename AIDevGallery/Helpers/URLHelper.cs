// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace AIDevGallery.Helpers;

internal static class URLHelper
{
    private const string DocsBaseUrl = "https://learn.microsoft.com";
    private const string WcrDocsRelativePath = "/windows/ai/apis/";

    public static bool IsValidUrl(string url)
    {
        Uri uri;
        return Uri.TryCreate(url, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string FixWcrReadmeLink(string link)
    {
        if (link.StartsWith('/'))
        {
            return Path.Join(DocsBaseUrl, link);
        }
        else
        {
            return Path.Join(DocsBaseUrl, WcrDocsRelativePath, link.Replace(".md", string.Empty));
        }
    }
}