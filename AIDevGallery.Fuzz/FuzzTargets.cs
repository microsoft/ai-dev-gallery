// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using System.Text;
using System.Web;

namespace AIDevGallery.Fuzz;

/// <summary>
/// Fuzz targets for AI Dev Gallery.
/// </summary>
public static class FuzzTargets
{
    /// <summary>
    /// Fuzz target for URI Protocol Handler (aidevgallery:// protocol).
    /// Tests the parsing of deep link URIs that can be triggered externally.
    /// </summary>
    public static void FuzzUriProtocolHandler(ReadOnlySpan<byte> input)
    {
        try
        {
            var inputString = Encoding.UTF8.GetString(input);

            // Simulate URI protocol activation parsing
            // This mirrors the logic in ActivationHelper.cs
            var uri = new Uri($"aidevgallery://{inputString}");

            // Test host parsing
            var host = uri.Host;

            // Test path parsing
            var localPath = uri.LocalPath;
            var pathComponents = localPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Test query string parsing (critical for addmodel route)
            if (host.Equals("addmodel", StringComparison.OrdinalIgnoreCase))
            {
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var modelPath = queryParams["modelPath"];

                // Validate path doesn't escape expected directory
                if (!string.IsNullOrEmpty(modelPath))
                {
                    // This is where path traversal vulnerabilities could occur
                    _ = Path.GetFullPath(modelPath);
                    _ = $"local-file:///{modelPath}";

                    // Simulate directory existence check
                    _ = Path.GetDirectoryName(modelPath);
                }
            }
            else if (host.Equals("models", StringComparison.OrdinalIgnoreCase) ||
                     host.Equals("apis", StringComparison.OrdinalIgnoreCase))
            {
                // Test item ID extraction
                if (pathComponents.Length > 0)
                {
                    _ = pathComponents[0];
                    _ = pathComponents.Length > 1 ? pathComponents[1] : null;
                }
            }
            else if (host.Equals("scenarios", StringComparison.OrdinalIgnoreCase))
            {
                // Test scenario ID extraction
                if (pathComponents.Length > 0)
                {
                    _ = pathComponents[0];
                }
            }
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or IOException or NotSupportedException)
        {
            // Expected for malformed URIs, invalid arguments, or invalid path operations
        }
    }

    /// <summary>
    /// Fuzz target for HuggingFace URL parser.
    /// Tests URL parsing and local path generation.
    /// </summary>
    public static void FuzzHuggingFaceUrl(ReadOnlySpan<byte> input)
    {
        try
        {
            var inputString = Encoding.UTF8.GetString(input);

            // Test HuggingFaceUrl constructor
            var hfUrl = new HuggingFaceUrl(inputString);

            // Test property access
            var org = hfUrl.Organization;
            var repo = hfUrl.Repo;
            var path = hfUrl.Path;
            var refValue = hfUrl.Ref;
            _ = hfUrl.IsFile;
            _ = hfUrl.FullUrl;

            // Critical: Test local path generation for path traversal
            var localPath = hfUrl.GetLocalPath(@"C:\ModelCache");

            // Verify no path traversal occurred
            if (!localPath.StartsWith(@"C:\ModelCache", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path traversal detected: {localPath}");
            }

            // Check for path components that shouldn't be in a safe path
            if (localPath.Contains(".."))
            {
                throw new InvalidOperationException($"Path contains '..': {localPath}");
            }

            // Test URL building methods
            HuggingFaceUrl.BuildRepoUrl(org, repo);
            HuggingFaceUrl.BuildTreeUrl(org, repo, refValue);

            if (!string.IsNullOrEmpty(path))
            {
                HuggingFaceUrl.BuildTreeUrl(org, repo, refValue, path);
                HuggingFaceUrl.BuildResolveUrl(org, repo, refValue, path);
                HuggingFaceUrl.BuildBlobUrl(org, repo, refValue, path);
            }

            HuggingFaceUrl.BuildApiUrl(org, repo, refValue);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            // Expected for invalid URLs or invalid path operations from GetLocalPath
        }
    }

    /// <summary>
    /// Fuzz target for GitHub URL parser.
    /// Tests URL parsing and local path generation.
    /// </summary>
    public static void FuzzGitHubUrl(ReadOnlySpan<byte> input)
    {
        try
        {
            var inputString = Encoding.UTF8.GetString(input);

            // Test GitHubUrl constructor
            var ghUrl = new GitHubUrl(inputString);

            // Test property access
            var org = ghUrl.Organization;
            var repo = ghUrl.Repo;
            var path = ghUrl.Path;
            var refValue = ghUrl.Ref;
            _ = ghUrl.IsFile;
            _ = ghUrl.FullUrl;

            // Critical: Test local path generation for path traversal
            var localPath = ghUrl.GetLocalPath(@"C:\ModelCache");

            // Verify no path traversal occurred
            if (!localPath.StartsWith(@"C:\ModelCache", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path traversal detected: {localPath}");
            }

            // Check for path components that shouldn't be in a safe path
            if (localPath.Contains(".."))
            {
                throw new InvalidOperationException($"Path contains '..': {localPath}");
            }

            // Test URL building methods
            GitHubUrl.BuildRepoUrl(org, repo);

            if (!string.IsNullOrEmpty(path))
            {
                GitHubUrl.BuildRawUrl(org, repo, refValue, path);
                GitHubUrl.BuildBlobUrl(org, repo, refValue, path);
            }

            GitHubUrl.BuildApiUrl(org, repo, refValue);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            // Expected for invalid URLs or invalid path operations from GetLocalPath
        }
    }
}