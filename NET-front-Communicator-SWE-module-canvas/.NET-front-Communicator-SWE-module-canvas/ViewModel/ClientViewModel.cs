using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using CanvasDataModel;
// using CentralGui; // No longer needed
using System.Linq; // <-- ADDED

namespace ViewModel;

public class ClientViewModel : BaseCanvasViewModel
{
    private readonly StateManager _clientActionManager = new();
    private readonly string _clientId;
    private DispatcherTimer _ghostTimer;
    private readonly INetworkService _networkService; // <-- NEW

    public override string CurrentUserId => _clientId;

    // --- CONSTRUCTOR UPDATED ---
    public ClientViewModel(string clientId, INetworkService networkService)
    {
        _clientId = clientId;
        _networkService = networkService;
        // Subscribe to messages from the network
        _networkService.MessageReceived += ProcessIncomingMessage;

        _ghostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _ghostTimer.Tick += (s, e) =>
        {
            GhostShape = null;
            _ghostTimer.Stop();
        };
    }
    // --- END UPDATE ---

    /// <summary>
    /// A local action was finalized (e.g., mouse up).
    /// Send it to the host for validation.
    /// </summary>
    protected override void OnActionFinalized(CanvasAction action, MessageType msgType)
    {
        ShowGhostShape(action);

        if (action.PrevShape != null)
        {
            _shapes[action.PrevShape.ShapeId] = action.PrevShape;
            if (SelectedShape?.ShapeId == action.PrevShape.ShapeId)
            {
                SelectedShape = action.PrevShape;
            }
        }

        string actionJson = CanvasDataModelSerializer.SerializeActionManual(action);
        var message = new NetworkMessage(CurrentUserId, msgType, actionJson);

        // --- UPDATED CALL ---
        _networkService.SendMessage(message);
    }

    /// <summary>
    /// User pressed Ctrl+Z. Send an UNDO message to the host.
    /// </summary>
    public override void OnUndoRequested()
    {
        CanvasAction? lastClientAction = _clientActionManager.PeekUndo();
        if (lastClientAction == null || lastClientAction.ActionType == CanvasActionType.Initial)
        {
            Console.WriteLine("[CLIENT] No local actions to undo.");
            return;
        }

        CanvasAction? reverseAction = CreateReverseAction(lastClientAction, CurrentUserId);
        if (reverseAction == null)
        {
            Console.WriteLine("[CLIENT] Could not create reverse action.");
            return;
        }

        ShowGhostShape(reverseAction);

        string actionJson = CanvasDataModelSerializer.SerializeActionManual(reverseAction);
        var message = new NetworkMessage(CurrentUserId, MessageType.UNDO, actionJson);

        // --- UPDATED CALL ---
        _networkService.SendMessage(message);
    }

    /// <summary>
    /// User pressed Ctrl+Y. Send a REDO message to the host.
    /// </summary>
    public override void OnRedoRequested()
    {
        CanvasAction? actionToRedo = _clientActionManager.PeekRedo();
        if (actionToRedo == null)
        {
            Console.WriteLine("[CLIENT] No local actions to redo.");
            return;
        }

        CanvasAction? redoAction = CreateRedoAction(actionToRedo, CurrentUserId);
        if (redoAction == null) return;

        ShowGhostShape(redoAction);

        string actionJson = CanvasDataModelSerializer.SerializeActionManual(redoAction);
        var message = new NetworkMessage(CurrentUserId, MessageType.REDO, actionJson);

        // --- UPDATED CALL ---
        _networkService.SendMessage(message);
    }

    /// <summary>
    /// Process an incoming message (from the Host).
    /// </summary>
    public override void ProcessIncomingMessage(NetworkMessage message)
    {
        CanvasAction? action = CanvasDataModelSerializer.DeserializeActionManual(message.SerializedAction);
        if (action == null)
        {
            Console.WriteLine("[CLIENT] Failed to deserialize action.");
            return;
        }

        IShape? shape = action.NewShape ?? action.PrevShape;
        if (shape == null) return;

        bool isMyAction = shape.LastModifiedBy == CurrentUserId;

        if (isMyAction && GhostShape != null)
        {
            if (action.ActionType != CanvasActionType.Create || GhostShape.ShapeId == shape.ShapeId)
            {
                GhostShape = null;
                _ghostTimer.Stop();
            }
        }

        ApplyActionToDictionary(action);

        if (isMyAction)
        {
            if (message.MsgType == MessageType.NORMAL)
            {
                _clientActionManager.AddAction(action);
                Console.WriteLine($"[CLIENT] Confirmed and added local action: {action.ActionType}");
            }
            else if (message.MsgType == MessageType.UNDO)
            {
                var undone = _clientActionManager.Undo();
                Console.WriteLine($"[CLIENT] Confirmed and performed local UNDO. (Popped {undone?.ActionType})");
            }
            else if (message.MsgType == MessageType.REDO)
            {
                var redone = _clientActionManager.Redo();
                Console.WriteLine($"[CLIENT] Confirmed and performed local REDO. (Pushed {redone?.ActionType})");
            }
        }
        else
        {
            Console.WriteLine($"[CLIENT] Received remote action from {shape.LastModifiedBy}");
        }

        if (SelectedShape?.ShapeId == shape.ShapeId)
        {
            SelectedShape = _shapes.GetValueOrDefault(shape.ShapeId);
        }

        OnPropertyChanged(nameof(SelectedShape)); // Force UI refresh
    }

    // ... (ApplyActionToDictionary, CreateReverseAction, CreateRedoAction, ShowGhostShape are all unchanged) ...
    private void ApplyActionToDictionary(CanvasAction action)
    {
        if (action.ActionType == CanvasActionType.Create && action.NewShape != null)
        {
            _shapes[action.NewShape.ShapeId] = action.NewShape;
        }
        else if (action.ActionType == CanvasActionType.Modify && action.NewShape != null)
        {
            _shapes[action.NewShape.ShapeId] = action.NewShape;
        }
        else if (action.ActionType == CanvasActionType.Delete && action.NewShape != null)
        {
            _shapes[action.NewShape.ShapeId] = action.NewShape;
        }
        else if (action.ActionType == CanvasActionType.Resurrect && action.NewShape != null)
        {
            _shapes[action.NewShape.ShapeId] = action.NewShape;
        }
    }
    private CanvasAction? CreateReverseAction(CanvasAction action, string userId)
    {
        if (action.ActionType == CanvasActionType.Create && action.NewShape != null)
        {
            IShape deletedShape = action.NewShape.WithDelete(userId);
            return new CanvasAction(CanvasActionType.Delete, action.NewShape, deletedShape);
        }
        if (action.ActionType == CanvasActionType.Modify && action.PrevShape != null && action.NewShape != null)
        {
            IShape revertedShape = action.PrevShape.WithUpdates(null, null, userId);
            return new CanvasAction(CanvasActionType.Modify, action.NewShape, revertedShape);
        }
        if (action.ActionType == CanvasActionType.Delete && action.PrevShape != null && action.NewShape != null)
        {
            IShape resurrectedShape = action.PrevShape.WithResurrect(userId);
            return new CanvasAction(CanvasActionType.Resurrect, action.NewShape, resurrectedShape);
        }
        if (action.ActionType == CanvasActionType.Resurrect && action.PrevShape != null && action.NewShape != null)
        {
            IShape deletedShape = action.PrevShape.WithDelete(userId);
            return new CanvasAction(CanvasActionType.Delete, action.NewShape, deletedShape);
        }
        return null;
    }
    private CanvasAction? CreateRedoAction(CanvasAction action, string userId)
    {
        if (action.NewShape != null)
        {
            IShape? prev = action.PrevShape?.WithUpdates(null, null, userId);
            IShape? next = action.NewShape.WithUpdates(null, null, userId);
            return new CanvasAction(action.ActionType, prev, next);
        }
        return null;
    }
    private void ShowGhostShape(CanvasAction action)
    {
        if (action.ActionType == CanvasActionType.Create)
        {
            GhostShape = action.NewShape;
        }
        else if (action.ActionType == CanvasActionType.Modify)
        {
            GhostShape = action.NewShape;
        }
        else if (action.ActionType == CanvasActionType.Delete)
        {
            GhostShape = action.PrevShape;
        }
        else if (action.ActionType == CanvasActionType.Resurrect)
        {
            GhostShape = action.NewShape;
        }

        _ghostTimer.Stop();
        _ghostTimer.Start();
    }
}
