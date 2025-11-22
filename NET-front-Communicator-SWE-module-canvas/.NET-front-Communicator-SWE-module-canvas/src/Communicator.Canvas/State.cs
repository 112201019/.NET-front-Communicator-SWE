using System; // <-- ADDED for Guid

namespace Communicator.Canvas;

/// <summary>
/// Represents a single undo-able/redo-able action on the canvas.
/// This replaces the old 'State' class which stored the entire canvas snapshot.
/// </summary>
public class CanvasAction
{
    // --- NEW PROPERTY ---
    /// <summary>
    /// A unique ID for this specific action instance.
    /// </summary>
    public string ActionId { get; }
    // --- END NEW ---

    /// <summary>
    /// The type of action that was performed (e.g., Create, Modify, Delete).
    /// </summary>
    public CanvasActionType ActionType { get; }

    /// <summary>
    /// The state of the shape *before* the action.
    /// </summary>
    public IShape? PrevShape { get; }

    /// <summary>
    /// The state of the shape *after* the action.
    /// </summary>
    public IShape? NewShape { get; }

    /// <summary>
    /// Constructor for creating a new action. A new Guid will be generated.
    /// </summary>
    public CanvasAction(CanvasActionType actionType, IShape? prevShape, IShape? newShape)
    {
        ActionId = Guid.NewGuid().ToString(); // <-- ADDED
        ActionType = actionType;
        PrevShape = prevShape;
        NewShape = newShape;
    }

    // --- NEW CONSTRUCTOR ---
    /// <summary>
    /// Public constructor for deserialization.
    /// </summary>
    public CanvasAction(string actionId, CanvasActionType actionType, IShape? prevShape, IShape? newShape)
    {
        ActionId = actionId; // <-- USES PROVIDED ID
        ActionType = actionType;
        PrevShape = prevShape;
        NewShape = newShape;
    }
    // --- END NEW ---
}
