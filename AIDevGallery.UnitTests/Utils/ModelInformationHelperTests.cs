// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests.Utils;

[TestClass]
public class ModelInformationHelperTests
{
    [TestMethod]
    public void FilterFiles_NoFilters_ReturnsAllFiles()
    {
        var files = new List<ModelFileDetails>
        {
            new ModelFileDetails { Path = "model.onnx" },
            new ModelFileDetails { Path = "README.md" }
        };

        var result = ModelInformationHelper.FilterFiles(files, null);
        Assert.AreEqual(2, result.Count);

        result = ModelInformationHelper.FilterFiles(files, new List<string>());
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void FilterFiles_WithFilters_ReturnsMatchingFiles()
    {
        var files = new List<ModelFileDetails>
        {
            new ModelFileDetails { Path = "model.onnx" },
            new ModelFileDetails { Path = "README.md" },
            new ModelFileDetails { Path = "config.json" }
        };

        var filters = new List<string> { ".onnx", ".json" };
        var result = ModelInformationHelper.FilterFiles(files, filters);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(f => f.Path == "model.onnx"));
        Assert.IsTrue(result.Any(f => f.Path == "config.json"));
        Assert.IsFalse(result.Any(f => f.Path == "README.md"));
    }

    [TestMethod]
    public void FilterFiles_CaseInsensitive_ReturnsMatchingFiles()
    {
        var files = new List<ModelFileDetails>
        {
            new ModelFileDetails { Path = "model.ONNX" }
        };

        var filters = new List<string> { ".onnx" };
        var result = ModelInformationHelper.FilterFiles(files, filters);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task GetDownloadFilesFromGitHub_ReturnsFiles()
    {
        var url = new GitHubUrl("https://github.com/org/repo/tree/main/path");
        var mockResponse = new List<object>
        {
            new { name = "file1.txt", path = "path/file1.txt", size = 100, download_url = "http://download/file1.txt", type = "file" },
            new { name = "file2.txt", path = "path/file2.txt", size = 200, download_url = "http://download/file2.txt", type = "file" }
        };
        var json = JsonSerializer.Serialize(mockResponse);

        using var handler = new MockHttpMessageHandler(json);
        var result = await ModelInformationHelper.GetDownloadFilesFromGitHub(url, handler);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("file1.txt", result[0].Name);
        Assert.AreEqual("path/file1.txt", result[0].Path);
        Assert.AreEqual(100, result[0].Size);
        Assert.AreEqual("http://download/file1.txt", result[0].DownloadUrl);
    }

    [TestMethod]
    public async Task GetDownloadFilesFromGitHub_SingleFileResponse_ReturnsFile()
    {
        var url = new GitHubUrl("https://github.com/org/repo/tree/main/path");
        var mockResponse = new { name = "file1.txt", path = "path/file1.txt", size = 100, download_url = "http://download/file1.txt", type = "file" };
        var json = JsonSerializer.Serialize(mockResponse);

        using var handler = new MockHttpMessageHandler(json);
        var result = await ModelInformationHelper.GetDownloadFilesFromGitHub(url, handler);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("file1.txt", result[0].Name);
    }

    [TestMethod]
    public async Task GetDownloadFilesFromHuggingFace_ReturnsFiles()
    {
        var url = new HuggingFaceUrl("https://huggingface.co/org/repo");
        var mockResponse = new List<object>
        {
            new { type = "file", size = 100L, path = "model.onnx" },
            new { type = "file", size = 200L, path = "config.json" }
        };
        var json = JsonSerializer.Serialize(mockResponse);

        using var handler = new MockHttpMessageHandler(json);
        var result = await ModelInformationHelper.GetDownloadFilesFromHuggingFace(url, handler);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("model.onnx", result[0].Path);
        Assert.AreEqual(100, result[0].Size);
        var downloadUrl = result[0].DownloadUrl;
        Assert.IsNotNull(downloadUrl);
        Assert.IsTrue(downloadUrl.Contains("resolve/main/model.onnx"));
    }

    [TestMethod]
    public async Task GetDownloadFilesFromHuggingFace_HandlesDirectories()
    {
        var url = new HuggingFaceUrl("https://huggingface.co/org/repo");

        // Initial response has a directory
        var rootResponse = new List<object>
        {
            new { type = "directory", path = "subfolder" },
            new { type = "file", size = 100L, path = "root.txt" }
        };

        // Subfolder response
        var subfolderResponse = new List<object>
        {
            new { type = "file", size = 200L, path = "subfolder/file.txt" }
        };

        using var handler = new MockHttpMessageHandler((request) =>
        {
            if (request.RequestUri?.ToString().EndsWith("subfolder") == true)
            {
                return JsonSerializer.Serialize(subfolderResponse);
            }

            return JsonSerializer.Serialize(rootResponse);
        });

        var result = await ModelInformationHelper.GetDownloadFilesFromHuggingFace(url, handler);

        Assert.AreEqual(2, result.Count);
        Assert.IsNotNull(result.FirstOrDefault(f => f.Path == "root.txt"));
        Assert.IsTrue(result.Any(f => f.Path == "root.txt"));
        Assert.IsNotNull(result.FirstOrDefault(f => f.Path == "subfolder/file.txt"));
        Assert.IsTrue(result.Any(f => f.Path == "subfolder/file.txt"));
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responseProvider;

        public MockHttpMessageHandler(string response)
        {
            _responseProvider = _ => response;
        }

        public MockHttpMessageHandler(Func<HttpRequestMessage, string> responseProvider)
        {
            _responseProvider = responseProvider;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _responseProvider(request);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
            return Task.FromResult(response);
        }
    }
}