using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode
{
    internal record class TextDataItem
    {
        public string Id { get; set; }
        public string Value { get; set; }

    }

    internal record class ImageDataItem
    {
        public string Id { get; set; }
        public string ImageSource { get; set; }
    }
}
