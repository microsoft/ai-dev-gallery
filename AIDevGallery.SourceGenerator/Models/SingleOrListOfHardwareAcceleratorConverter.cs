// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDevGallery.SourceGenerator.Models;

internal class SingleOrListOfHardwareAcceleratorConverter : JsonConverter<List<HardwareAccelerator>>
{
    public override List<HardwareAccelerator> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<HardwareAccelerator>();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                list.Add(JsonSerializer.Deserialize(ref reader, SourceGenerationContext.Default.HardwareAccelerator));
            }
        }
        else if (reader.TokenType != JsonTokenType.Null)
        {
            var singleValue = JsonSerializer.Deserialize(ref reader, SourceGenerationContext.Default.HardwareAccelerator);
            list.Add(singleValue);
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<HardwareAccelerator> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            JsonSerializer.Serialize(writer, value.First(), SourceGenerationContext.Default.HardwareAccelerator);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, SourceGenerationContext.Default.HardwareAccelerator);
            }

            writer.WriteEndArray();
        }
    }
}