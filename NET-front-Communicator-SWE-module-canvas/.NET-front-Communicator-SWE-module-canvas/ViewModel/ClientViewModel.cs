using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Communicator.Canvas;
using Microsoft.Win32;

namespace ViewModel;

public class ClientViewModel : CanvasViewModel
{
    private readonly string _myIp = "192.168.1.50";
    private readonly string _hostIp = "127.0.0.1";
    private bool _suppressCommit = false;

    public ClientViewModel()
    {
        CurrentUserId = "Client_" + Guid.NewGuid().ToString().Substring(0, 4);
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
                RaiseRequestRedraw();
            }
        }

        var msg = new NetworkMessage(NetworkMessageType.NORMAL, action);
        string json = CanvasSerializer.SerializeNetworkMessage(msg);
        NetworkMock.SendMessage(_hostIp, json);
        ShowGhostShape(action);
    }

    public override void Undo()
    {
        CommitModification();
        SelectedShape = null;
        CanvasAction? actionToUndo = _stateManager.PeekUndo();

        if (actionToUndo != null)
        {
            CanvasAction reverseAction = GetInverseAction(actionToUndo, CurrentUserId);
            var msg = new NetworkMessage(NetworkMessageType.UNDO, reverseAction);
            string json = CanvasSerializer.SerializeNetworkMessage(msg);
            NetworkMock.SendMessage(_hostIp, json);
        }
    }

    public override void Redo()
    {
        SelectedShape = null;
        CanvasAction? actionToRedo = _stateManager.PeekRedo();

        if (actionToRedo != null)
        {
            var msg = new NetworkMessage(NetworkMessageType.REDO, actionToRedo);
            string json = CanvasSerializer.SerializeNetworkMessage(msg);
            NetworkMock.SendMessage(_hostIp, json);
        }
    }

    private async void ShowGhostShape(CanvasAction action)
    {
        if (action.NewShape != null)
        {
            IShape ghost = action.NewShape;
            GhostShapes.Add(ghost);
            RaiseRequestRedraw();
            await Task.Delay(2000);
            if (GhostShapes.Contains(ghost))
            {
                GhostShapes.Remove(ghost);
                RaiseRequestRedraw();
            }
        }
    }

    public void ProcessIncomingMessage(string json)
    {
        NetworkMessage? msg = CanvasSerializer.DeserializeNetworkMessage(json);
        if (msg == null)
        {
            return;
        }

        // --- HANDLE RESTORE ---
        if (msg.MessageType == NetworkMessageType.RESTORE)
        {
            Console.WriteLine("[Client] Received RESTORE command.");
            if (!string.IsNullOrEmpty(msg.Payload))
            {
                ApplyRestore(msg.Payload);
            }
            return;
        }
        // ---------------------

        CanvasAction action = msg.Action;
        if (action == null)
        {
            return; // Safety
        }

        bool isMyAction = false;

        if (msg.MessageType == NetworkMessageType.NORMAL)
        {
            isMyAction = action.NewShape?.LastModifiedBy == CurrentUserId;
        }
        else
        {
            if (msg.MessageType == NetworkMessageType.UNDO)
            {
                CanvasAction? localUndo = _stateManager.PeekUndo();
                if (localUndo != null && localUndo.ActionId == action.ActionId)
                {
                    isMyAction = true;
                }
            }
            else if (msg.MessageType == NetworkMessageType.REDO)
            {
                CanvasAction? localRedo = _stateManager.PeekRedo();
                if (localRedo != null && localRedo.ActionId == action.ActionId)
                {
                    isMyAction = true;
                }
            }
        }

        switch (msg.MessageType)
        {
            case NetworkMessageType.NORMAL: HandleNormalMessage(action, isMyAction); break;
            case NetworkMessageType.UNDO: HandleUndoMessage(action, isMyAction); break;
            case NetworkMessageType.REDO: HandleRedoMessage(action, isMyAction); break;
        }
    }

    private void HandleNormalMessage(CanvasAction action, bool isMyAction)
    {
        if (isMyAction)
        {
            ApplyActionLocally(action);
            IShape? ghost = GhostShapes.FirstOrDefault(g => g.ShapeId == action.NewShape?.ShapeId);
            if (ghost != null)
            {
                GhostShapes.Remove(ghost);
                RaiseRequestRedraw();
            }
        }
        else
        {
            if (action.NewShape != null)
            {
                // KEY FIX: Use helper to ensure SelectedShape isn't stale
                UpdateShapeFromNetwork(action.NewShape);
                RaiseRequestRedraw();
            }
        }
    }

    private void HandleUndoMessage(CanvasAction action, bool isMyAction)
    {
        if (isMyAction)
        {
            _stateManager.Undo();
        }
        if (action.NewShape != null)
        {
            // KEY FIX
            UpdateShapeFromNetwork(action.NewShape);
        }
        RaiseRequestRedraw();
    }

    private void HandleRedoMessage(CanvasAction action, bool isMyAction)
    {
        if (isMyAction)
        {
            _stateManager.Redo();
        }
        if (action.NewShape != null)
        {
            // KEY FIX
            UpdateShapeFromNetwork(action.NewShape);
        }
        RaiseRequestRedraw();
    }
}
