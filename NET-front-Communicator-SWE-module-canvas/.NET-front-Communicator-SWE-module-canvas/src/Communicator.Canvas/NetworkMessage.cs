namespace Communicator.Canvas;

/// <summary>
/// Represents a C# object for a network message, which can be
/// serialized for transmission.
/// </summary>
public class NetworkMessage
{
    /// <summary>
    /// The type of message (e.g., NORMAL, UNDO, REDO, RESTORE).
    /// </summary>
    public NetworkMessageType MessageType { get; }

    /// <summary>
    /// The CanvasAction associated with this message (Optional for RESTORE).
    /// </summary>
    public CanvasAction? Action { get; }

    /// <summary>
    /// Optional payload string (e.g., JSON dictionary for RESTORE).
    /// </summary>
    public string? Payload { get; }

    /// <summary>
    /// Constructor for creating a new network message.
    /// </summary>
    public NetworkMessage(NetworkMessageType messageType, CanvasAction? action, string? payload = null)
    {
        MessageType = messageType;
        Action = action;
        Payload = payload;
    }
}
