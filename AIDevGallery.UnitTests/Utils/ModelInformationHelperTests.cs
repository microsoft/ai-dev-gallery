using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.UnitTests.Utils
{
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
    }
}
