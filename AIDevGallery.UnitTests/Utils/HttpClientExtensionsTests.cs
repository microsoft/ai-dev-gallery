using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests.Utils
{
    [TestClass]
    public class HttpClientExtensionsTests
    {
        [TestMethod]
        public async Task DownloadAsync_DownloadsDataCorrectly()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3, 4, 5 };
            using var handler = new MockHttpMessageHandler(data);
            using var client = new HttpClient(handler);
            using var destination = new MemoryStream();

            // Act
            await client.DownloadAsync("http://example.com", destination);

            // Assert
            CollectionAssert.AreEqual(data, destination.ToArray());
        }

        [TestMethod]
        public async Task DownloadAsync_ReportsProgress()
        {
            // Arrange
            var data = new byte[100];
            new Random().NextBytes(data);
            using var handler = new MockHttpMessageHandler(data);
            using var client = new HttpClient(handler);
            using var destination = new MemoryStream();

            var tcs = new TaskCompletionSource<bool>();
            var progress = new Progress<float>(p =>
            {
                if (p >= 1.0f)
                {
                    tcs.TrySetResult(true);
                }
            });

            long totalBytes = 0;
            var progressBytes = new Progress<long>(bytes =>
            {
                totalBytes = bytes;
            });

            // Act
            await client.DownloadAsync("http://example.com", destination, progress, progressBytes);

            // Assert
            await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.IsTrue(tcs.Task.IsCompletedSuccessfully, "Progress did not reach 100%");
            Assert.AreEqual(data.Length, totalBytes);
            CollectionAssert.AreEqual(data, destination.ToArray());
        }

        [TestMethod]
        public async Task DownloadAsync_NoContentLength_DownloadsButSkipsProgress()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3 };
            using var handler = new MockHttpMessageHandler(data, contentLength: null);
            using var client = new HttpClient(handler);
            using var destination = new MemoryStream();

            bool progressReported = false;
            var progress = new Progress<float>(_ => progressReported = true);

            // Act
            await client.DownloadAsync("http://example.com", destination, progress);

            // Assert
            CollectionAssert.AreEqual(data, destination.ToArray());
            Assert.IsFalse(progressReported, "Progress should not be reported when Content-Length is missing");
        }

        [TestMethod]
        public async Task DownloadAsync_Cancellation_StopsDownload()
        {
            // Arrange
            var data = new byte[1024 * 100]; // 100KB
            using var handler = new MockHttpMessageHandler(data);
            using var client = new HttpClient(handler);
            using var destination = new MemoryStream();
            var cts = new CancellationTokenSource();

            // Act
            cts.Cancel(); // Cancel immediately
            await client.DownloadAsync("http://example.com", destination, cancellationToken: cts.Token);

            // Assert
            // Since we cancel immediately, it might not even start the request or read anything.
            // The implementation checks token in GetAsync and ReadAsStreamAsync.
            Assert.AreEqual(0, destination.Length);
        }

        [TestMethod]
        public async Task CopyToAsync_CopiesDataCorrectly()
        {
            // Arrange
            var sourceData = new byte[] { 1, 2, 3, 4, 5 };
            using var sourceStream = new MemoryStream(sourceData);
            using var destinationStream = new MemoryStream();

            // Act
            await HttpClientExtensions.CopyToAsync(sourceStream, destinationStream, bufferSize: 2);

            // Assert
            var destinationData = destinationStream.ToArray();
            CollectionAssert.AreEqual(sourceData, destinationData);
        }

        [TestMethod]
        public async Task CopyToAsync_ReportsProgress()
        {
            // Arrange
            var sourceData = new byte[10]; // 10 bytes
            new Random().NextBytes(sourceData);
            using var sourceStream = new MemoryStream(sourceData);
            using var destinationStream = new MemoryStream();
            
            // Use a TaskCompletionSource to wait for the final progress report
            var tcs = new TaskCompletionSource<bool>();
            var progress = new Progress<long>(bytes => 
            {
                if (bytes == 10)
                {
                    tcs.TrySetResult(true);
                }
            });

            // Act
            // Buffer size 2 means it should report progress roughly 5 times (2, 4, 6, 8, 10)
            await HttpClientExtensions.CopyToAsync(sourceStream, destinationStream, bufferSize: 2, progress: progress);

            // Assert
            // Wait for the final progress report with a timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            
            if (completedTask != tcs.Task)
            {
                Assert.Fail("Timed out waiting for progress report of 10 bytes");
            }
            
            CollectionAssert.AreEqual(sourceData, destinationStream.ToArray());
        }

        [TestMethod]
        public async Task CopyToAsync_CancellationStopsCopy()
        {
            // Arrange
            var sourceData = new byte[100];
            using var sourceStream = new MemoryStream(sourceData);
            using var destinationStream = new MemoryStream();
            var cts = new CancellationTokenSource();
            
            // Cancel immediately
            cts.Cancel();

            // Act
            await HttpClientExtensions.CopyToAsync(sourceStream, destinationStream, bufferSize: 10, cancellationToken: cts.Token);

            // Assert
            // Should be empty or partial, but definitely not full if cancelled early enough. 
            // However, since MemoryStream operations are synchronous, it might complete if not careful.
            // But CopyToAsync checks cancellation token in the loop.
            // With MemoryStream, ReadAsync might complete synchronously, but the loop checks token.
            
            // Since we cancelled before calling, it should return immediately or after first check.
            // The implementation checks `if (cancellationToken.IsCancellationRequested) return;` inside the loop.
            
            // Let's verify it didn't throw and maybe didn't copy everything if we could delay it, 
            // but with MemoryStream it's instant. 
            // However, if we pass a cancelled token, it should ideally stop.
            
            // Actually, the implementation:
            // while ((bytesRead = await source.ReadAsync...) != 0)
            // {
            //    if (cancellationToken.IsCancellationRequested) return;
            //    ...
            // }
            
            // If we cancel before, the first check inside the loop will catch it.
            // But ReadAsync also takes the token.
            
            Assert.AreEqual(0, destinationStream.Length);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CopyToAsync_NullSource_ThrowsArgumentNullException()
        {
            using var destinationStream = new MemoryStream();
            await HttpClientExtensions.CopyToAsync(null!, destinationStream, 10);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CopyToAsync_NullDestination_ThrowsArgumentNullException()
        {
            using var sourceStream = new MemoryStream();
            await HttpClientExtensions.CopyToAsync(sourceStream, null!, 10);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task CopyToAsync_UnreadableSource_ThrowsArgumentException()
        {
            using var sourceStream = new MemoryStream(new byte[1], false); // not writable, but readable? No, writable=false means read-only? 
            // MemoryStream(byte[], writable) -> if writable=false, canWrite=false. canRead is usually true.
            // We need a stream where CanRead is false.
            
            var unreadableStream = new UnreadableStream();
            using var destinationStream = new MemoryStream();
            
            await HttpClientExtensions.CopyToAsync(unreadableStream, destinationStream, 10);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task CopyToAsync_UnwritableDestination_ThrowsArgumentException()
        {
            using var sourceStream = new MemoryStream();
            using var destinationStream = new MemoryStream(new byte[10], false); // writable=false
            
            await HttpClientExtensions.CopyToAsync(sourceStream, destinationStream, 10);
        }

        private class UnreadableStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => 0;
            public override void SetLength(long value) { }
            public override void Write(byte[] buffer, int offset, int count) { }
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly byte[] _content;
            private readonly long? _contentLength;

            public MockHttpMessageHandler(byte[] content, long? contentLength = -1)
            {
                _content = content;
                _contentLength = contentLength == -1 ? content.Length : contentLength;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new ByteArrayContent(_content);
                if (_contentLength.HasValue)
                {
                    content.Headers.ContentLength = _contentLength;
                }
                else
                {
                    content.Headers.ContentLength = null;
                }

                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = content
                };
                
                // Force null if requested, though ByteArrayContent might fight back.
                // Actually, if we want to simulate no content length, we might need to use StreamContent and NOT set the header.
                if (!_contentLength.HasValue)
                {
                    response.Content.Headers.ContentLength = null;
                }

                return Task.FromResult(response);
            }
        }
    }
}
