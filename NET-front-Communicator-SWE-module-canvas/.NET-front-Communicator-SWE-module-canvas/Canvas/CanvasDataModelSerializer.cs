using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CanvasDataModel;

public static class CanvasDataModelSerializer
{
    // --- NEW: Options for automatic serialization of NetworkMessage ---
    private static readonly JsonSerializerOptions _messageOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() } // Converts enums (MessageType) to strings
    };

    /// <summary>
    /// Serializes a NetworkMessage using System.Text.Json.
    /// </summary>
    public static string SerializeMessage(NetworkMessage message)
    {
        return JsonSerializer.Serialize(message, _messageOptions);
    }

    /// <summary>
    /// Deserializes a NetworkMessage using System.Text.Json.
    /// </summary>
    public static NetworkMessage? DeserializeMessage(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NetworkMessage>(json, _messageOptions);
        }
        catch
        {
            return null;
        }
    }
    // --- END NEW ---

    public static string SerializeShapeManual(IShape shape)
    {
        // ... (implementation unchanged) ...
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("ShapeId", shape.ShapeId);
                writer.WriteString("Type", shape.Type.ToString());
                writer.WritePropertyName("Points");
                writer.WriteStartArray();
                foreach (var point in shape.Points)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("X", point.X);
                    writer.WriteNumber("Y", point.Y);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteString("Color", $"#{shape.Color.ToArgb():X8}");
                writer.WriteNumber("Thickness", shape.Thickness);
                writer.WriteString("CreatedBy", shape.CreatedBy);
                writer.WriteString("LastModifiedBy", shape.LastModifiedBy);
                writer.WriteBoolean("IsDeleted", shape.IsDeleted);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static IShape? DeserializeShapeManual(string json)
    {
        // ... (implementation unchanged) ...
        if (string.IsNullOrEmpty(json)) { return null; }
        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null) { return null; }
        string? shapeId = doc["ShapeId"]?.GetValue<string>();
        string? typeName = doc["Type"]?.GetValue<string>();
        string? colorHex = doc["Color"]?.GetValue<string>();
        double thickness = doc["Thickness"]?.GetValue<double>() ?? 1.0;
        string? createdBy = doc["CreatedBy"]?.GetValue<string>();
        string? lastModifiedBy = doc["LastModifiedBy"]?.GetValue<string>();
        bool isDeleted = doc["IsDeleted"]?.GetValue<bool>() ?? false;
        Color color = Color.Black;
        if (!string.IsNullOrEmpty(colorHex))
        {
            color = Color.FromArgb(Convert.ToInt32(colorHex.Substring(1), 16));
        }
        List<Point> points = new List<Point>();
        JsonArray? pointsArray = doc["Points"]?.AsArray();
        if (pointsArray != null)
        {
            foreach (var pointNode in pointsArray)
            {
                if (pointNode != null)
                {
                    int x = pointNode["X"]?.GetValue<int>() ?? 0;
                    int y = pointNode["Y"]?.GetValue<int>() ?? 0;
                    points.Add(new Point(x, y));
                }
            }
        }
        if (Enum.TryParse<ShapeType>(typeName, out ShapeType shapeType))
        {
            switch (shapeType)
            {
                case ShapeType.FREEHAND:
                    return new FreeHand(shapeId!, points, color, thickness, createdBy!, lastModifiedBy!, isDeleted);
                case ShapeType.RECTANGLE:
                    return new RectangleShape(shapeId!, points, color, thickness, createdBy!, lastModifiedBy!, isDeleted);
                case ShapeType.TRIANGLE:
                    return new TriangleShape(shapeId!, points, color, thickness, createdBy!, lastModifiedBy!, isDeleted);
                case ShapeType.LINE:
                    return new StraightLine(shapeId!, points, color, thickness, createdBy!, lastModifiedBy!, isDeleted);
                case ShapeType.ELLIPSE:
                    return new EllipseShape(shapeId!, points, color, thickness, createdBy!, lastModifiedBy!, isDeleted);
            }
        }
        return null;
    }

    public static string SerializeActionManual(CanvasAction action)
    {
        // ... (implementation unchanged, ActionId fix included) ...
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("ActionId", action.ActionId);
                writer.WriteString("ActionType", action.ActionType.ToString());
                if (action.PrevShape != null)
                {
                    string prevShapeJson = SerializeShapeManual(action.PrevShape);
                    writer.WritePropertyName("PrevShape");
                    writer.WriteRawValue(prevShapeJson);
                }
                else
                {
                    writer.WriteNull("PrevShape");
                }
                if (action.NewShape != null)
                {
                    string newShapeJson = SerializeShapeManual(action.NewShape);
                    writer.WritePropertyName("NewShape");
                    writer.WriteRawValue(newShapeJson);
                }
                else
                {
                    writer.WriteNull("NewShape");
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static CanvasAction? DeserializeActionManual(string json)
    {
        // ... (implementation unchanged, ActionId fix included) ...
        if (string.IsNullOrEmpty(json)) { return null; }
        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null) { return null; }
        string actionId = doc["ActionId"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
        string? actionTypeName = doc["ActionType"]?.GetValue<string>();
        if (!Enum.TryParse<CanvasActionType>(actionTypeName, out CanvasActionType actionType))
        {
            return null;
        }
        IShape? prevShape = null;
        JsonNode? prevShapeNode = doc["PrevShape"];
        if (prevShapeNode != null && prevShapeNode.GetValueKind() == JsonValueKind.Object)
        {
            prevShape = DeserializeShapeManual(prevShapeNode.ToJsonString());
        }
        IShape? newShape = null;
        JsonNode? newShapeNode = doc["NewShape"];
        if (newShapeNode != null && newShapeNode.GetValueKind() == JsonValueKind.Object)
        {
            newShape = DeserializeShapeManual(newShapeNode.ToJsonString());
        }
        return new CanvasAction(actionId, actionType, prevShape, newShape);
    }

    public static byte[] JsonStringToBytes(string jsonString)
    {
        // ... (implementation unchanged) ...
        return Encoding.UTF8.GetBytes(jsonString);
    }

    public static string BytesToJsonString(byte[] byteArray)
    {
        // ... (implementation unchanged) ...
        return Encoding.UTF8.GetString(byteArray);
    }

    public static string SerializeShapesDictionary(Dictionary<string, IShape> dictionary)
    {
        // ... (implementation unchanged) ...
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var kvp in dictionary)
                {
                    writer.WritePropertyName(kvp.Key);
                    string shapeJson = SerializeShapeManual(kvp.Value);
                    writer.WriteRawValue(shapeJson);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static Dictionary<string, IShape> DeserializeShapesDictionary(string json)
    {
        // ... (implementation unchanged) ...
        var dictionary = new Dictionary<string, IShape>();
        if (string.IsNullOrEmpty(json)) { return dictionary; }
        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null) { return dictionary; }
        if (doc is JsonObject rootObject)
        {
            foreach (var property in rootObject)
            {
                string shapeId = property.Key;
                JsonNode? shapeNode = property.Value;
                if (shapeNode == null) continue;
                IShape? shape = DeserializeShapeManual(shapeNode.ToJsonString());
                if (shape != null)
                {
                    dictionary.Add(shapeId, shape);
                }
            }
        }
        return dictionary;
    }
}
