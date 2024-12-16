// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDevGallery.Models;

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

                try
                {
                    list.Add(JsonSerializer.Deserialize(ref reader, AppDataSourceGenerationContext.Default.HardwareAccelerator));
                }
                catch (Exception)
                {
                }
            }
        }
        else if (reader.TokenType != JsonTokenType.Null)
        {
            try
            {
                list.Add(JsonSerializer.Deserialize(ref reader, AppDataSourceGenerationContext.Default.HardwareAccelerator));
            }
            catch (Exception)
            {
            }
        }

        if (list.Count == 0)
        {
            list.Add(HardwareAccelerator.CPU);
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<HardwareAccelerator> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            JsonSerializer.Serialize(writer, value.First(), AppDataSourceGenerationContext.Default.HardwareAccelerator);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, AppDataSourceGenerationContext.Default.HardwareAccelerator);
            }

            writer.WriteEndArray();
        }
    }
}