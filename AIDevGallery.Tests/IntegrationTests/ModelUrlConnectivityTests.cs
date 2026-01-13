// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Integration tests that verify all model URLs in definition files are accessible.
// Scans all .model.json and .modelgroup.json files to extract and test URLs.
//
// Test coverage:
// - URL accessibility validation (HEAD requests)
// - Both individual file tests and batch testing across all definitions
// - Proper URL conversion (display URL → actual download URL)
// - GitHub: Tests raw.githubusercontent.com URLs for files, API for directories
// - HuggingFace: Tests resolve URLs for files, API for directories
//
// Note: These are expensive tests making real HTTP requests.
// Should be run separately from unit tests in CI/CD pipelines.
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

[TestClass]
public class ModelUrlConnectivityTests
{
    private const int RequestTimeoutSeconds = 30;
    private const int RateLimitDelayMs = 500;
    private const string ModelDefinitionsPath = "AIDevGallery/Samples/Definitions/Models";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
    };

    public static IEnumerable<object[]> GetModelGroupFiles()
    {
        var projectRoot = GetProjectRoot();
        var modelsDir = Path.Combine(projectRoot, ModelDefinitionsPath);

        if (!Directory.Exists(modelsDir))
        {
            yield break;
        }

        var modelFiles = Directory.GetFiles(modelsDir, "*.modelgroup.json")
            .Concat(Directory.GetFiles(modelsDir, "*.model.json"));

        foreach (var file in modelFiles)
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetModelGroupFiles), DynamicDataSourceType.Method)]
    public async Task ModelUrlsShouldBeAccessible(string fileName)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var filePath = Path.Combine(projectRoot, ModelDefinitionsPath, fileName);

        Assert.IsTrue(File.Exists(filePath), $"Model definition file not found: {filePath}");

        // Act
        var urls = ExtractUrlsFromModelFile(filePath);
        var failedUrls = await TestUrlsAsync(urls, fileName);

        // Assert
        if (failedUrls.Count > 0)
        {
            var errorMessage = BuildErrorMessage(failedUrls, fileName);
            Assert.Fail(errorMessage);
        }
    }

    [TestMethod]
    public async Task AllModelUrlsShouldBeAccessibleBatchTest()
    {
        // This test checks all URLs across all model files in one go
        // Useful for getting a complete report of all broken URLs
        var projectRoot = GetProjectRoot();
        var modelsDir = Path.Combine(projectRoot, ModelDefinitionsPath);
        var allFailedUrls = new List<(string File, string Url, string ModelName, int StatusCode, string Error)>();

        var modelFiles = Directory.GetFiles(modelsDir, "*.modelgroup.json")
            .Concat(Directory.GetFiles(modelsDir, "*.model.json"))
            .ToList();

        foreach (var filePath in modelFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var urls = ExtractUrlsFromModelFile(filePath);
            var failedUrls = await TestUrlsAsync(urls, fileName);
            allFailedUrls.AddRange(failedUrls.Select(f => (fileName, f.Url, f.ModelName, f.StatusCode, f.Error)));
        }

        if (allFailedUrls.Count > 0)
        {
            var errorMessage = BuildBatchErrorMessage(allFailedUrls);
            Assert.Fail(errorMessage);
        }
    }

    private static List<(string Url, string ModelName)> ExtractUrlsFromModelFile(string filePath)
    {
        var urls = new List<(string Url, string ModelName)>();
        var jsonContent = File.ReadAllText(filePath);

        using var doc = JsonDocument.Parse(jsonContent);
        ExtractUrlsRecursively(doc.RootElement, urls);

        return urls;
    }

    /// <summary>
    /// Recursively extracts URLs from JSON structure.
    /// Supports both .model.json and .modelgroup.json formats.
    /// </summary>
    private static void ExtractUrlsRecursively(JsonElement element, List<(string Url, string ModelName)> urls)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Check if this element has a URL property
        if (element.TryGetProperty("Url", out var urlElement))
        {
            var url = urlElement.GetString();
            if (!string.IsNullOrEmpty(url))
            {
                var name = element.TryGetProperty("Name", out var nameElement)
                    ? nameElement.GetString() ?? "Unknown"
                    : "Unknown";
                urls.Add((url, name));
            }
        }

        // Recursively process all child objects
        foreach (var property in element.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
        {
            ExtractUrlsRecursively(property.Value, urls);
        }
    }

    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Navigate up until we find the solution file
        while (!string.IsNullOrEmpty(currentDir))
        {
            if (File.Exists(Path.Combine(currentDir, "AIDevGallery.sln")))
            {
                return currentDir;
            }

            var parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        throw new InvalidOperationException("Could not find project root (AIDevGallery.sln)");
    }

    /// <summary>
    /// Converts a model URL to the actual download URL that will be tested.
    /// Mirrors the business code's actual download flow:
    /// - For FILES: tests the final download URL (raw/resolve URL)
    /// - For DIRECTORIES: tests the API endpoint that fetches file list
    ///
    /// Business code flow:
    /// 1. GitHub: API returns DownloadUrl (raw.githubusercontent.com) → test raw URL for files
    /// 2. HuggingFace: Code builds resolve URL → test resolve URL for files
    /// 3. Both: API endpoint returns file list → test API URL for directories
    /// </summary>
    private static (string Url, bool IsDirectory) ConvertToActualDownloadUrl(string url)
    {
        try
        {
            if (url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
            {
                var ghUrl = new GitHubUrl(url);
                var isFile = ghUrl.IsFile && !string.IsNullOrEmpty(ghUrl.Path);

                // GitHub file: test raw URL (this is what gets downloaded - from API's DownloadUrl field)
                // GitHub directory: test API endpoint (fetches file list)
                var convertedUrl = isFile
                    ? GitHubUrl.BuildRawUrl(ghUrl.Organization, ghUrl.Repo, ghUrl.Ref, ghUrl.Path)
                    : GitHubUrl.BuildApiUrl(ghUrl.Organization, ghUrl.Repo, ghUrl.Ref, ghUrl.Path);

                return (convertedUrl, !isFile);
            }
            else if (url.Contains("huggingface.co"))
            {
                var hfUrl = new HuggingFaceUrl(url);

                if (hfUrl.IsFile && !string.IsNullOrEmpty(hfUrl.Path))
                {
                    // HuggingFace file: test resolve URL (business code builds this for download)
                    return (HuggingFaceUrl.BuildResolveUrl(hfUrl.Organization, hfUrl.Repo, hfUrl.Ref, hfUrl.Path), false);
                }
                else
                {
                    // HuggingFace directory: test API endpoint (fetches file list)
                    // If the URL points to a file, API is called on parent directory
                    string? path = hfUrl.Path;
                    if (hfUrl.IsFile && hfUrl.Path != null)
                    {
                        var filePath = hfUrl.Path.Split('/');
                        path = filePath.Length > 1
                            ? string.Join("/", filePath.Take(filePath.Length - 1))
                            : null;
                    }

                    return (HuggingFaceUrl.BuildApiUrl(hfUrl.Organization, hfUrl.Repo, hfUrl.Ref, path), true);
                }
            }

            return (url, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse URL '{url}': {ex.Message}");
            return (url, false);
        }
    }

    /// <summary>
    /// Tests a list of URLs and returns failed ones.
    /// </summary>
    private static async Task<List<(string Url, string ModelName, int StatusCode, string Error)>> TestUrlsAsync(
        List<(string Url, string ModelName)> urls,
        string fileName)
    {
        var failedUrls = new List<(string Url, string ModelName, int StatusCode, string Error)>();

        foreach (var (url, modelName) in urls)
        {
            var (actualDownloadUrl, isDirectory) = ConvertToActualDownloadUrl(url);

            try
            {
                Console.WriteLine($"Testing: {modelName} ({fileName})");
                Console.WriteLine($"  Model URL: {url}");
                Console.WriteLine($"  Actual Download URL: {actualDownloadUrl}{(isDirectory ? " (directory - will fetch file list)" : string.Empty)}");

                var (success, statusCode, errorMessage) = await TestSingleUrlAsync(actualDownloadUrl);

                if (!success)
                {
                    Console.WriteLine($"Failed: {statusCode} - {errorMessage}");
                    failedUrls.Add((url, modelName, statusCode, $"Download URL: {actualDownloadUrl}\n  Error: {errorMessage}"));
                }
                else
                {
                    Console.WriteLine($"Success: {statusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                failedUrls.Add((url, modelName, 0, $"Download URL: {actualDownloadUrl}\n  Error: {ex.Message}"));
            }

            // Rate limiting - be nice to servers
            await Task.Delay(RateLimitDelayMs);
        }

        return failedUrls;
    }

    /// <summary>
    /// Tests a single URL using HEAD request.
    /// </summary>
    private static async Task<(bool Success, int StatusCode, string ErrorMessage)> TestSingleUrlAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            request.Headers.Add("User-Agent", "AIDevGallery");
            var response = await HttpClient.SendAsync(request);
            var statusCode = (int)response.StatusCode;

            return (response.IsSuccessStatusCode, statusCode, response.ReasonPhrase ?? "Unknown error");
        }
        catch (HttpRequestException ex)
        {
            return (false, 0, $"HTTP Exception: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, 0, "Request timeout");
        }
    }

    /// <summary>
    /// Builds an error message for a single file's failed URLs.
    /// </summary>
    private static string BuildErrorMessage(
        List<(string Url, string ModelName, int StatusCode, string Error)> failedUrls,
        string fileName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Found {failedUrls.Count} inaccessible URL(s) in {fileName}:");
        sb.AppendLine();

        AppendFailedUrlsToMessage(sb, failedUrls.Select(f => (string.Empty, f.Url, f.ModelName, f.StatusCode, f.Error)));

        return sb.ToString();
    }

    /// <summary>
    /// Builds an error message for all failed URLs across multiple files.
    /// </summary>
    private static string BuildBatchErrorMessage(
        List<(string File, string Url, string ModelName, int StatusCode, string Error)> allFailedUrls)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Found {allFailedUrls.Count} inaccessible URL(s) across all model files:");
        sb.AppendLine();

        foreach (var group in allFailedUrls.GroupBy(x => x.File))
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"File: {group.Key}");
            AppendFailedUrlsToMessage(sb, group);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends failed URL details to the error message.
    /// </summary>
    private static void AppendFailedUrlsToMessage(
        System.Text.StringBuilder sb,
        IEnumerable<(string File, string Url, string ModelName, int StatusCode, string Error)> failedUrls)
    {
        foreach (var (_, url, modelName, statusCode, error) in failedUrls)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Model: {modelName}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Model URL: {url}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  {error}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Status Code: {statusCode}");
            sb.AppendLine();
        }
    }
}