using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDevGallery.UnitTests.Models
{
    [TestClass]
    public class SingleOrListOfHardwareAcceleratorConverterTests
    {
        private class TestClass
        {
            [JsonConverter(typeof(SingleOrListOfHardwareAcceleratorConverter))]
            public List<HardwareAccelerator> Accelerators { get; set; }
        }

        [TestMethod]
        public void Deserialize_SingleItem_ReturnsList()
        {
            var json = "{\"Accelerators\": \"CPU\"}";
            var obj = JsonSerializer.Deserialize<TestClass>(json);
            Assert.IsNotNull(obj);
            Assert.AreEqual(1, obj.Accelerators.Count);
            Assert.AreEqual(HardwareAccelerator.CPU, obj.Accelerators[0]);
        }

        [TestMethod]
        public void Deserialize_Array_ReturnsList()
        {
            var json = "{\"Accelerators\": [\"CPU\", \"GPU\"]}";
            var obj = JsonSerializer.Deserialize<TestClass>(json);
            Assert.IsNotNull(obj);
            Assert.AreEqual(2, obj.Accelerators.Count);
            Assert.IsTrue(obj.Accelerators.Contains(HardwareAccelerator.CPU));
            Assert.IsTrue(obj.Accelerators.Contains(HardwareAccelerator.GPU));
        }

        [TestMethod]
        public void Deserialize_Null_ReturnsDefaultCPU()
        {
             // The converter handles "TokenType != JsonTokenType.Null".
             // If it is null, it might skip the else if block.
             // Then list is empty.
             // Then "if (list.Count == 0) list.Add(HardwareAccelerator.CPU)".
             
             var json = "{\"Accelerators\": null}";
             var obj = JsonSerializer.Deserialize<TestClass>(json);
             Assert.IsNotNull(obj);
             Assert.AreEqual(1, obj.Accelerators.Count);
             Assert.AreEqual(HardwareAccelerator.CPU, obj.Accelerators[0]);
        }

        [TestMethod]
        public void Serialize_SingleItem_WritesSingleValue()
        {
            var obj = new TestClass { Accelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU } };
            var json = JsonSerializer.Serialize(obj);
            // Expect "CPU" not ["CPU"]
            Assert.IsTrue(json.Contains("\"Accelerators\":\"CPU\""));
        }

        [TestMethod]
        public void Serialize_MultipleItems_WritesArray()
        {
            var obj = new TestClass { Accelerators = new List<HardwareAccelerator> { HardwareAccelerator.CPU, HardwareAccelerator.GPU } };
            var json = JsonSerializer.Serialize(obj);
            Assert.IsTrue(json.Contains("\"Accelerators\":[\"CPU\",\"GPU\"]"));
        }
    }
}
