namespace CanvasDataModel;

/// <summary>
/// Internal node for the doubly-linked list, now holding a CanvasAction.
/// </summary>
public class ActionNode
{
    public CanvasAction Action { get; }
    public ActionNode? Prev { get; set; }
    public ActionNode? Next { get; set; }

    public ActionNode(CanvasAction action)
    {
        Action = action;
    }
}

/// <summary>
/// Manages the undo/redo stack using a list of actions (Command Pattern).
/// </summary>
public class StateManager
{
    private ActionNode? _current;

    // --- NEW: Added _initial to track the start ---
    private readonly ActionNode _initial;

    public StateManager()
    {
        // --- MODIFIED ---
        _initial = new ActionNode(new CanvasAction(CanvasActionType.Initial, null, null));
        _current = _initial;
        // --- END MODIFIED ---
    }

    public void AddAction(CanvasAction action)
    {
        var node = new ActionNode(action);

        if (_current != null)
        {
            _current.Next = null;
            node.Prev = _current;
            _current.Next = node;
        }

        _current = node;
    }

    public CanvasAction? Undo()
    {
        if (_current?.Prev != null)
        {
            CanvasAction actionToUndo = _current.Action;
            _current = _current.Prev;
            return actionToUndo;
        }
        return null;
    }

    public CanvasAction? Redo()
    {
        if (_current?.Next != null)
        {
            _current = _current.Next;
            return _current.Action;
        }
        return null;
    }

    // --- NEW METHODS for Client ---

    /// <summary>
    /// Peeks at the action that *would be undone*.
    /// </summary>
    public CanvasAction? PeekUndo()
    {
        return _current?.Action;
    }

    /// <summary>
    /// Peeks at the action that *would be redone*.
    /// </summary>
    public CanvasAction? PeekRedo()
    {
        return _current?.Next?.Action;
    }
    // --- END NEW ---

    public CanvasAction? CurrentAction => _current?.Action;
}
