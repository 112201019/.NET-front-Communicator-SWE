using System;
using System.Collections.Generic;

namespace Communicator.Canvas;

/// <summary>
/// Represents a node in the Undo/Redo history chain.
/// Implements a **Doubly Linked List** node structure.
/// </summary>
public class ActionNode
{
    /// <summary>
    /// The action data stored in this node (The Command).
    /// </summary>
    public CanvasAction Action { get; }

    /// <summary>
    /// Reference to the previous action (Undo direction).
    /// </summary>
    public ActionNode? Prev { get; set; }

    /// <summary>
    /// Reference to the next action (Redo direction).
    /// </summary>
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

/// <summary>
/// Manages the application state history.
/// Implements the **Command/Memento Pattern** logic using a linear history stack.
/// </summary>
public class StateManager
{
    private ActionNode? _current;
    private readonly ActionNode _initial;

    public StateManager()
    {
        // Sentinel node acting as the "Initial State" before any actions.
        _initial = new ActionNode(new CanvasAction(CanvasActionType.Initial, null, null));
        _current = _initial;
    }

    /// <summary>
    /// Adds a new action to the history.
    /// This invalidates any existing "Redo" history (branching history is not supported).
    /// </summary>
    /// <param name="action">The action to record.</param>
    public void AddAction(CanvasAction action)
    {
        var node = new ActionNode(action);

        if (_current != null)
        {
            // Sever the link to any future actions (clearing Redo stack)
            _current.Next = null;

            // Link new node
            node.Prev = _current;
            _current.Next = node;
        }

        // Advance pointer
        _current = node;
    }

    /// <summary>
    /// Moves the state pointer backward one step.
    /// </summary>
    /// <returns>The action that was undone, or null if at start.</returns>
    public CanvasAction? Undo()
    {
        // Cannot undo the initial sentinel node
        if (_current?.Prev != null)
        {
            CanvasAction actionToUndo = _current.Action;
            _current = _current.Prev;
            return actionToUndo;
        }
        return null;
    }

    /// <summary>
    /// Moves the state pointer forward one step.
    /// </summary>
    /// <returns>The action that was redone, or null if at end.</returns>
    public CanvasAction? Redo()
    {
        if (_current?.Next != null)
        {
            _current = _current.Next;
            return _current.Action;
        }
        return null;
    }

    /// <summary>
    /// Peeks at the action that would be undone, without modifying state.
    /// Useful for Client-Side Prediction checks.
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
    /// Peeks at the action that would be redone, without modifying state.
    /// </summary>
    public CanvasAction? PeekRedo()
    {
        if (_current?.Next != null)
        {
            return _current.Next.Action;
        }
        return null;
    }

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
