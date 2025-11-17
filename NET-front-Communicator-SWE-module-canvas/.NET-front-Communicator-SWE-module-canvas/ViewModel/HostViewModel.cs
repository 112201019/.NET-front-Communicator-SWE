using System;
using System.Diagnostics;
using CanvasDataModel;
// using CentralGui; // No longer needed
using System.Linq;
using System.Collections.Generic;

namespace ViewModel;

public class HostViewModel : BaseCanvasViewModel
{
    private readonly StateManager _hostActionManager = new();
    private readonly string _hostId;
    private readonly INetworkService _networkService; // <-- NEW

    public override string CurrentUserId => _hostId;

    // --- CONSTRUCTOR UPDATED ---
    public HostViewModel(string hostId, INetworkService networkService)
    {
        _hostId = hostId;
        _networkService = networkService;
        // Subscribe to messages from the network
        _networkService.MessageReceived += ProcessIncomingMessage;
    }
    // --- END UPDATE ---

    /// <summary>
    /// The Host's own local action (e.g., drawing) was finalized.
    /// It validates it against its own state and broadcasts it.
    /// </summary>
    protected override void OnActionFinalized(CanvasAction action, MessageType msgType)
    {
        bool isValid = true;

        if (action.ActionType != CanvasActionType.Create)
        {
            isValid = ValidateAction(action);
        }

        if (isValid)
        {
            Console.WriteLine($"[HOST] Host local action {action.ActionType} is valid.");
            if (msgType == MessageType.NORMAL)
            {
                _hostActionManager.AddAction(action);
            }
            else if (msgType == MessageType.UNDO)
            {
                _hostActionManager.Undo();
            }
            else if (msgType == MessageType.REDO)
            {
                _hostActionManager.Redo();
            }

            ApplyActionToDictionary(action);

            string actionJson = CanvasDataModelSerializer.SerializeActionManual(action);
            var message = new NetworkMessage(CurrentUserId, msgType, actionJson);

            // --- UPDATED CALL ---
            _networkService.SendMessage(message); // On Host, SendMessage = Broadcast
        }
        else
        {
            Console.WriteLine($"[HOST] Host local action {action.ActionType} was INVALID. Reverting.");
            if (action.PrevShape != null)
            {
                _shapes[action.PrevShape.ShapeId] = action.PrevShape;
                SelectedShape = action.PrevShape;
            }
        }
    }

    /// <summary>
    /// Host's own Undo request.
    /// </summary>
    public override void OnUndoRequested()
    {
        CanvasAction? lastAction = _hostActionManager.PeekUndo();
        if (lastAction == null || lastAction.ActionType == CanvasActionType.Initial) return;
        CanvasAction? reverseAction = CreateReverseAction(lastAction, CurrentUserId);
        if (reverseAction == null) return;
        OnActionFinalized(reverseAction, MessageType.UNDO);
    }

    /// <summary>
    /// Host's own Redo request.
    /// </summary>
    public override void OnRedoRequested()
    {
        CanvasAction? actionToRedo = _hostActionManager.PeekRedo();
        if (actionToRedo == null) return;
        CanvasAction? redoAction = CreateRedoAction(actionToRedo, CurrentUserId);
        if (redoAction == null) return;
        OnActionFinalized(redoAction, MessageType.REDO);
    }

    /// <summary>
    /// Processes an incoming message from a Client.
    /// </summary>
    public override void ProcessIncomingMessage(NetworkMessage message)
    {
        CanvasAction? action = CanvasDataModelSerializer.DeserializeActionManual(message.SerializedAction);
        if (action == null)
        {
            Console.WriteLine("[HOST] Received invalid action. Ignoring.");
            return;
        }

        bool isValid = ValidateAction(action);

        if (isValid)
        {
            Console.WriteLine($"[HOST] Client {message.SenderId} action {action.ActionType} is VALID. Applying and broadcasting.");

            if (message.MsgType == MessageType.NORMAL)
            {
                _hostActionManager.AddAction(action);
            }
            else if (message.MsgType == MessageType.UNDO)
            {
                _hostActionManager.Undo();
            }
            else if (message.MsgType == MessageType.REDO)
            {
                _hostActionManager.Redo();
            }

            ApplyActionToDictionary(action);

            // --- UPDATED CALL ---
            _networkService.SendMessage(message); // Broadcast the *original* valid message
        }
        else
        {
            Console.WriteLine($"[HOST] Client {message.SenderId} action {action.ActionType} is INVALID. Ignoring.");
        }
    }

    // ... (ValidateAction, ApplyActionToDictionary, CreateReverseAction, CreateRedoAction are all unchanged) ...
    private bool ValidateAction(CanvasAction action)
    {
        // CREATE actions are always valid
        if (action.ActionType == CanvasActionType.Create)
        {
            if (action.NewShape != null && _shapes.ContainsKey(action.NewShape.ShapeId))
            {
                Console.WriteLine($"[HOST VALIDATION] FAILED: Shape {action.NewShape.ShapeId} already exists.");
                return false;
            }
            return true;
        }
        if (action.PrevShape == null)
        {
            Console.WriteLine("[HOST VALIDATION] FAILED: Action has no PrevShape.");
            return false;
        }
        if (!_shapes.TryGetValue(action.PrevShape.ShapeId, out IShape? currentShape))
        {
            Console.WriteLine($"[HOST VALIDATION] FAILED: Shape {action.PrevShape.ShapeId} does not exist.");
            return false;
        }
        string clientPrevShapeJson = CanvasDataModelSerializer.SerializeShapeManual(action.PrevShape);
        string hostCurrentShapeJson = CanvasDataModelSerializer.SerializeShapeManual(currentShape);
        if (clientPrevShapeJson == hostCurrentShapeJson)
        {
            return true;
        }
        else
        {
            Console.WriteLine("[HOST VALIDATION] FAILED: Client state mismatch (desync).");
            Debug.WriteLine($"[HOST] Client's PrevShape:\n{clientPrevShapeJson}");
            Debug.WriteLine($"[HOST] Host's CurrentShape:\n{hostCurrentShapeJson}");
            return false;
        }
    }
    private void ApplyActionToDictionary(CanvasAction action)
    {
        if (action.NewShape != null)
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
}
