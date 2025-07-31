using Newtonsoft.Json;
using UnityEngine;
using System;

namespace LDraw.Runtime
{
public class Vector3Converter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float x = 0, y = 0, z = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                string propName = (string)reader.Value;
                reader.Read();
                switch (propName)
                {
                    case "x": x = Convert.ToSingle(reader.Value); break;
                    case "y": y = Convert.ToSingle(reader.Value); break;
                    case "z": z = Convert.ToSingle(reader.Value); break;
                }
            }
            else if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }
        }
        return new Vector3(x, y, z);
    }
}

public class QuaternionConverter : JsonConverter<Quaternion>
{
    public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WritePropertyName("w");
        writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float x = 0, y = 0, z = 0, w = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                string propName = (string)reader.Value;
                reader.Read();
                switch (propName)
                {
                    case "x": x = Convert.ToSingle(reader.Value); break;
                    case "y": y = Convert.ToSingle(reader.Value); break;
                    case "z": z = Convert.ToSingle(reader.Value); break;
                    case "w": w = Convert.ToSingle(reader.Value); break;
                }
            }
            else if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }
        }
        return new Quaternion(x, y, z, w);
    }
}

public class ColorConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("r");
        writer.WriteValue(value.r);
        writer.WritePropertyName("g");
        writer.WriteValue(value.g);
        writer.WritePropertyName("b");
        writer.WriteValue(value.b);
        // writer.WritePropertyName("a");
        // writer.WriteValue(value.a);
        writer.WriteEndObject();
    }

    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float r = 0, g = 0, b = 0; //, a = 1f;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                string propName = (string)reader.Value;
                reader.Read();
                switch (propName)
                {
                    case "r": r = Convert.ToSingle(reader.Value); break;
                    case "g": g = Convert.ToSingle(reader.Value); break;
                    case "b": b = Convert.ToSingle(reader.Value); break;
                    // case "a": a = Convert.ToSingle(reader.Value); break;
                }
            }
            else if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }
        }

        return new Color(r, g, b);//, a);
    }
}

public class NullableVector3Converter : JsonConverter<Vector3?>
{
    public override void WriteJson(JsonWriter writer, Vector3? value, JsonSerializer serializer)
    {
        if (value.HasValue)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.Value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.Value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.Value.z);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override Vector3? ReadJson(JsonReader reader, Type objectType, Vector3? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        float x = 0, y = 0, z = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                string propName = (string)reader.Value;
                if (!reader.Read()) break;

                switch (propName)
                {
                    case "x": x = Convert.ToSingle(reader.Value); break;
                    case "y": y = Convert.ToSingle(reader.Value); break;
                    case "z": z = Convert.ToSingle(reader.Value); break;
                }
            }
            else if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }
        }

        return new Vector3(x, y, z);
    }
}

}
