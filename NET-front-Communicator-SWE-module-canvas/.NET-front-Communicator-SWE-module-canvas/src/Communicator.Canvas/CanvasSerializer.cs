using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Communicator.Canvas;

/// <summary>
/// Static utility class for manually serializing and deserializing IShape objects
/// and CanvasAction objects to/from JSON strings and byte arrays.
/// </summary>
public static class CanvasSerializer
{
    // =========================================================================
    // Private Helpers
    // =========================================================================

    /// <summary>
    /// Converts a System.Drawing.Color to a hex string (e.g., "#FFFF0000").
    /// </summary>
    private static string ColorToHex(Color color)
    {
        return $"#{color.ToArgb():X8}";
    }

    /// <summary>
    /// Converts a hex string to a System.Drawing.Color.
    /// </summary>
    private static Color HexToColor(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length != 9)
        {
            return Color.Empty;
        }
        return Color.FromArgb(
            Convert.ToInt32(hexString.Substring(1, 8), 16)
        );
    }

    // =========================================================================
    // Core IShape Serialization (Manual JSON Construction)
    // =========================================================================

    /// <summary>
    /// Manually serializes an IShape object to a JSON string, ensuring only
    /// the core properties are included.
    /// </summary>
    /// <param name="shape">The IShape object to serialize.</param>
    /// <returns>A JSON string representing the shape.</returns>
    public static string SerializeShapeManual(IShape shape)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            writer.WriteString("ShapeId", shape.ShapeId);
            writer.WriteString("Type", shape.Type.ToString());

            writer.WritePropertyName("Points");
            writer.WriteStartArray();
            foreach (Point point in shape.Points)
            {
                writer.WriteStartObject();
                writer.WriteNumber("X", point.X);
                writer.WriteNumber("Y", point.Y);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteString("Color", $"#{shape.Color.ToArgb():X8}");
            writer.WriteNumber("Thickness", shape.Thickness);

            // --- MODIFIED PROPERTIES ---
            writer.WriteString("CreatedBy", shape.CreatedBy);
            writer.WriteString("LastModifiedBy", shape.LastModifiedBy);
            writer.WriteBoolean("IsDeleted", shape.IsDeleted);
            // --- UserId REMOVED ---

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Manually deserializes a JSON string back into an IShape object.
    /// </summary>
    public static IShape? DeserializeShapeManual(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null)
        {
            return null;
        }

        // --- MODIFIED PROPERTIES ---
        string? shapeId = doc["ShapeId"]?.GetValue<string>();
        string? typeName = doc["Type"]?.GetValue<string>();
        string? colorHex = doc["Color"]?.GetValue<string>();
        double thickness = doc["Thickness"]?.GetValue<double>() ?? 1.0;
        string? createdBy = doc["CreatedBy"]?.GetValue<string>();
        string? lastModifiedBy = doc["LastModifiedBy"]?.GetValue<string>();
        bool isDeleted = doc["IsDeleted"]?.GetValue<bool>() ?? false;
        // --- UserId REMOVED ---

        Color color = Color.Black;
        if (!string.IsNullOrEmpty(colorHex))
        {
            color = Color.FromArgb(Convert.ToInt32(colorHex.Substring(1), 16));
        }

        List<Point> points = new List<Point>();
        JsonArray? pointsArray = doc["Points"]?.AsArray();
        if (pointsArray != null)
        {
            foreach (JsonNode? pointNode in pointsArray)
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
            // --- MODIFIED: Use new full constructor ---
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

    // =========================================================================
    // CanvasAction Serialization
    // =========================================================================

    /// <summary>
    /// Serializes a CanvasAction object, including its PrevShape and NewShape fields,
    /// by recursively calling the shape serializer.
    /// </summary>
    public static string SerializeActionManual(CanvasAction action)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            // --- ADDED ---
            writer.WriteString("ActionId", action.ActionId);
            // --- END ADDED ---

            writer.WriteString("ActionType", action.ActionType.ToString());

            if (action.PrevShape != null)
            {
                string prevShapeJson = SerializeShapeManual(action.PrevShape);
                writer.WritePropertyName("Prev");
                writer.WriteRawValue(prevShapeJson);
            }
            else
            {
                writer.WriteNull("Prev");
            }

            if (action.NewShape != null)
            {
                string newShapeJson = SerializeShapeManual(action.NewShape);
                writer.WritePropertyName("Next");
                writer.WriteRawValue(newShapeJson);
            }
            else
            {
                writer.WriteNull("Next");
            }

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserializes a JSON string back into a CanvasAction object.
    /// </summary>
    public static CanvasAction? DeserializeActionManual(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null)
        {
            return null;
        }

        // --- ADDED (with fallback for safety) ---
        string actionId = doc["ActionId"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
        // --- END ADDED ---

        string? actionTypeName = doc["ActionType"]?.GetValue<string>();
        if (!Enum.TryParse<CanvasActionType>(actionTypeName, out CanvasActionType actionType))
        {
            return null;
        }

        IShape? prevShape = null;
        JsonNode? prevShapeNode = doc["Prev"];
        if (prevShapeNode != null && prevShapeNode.GetValueKind() == JsonValueKind.Object)
        {
            prevShape = DeserializeShapeManual(prevShapeNode.ToJsonString());
        }

        IShape? newShape = null;
        JsonNode? newShapeNode = doc["Next"];
        if (newShapeNode != null && newShapeNode.GetValueKind() == JsonValueKind.Object)
        {
            newShape = DeserializeShapeManual(newShapeNode.ToJsonString());
        }

        // --- MODIFIED: Pass ActionId to new constructor ---
        return new CanvasAction(actionId, actionType, prevShape, newShape);
    }


    /// <summary>
    /// Helper to write raw JSON string content (like a nested shape object) into the current writer.
    /// This is necessary because the shape serialization already wrapped the object in {}.
    /// </summary>
    private static void WriteRawJson(Utf8JsonWriter writer, string rawJson)
    {
        // This is necessary because the shape serialization already wrote its own object { }
        // We parse it and write its contents directly to the current action writer stream.
        using JsonDocument document = JsonDocument.Parse(rawJson);
        document.RootElement.WriteTo(writer);
    }

    // =========================================================================
    // Byte Array Conversion Functions
    // =========================================================================

    /// <summary>
    /// Converts a JSON string to a UTF-8 encoded byte array.
    /// </summary>
    public static byte[] JsonStringToBytes(string jsonString)
    {
        return Encoding.UTF8.GetBytes(jsonString);
    }

    /// <summary>
    /// Converts a UTF-8 encoded byte array back into a JSON string.
    /// </summary>
    public static string BytesToJsonString(byte[] byteArray)
    {
        return Encoding.UTF8.GetString(byteArray);
    }

    // --- NEW: STACK SERIALIZATION METHODS ---

    /// <summary>
    /// Serializes the entire Action Stack DTO to a JSON string.
    /// We use System.Text.Json (automatic) here because the DTO is simple
    /// and already uses our manual action serializer.
    /// </summary>
    public static string SerializeActionStack(SerializedActionStack stackDto)
    {
        // We must configure System.Text.Json to handle the CanvasAction objects
        // within the DTO, which themselves contain IShape.
        // This is getting very complex.

        // --- REVISED, MANUAL APPROACH (More robust) ---

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("CurrentIndex", stackDto.CurrentIndex);

            writer.WritePropertyName("AllActions");
            writer.WriteStartArray();
            foreach (CanvasAction action in stackDto.AllActions)
            {
                // Call our manual action serializer for each action
                string actionJson = SerializeActionManual(action);
                writer.WriteRawValue(actionJson);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserializes a JSON string back into the Action Stack DTO.
    /// </summary>
    public static SerializedActionStack? DeserializeActionStack(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null)
        {
            return null;
        }

        var dto = new SerializedActionStack
        {
            CurrentIndex = doc["CurrentIndex"]?.GetValue<int>() ?? -1
        };

        JsonArray? actionsArray = doc["AllActions"]?.AsArray();
        if (actionsArray != null)
        {
            foreach (JsonNode? actionNode in actionsArray)
            {
                if (actionNode != null)
                {
                    // Call our manual action deserializer
                    CanvasAction? action = DeserializeActionManual(actionNode.ToJsonString());
                    if (action != null)
                    {
                        dto.AllActions.Add(action);
                    }
                }
            }
        }

        return dto;
    }
    // --- END NEW ---
    // --- NEW: SHAPES DICTIONARY SERIALIZATION ---


    /// <summary>
    /// Serializes the main shapes dictionary to a JSON string.
    /// </summary>
    public static string SerializeShapesDictionary(Dictionary<string, IShape> dictionary)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            // ---
            // --- MAJOR LOGIC CHANGE ---
            // ---
            // The root is an object
            writer.WriteStartObject();

            foreach (KeyValuePair<string, IShape> kvp in dictionary)
            {
                // The Key (e.g., "shape-id-1") is the property name
                writer.WritePropertyName(kvp.Key);

                // The Value is the serialized shape object directly
                string shapeJson = SerializeShapeManual(kvp.Value);
                writer.WriteRawValue(shapeJson);
            }

            writer.WriteEndObject();
            // ---
            // ---
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserializes a JSON string back into the main shapes dictionary.
    /// </summary>
    public static Dictionary<string, IShape> DeserializeShapesDictionary(string json)
    {
        // --- MODIFIED: Return type is simpler ---
        var dictionary = new Dictionary<string, IShape>();
        if (string.IsNullOrEmpty(json))
        {
            return dictionary;
        }

        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null)
        {
            return dictionary;
        }

        // ---
        // --- MAJOR LOGIC CHANGE ---
        // ---
        if (doc is JsonObject rootObject)
        {
            foreach (KeyValuePair<string, JsonNode?> property in rootObject)
            {
                string shapeId = property.Key;
                JsonNode? shapeNode = property.Value; // The value *is* the shape node

                if (shapeNode == null)
                {
                    continue;
                }

                IShape? shape = DeserializeShapeManual(shapeNode.ToJsonString());

                if (shape != null)
                {
                    // Add the deserialized shape directly
                    dictionary.Add(shapeId, shape);
                }
            }
        }
        // ---
        // ---

        return dictionary;
    }
    // --- END NEW ---

    // =========================================================================
    // --- NEW: NETWORK MESSAGE SERIALIZATION ---
    // =========================================================================

    /// <summary>
    /// Serializes a NetworkMessage C# object to a JSON string.
    /// This will internally serialize the CanvasAction to a string.
    /// </summary>

    public static string SerializeNetworkMessage(NetworkMessage message)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();

            writer.WriteString("MessageType", message.MessageType.ToString());

            if (message.Action != null)
            {
                writer.WritePropertyName("Action");
                string actionJson = SerializeActionManual(message.Action);
                writer.WriteRawValue(actionJson);
            }

            if (!string.IsNullOrEmpty(message.Payload))
            {
                writer.WriteString("Payload", message.Payload);
            }

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static NetworkMessage? DeserializeNetworkMessage(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        JsonNode? doc = JsonNode.Parse(json);
        if (doc == null)
        {
            return null;
        }

        string? messageTypeName = doc["MessageType"]?.GetValue<string>();
        if (!Enum.TryParse<NetworkMessageType>(messageTypeName, out NetworkMessageType messageType))
        {
            return null;
        }

        CanvasAction? action = null;
        JsonNode? actionNode = doc["Action"];
        if (actionNode != null)
        {
            action = DeserializeActionManual(actionNode.ToJsonString());
        }

        string? payload = doc["Payload"]?.GetValue<string>();

        return new NetworkMessage(messageType, action, payload);
    }
}
