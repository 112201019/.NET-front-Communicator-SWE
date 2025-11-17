using CanvasDataModel;
using System;

namespace CanvasDataModel;

public enum MessageType
{
    NORMAL, // For Create, Modify, Delete, Resurrect
    UNDO,
    REDO
}

public class NetworkMessage
{
    /// <summary>
    /// The ID of the client who sent this message.
    /// </summary>
    public string SenderId { get; set; }

    /// <summary>
    /// The type of message being sent.
    /// </summary>
    public MessageType MsgType { get; set; }

    /// <summary>
    /// The JSON-serialized CanvasAction.
    /// </summary>
    public string SerializedAction { get; set; }

    public NetworkMessage(string senderId, MessageType msgType, string serializedAction)
    {
        SenderId = senderId;
        MsgType = msgType;
        SerializedAction = serializedAction;
    }

    // For deserialization
    public NetworkMessage() { }
}
