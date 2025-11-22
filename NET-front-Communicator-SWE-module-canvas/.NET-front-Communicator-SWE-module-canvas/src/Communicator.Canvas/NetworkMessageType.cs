namespace Communicator.Canvas;

/// <summary>
/// Defines the type of network message being sent.
/// </summary>
public enum NetworkMessageType
{
    /// <summary>
    /// A standard action (Create, Modify, Delete, Resurrect).
    /// </summary>
    NORMAL = 0,

    /// <summary>
    /// A message indicating a remote Undo operation.
    /// </summary>
    UNDO = 1,

    /// <summary>
    /// A message indicating a remote Redo operation.
    /// </summary>
    REDO = 2,

    /// <summary>
    /// A message indicating a full state restore (reset).
    /// </summary>
    RESTORE = 3
}
