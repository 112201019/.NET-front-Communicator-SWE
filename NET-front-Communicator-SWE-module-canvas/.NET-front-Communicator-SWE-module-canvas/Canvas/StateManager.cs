using System;
using System.Collections.Generic;

namespace CanvasDataModel;

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

public class SerializedActionStack
{
    public List<CanvasAction> AllActions { get; set; } = new();
    public int CurrentIndex { get; set; } = -1;
}

public class StateManager
{
    private ActionNode? _current;
    private readonly ActionNode _initial;

    public StateManager()
    {
        _initial = new ActionNode(new CanvasAction(CanvasActionType.Initial, null, null));
        _current = _initial;
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

    // --- NEW: PEEK METHODS FOR CLIENT PREDICTION ---

    /// <summary>
    /// Returns the action that *would* be undone if Undo() was called, 
    /// without changing the state.
    /// </summary>
    public CanvasAction? PeekUndo()
    {
        if (_current?.Prev != null)
        {
            return _current.Action;
        }
        return null;
    }

    /// <summary>
    /// Returns the action that *would* be redone if Redo() was called,
    /// without changing the state.
    /// </summary>
    public CanvasAction? PeekRedo()
    {
        if (_current?.Next != null)
        {
            return _current.Next.Action;
        }
        return null;
    }
    // --- END NEW ---

    public CanvasAction? CurrentAction => _current?.Action;

    public SerializedActionStack ExportState()
    {
        var dto = new SerializedActionStack();
        ActionNode? node = _initial;
        int index = 0;

        while (node != null)
        {
            dto.AllActions.Add(node.Action);
            if (node == _current)
            {
                dto.CurrentIndex = index;
            }
            node = node.Next;
            index++;
        }
        return dto;
    }

    public void ImportState(SerializedActionStack dto)
    {
        if (dto.AllActions.Count == 0 || dto.CurrentIndex == -1)
        {
            _current = _initial;
            _initial.Next = null;
            return;
        }

        _initial.Next = null;
        _current = _initial;

        ActionNode? targetCurrentNode = _initial;

        for (int j = 1; j < dto.AllActions.Count; j++)
        {
            AddAction(dto.AllActions[j]);
            if (j == dto.CurrentIndex)
            {
                targetCurrentNode = _current;
            }
        }
        _current = targetCurrentNode;
    }
}
