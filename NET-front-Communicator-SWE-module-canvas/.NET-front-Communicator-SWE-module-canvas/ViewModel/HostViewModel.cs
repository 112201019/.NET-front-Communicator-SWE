using System;
using System.Collections.Generic;
using Communicator.Canvas;
using Microsoft.Win32;
namespace ViewModel;
using System.IO; // Still keep this

public class HostViewModel : CanvasViewModel
{
    private readonly string _myIp = "127.0.0.1";
    private readonly List<string> _clientIps = new() { "192.168.1.50" };
    private bool _suppressCommit = false;
    // --- OVERRIDE TO TRUE ---
    public override bool IsHost => true;
    // ------------------------
    public HostViewModel()
    {
        CurrentUserId = "Host_Admin";
        NetworkMock.Register(_myIp, ProcessIncomingMessage);
    }

    public override void CommitModification()
    {
        if (_suppressCommit)
        {
            return;
        }

        base.CommitModification();
    }

    protected override void ProcessAction(CanvasAction action)
    {
        if (action.ActionType == CanvasActionType.Modify && action.PrevShape != null)
        {
            if (_shapes.ContainsKey(action.PrevShape.ShapeId))
            {
                _shapes[action.PrevShape.ShapeId] = action.PrevShape;
                if (SelectedShape != null && SelectedShape.ShapeId == action.PrevShape.ShapeId)
                {
                    _suppressCommit = true;
                    try { base.SelectedShape = action.PrevShape; }
                    finally { _suppressCommit = false; }
                }
            }
        }

        if (ValidateAction(action))
        {
            ApplyActionLocally(action);
            var msg = new NetworkMessage(NetworkMessageType.NORMAL, action);
            string json = CanvasSerializer.SerializeNetworkMessage(msg);
            NetworkMock.Broadcast(_clientIps, json);
        }
        else
        {
            Console.WriteLine($"[Host] Local Action Rejected: {action.ActionType} on {action.NewShape?.ShapeId}");
            RaiseRequestRedraw();
        }
    }

    public override void Undo()
    {
        CommitModification();
        SelectedShape = null;
        CanvasAction? actionToUndo = _stateManager.PeekUndo();
        base.Undo();

        if (actionToUndo != null)
        {
            CanvasAction reverseAction = GetInverseAction(actionToUndo, CurrentUserId);
            var msg = new NetworkMessage(NetworkMessageType.UNDO, reverseAction);
            string json = CanvasSerializer.SerializeNetworkMessage(msg);
            NetworkMock.Broadcast(_clientIps, json);
        }
    }

    public override void Redo()
    {
        SelectedShape = null;
        CanvasAction? actionToRedo = _stateManager.PeekRedo();
        base.Redo();

        if (actionToRedo != null)
        {
            var msg = new NetworkMessage(NetworkMessageType.REDO, actionToRedo);
            string json = CanvasSerializer.SerializeNetworkMessage(msg);
            NetworkMock.Broadcast(_clientIps, json);
        }
    }

    //public void ProcessIncomingMessage(string json)
    //{
    //    NetworkMessage? msg = CanvasSerializer.DeserializeNetworkMessage(json);
    //    if (msg == null) return;

    //    CanvasAction action = msg.Action;

    //    if (ValidateAction(action))
    //    {
    //        if (action.NewShape != null)
    //        {
    //            // KEY FIX: Use helper to sync dictionary and selection
    //            UpdateShapeFromNetwork(action.NewShape);
    //        }

    //        RaiseRequestRedraw();
    //        NetworkMock.Broadcast(_clientIps, json);
    //    }
    //    else
    //    {
    //        Console.WriteLine($"[Host] Validation Failed for Incoming Action: {action.ActionType}");
    //    }
    //}
    // --- NEW: RESTORE FEATURE ---
    // --- NEW: RESTORE FEATURE ---
    public void RestoreShapes()
    {
        OpenFileDialog openDialog = new OpenFileDialog
        {
            Filter = "Canvas JSON (*.json)|*.json"
        };

        if (openDialog.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(openDialog.FileName);

                // 1. Apply Locally
                ApplyRestore(json);

                // 2. Broadcast RESTORE Message
                var msg = new NetworkMessage(NetworkMessageType.RESTORE, null, json);
                string networkJson = CanvasSerializer.SerializeNetworkMessage(msg);

                Console.WriteLine("[Host] Broadcasting RESTORE command...");
                NetworkMock.Broadcast(_clientIps, networkJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Host] Failed to restore: {ex.Message}");
            }
        }
    }
    // ----------------------------

    public void ProcessIncomingMessage(string json)
    {
        NetworkMessage? msg = CanvasSerializer.DeserializeNetworkMessage(json);
        if (msg == null)
        {
            return;
        }

        // Host usually only receives its own broadcasts in this setup, or client actions
        // Logic kept for Client Actions
        if (msg.MessageType == NetworkMessageType.NORMAL || msg.MessageType == NetworkMessageType.UNDO || msg.MessageType == NetworkMessageType.REDO)
        {
            if (msg.Action != null)
            {
                CanvasAction action = msg.Action;
                if (ValidateAction(action))
                {
                    if (action.NewShape != null)
                    {
                        UpdateShapeFromNetwork(action.NewShape);
                    }

                    RaiseRequestRedraw();
                    NetworkMock.Broadcast(_clientIps, json);
                }
                else
                {
                    Console.WriteLine($"[Host] Validation Failed for Incoming Action: {action.ActionType}");
                }
            }
        }
        // Host ignores incoming RESTORE messages (it is the source)
    }
    private bool ValidateAction(CanvasAction action)
    {
        string shapeId = action.NewShape?.ShapeId ?? action.PrevShape?.ShapeId ?? "";
        if (string.IsNullOrEmpty(shapeId))
        {
            return false;
        }

        switch (action.ActionType)
        {
            case CanvasActionType.Create:
                return true;

            case CanvasActionType.Delete:
            case CanvasActionType.Modify:
            case CanvasActionType.Resurrect:
                if (!_shapes.ContainsKey(shapeId))
                {
                    return false;
                }

                IShape currentHostShape = _shapes[shapeId];
                IShape? incomingPrevShape = action.PrevShape;

                if (incomingPrevShape == null)
                {
                    return false;
                }

                if (currentHostShape.ShapeId != incomingPrevShape.ShapeId)
                {
                    return false;
                }

                if (currentHostShape.LastModifiedBy != incomingPrevShape.LastModifiedBy)
                {
                    Console.WriteLine($"[Host] Version Mismatch! HostVer: {currentHostShape.LastModifiedBy}, IncomingVer: {incomingPrevShape.LastModifiedBy}");
                    return false;
                }

                return true;

            default:
                return false;
        }
    }
}
