using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Win32; // For SaveFileDialog
using CanvasDataModel;
using System.IO; // Still keep this

namespace ViewModel;

public class CanvasViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // --- NEW PROPERTY: IsHost ---
    // Used to toggle UI elements visibility
    public virtual bool IsHost => false;
    // ----------------------------

    // --- EVENT FOR MANUAL REDRAW (Ghost Shapes) ---
    public event Action? RequestRedraw;
    protected void RaiseRequestRedraw()
    {
        RequestRedraw?.Invoke();
    }

    public enum DrawingMode { Select, FreeHand, StraightLine, Rectangle, EllipseShape, TriangleShape }
    private DrawingMode _currentMode = DrawingMode.FreeHand;
    public DrawingMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged();
                if (_currentMode != DrawingMode.Select)
                {
                    SelectedShape = null;
                }
            }
        }
    }

    public List<IShape> GhostShapes { get; } = new();

    private List<Point> _trackedPoints = new();
    public bool _isTracking = false;
    private bool _isMovingShape = false;
    public bool IsMovingShape => _isMovingShape;
    private Point _moveStartPoint;
    private IShape? _originalShapeForMove;
    public Rectangle CanvasBounds { get; set; }

    public Dictionary<string, IShape> _shapes = new();
    protected readonly StateManager _stateManager = new();
    public IShape? _originalShapeForUndo = null;

    private Color _currentColor = Color.Black;
    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (_currentColor == value) { return; }
            _currentColor = value;
            OnPropertyChanged();

            if (SelectedShape != null && SelectedShape.Color != value)
            {
                _originalShapeForUndo ??= SelectedShape;
                IShape newShape = SelectedShape.WithUpdates(value, null, CurrentUserId);
                _selectedShape = newShape;
                OnPropertyChanged(nameof(SelectedShape));
            }
        }
    }

    public string CurrentUserId { get; set; } = "user_default";

    private double _currentThickness = 2.0;
    public double CurrentThickness
    {
        get => _currentThickness;
        set
        {
            if (_currentThickness == value) { return; }
            _currentThickness = value;
            OnPropertyChanged();

            if (SelectedShape != null && SelectedShape.Thickness != value)
            {
                _originalShapeForUndo ??= SelectedShape;
                IShape newShape = SelectedShape.WithUpdates(null, value, CurrentUserId);
                _selectedShape = newShape;
                OnPropertyChanged(nameof(SelectedShape));
            }
        }
    }

    protected IShape? _selectedShape;
    public IShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                if (_selectedShape != null)
                {
                    CommitModification();
                }
                _selectedShape = value;
                OnPropertyChanged();

                if (_selectedShape != null)
                {
                    if (_currentColor != _selectedShape.Color)
                    {
                        _currentColor = _selectedShape.Color;
                        OnPropertyChanged(nameof(CurrentColor));
                    }
                    if (_currentThickness != _selectedShape.Thickness)
                    {
                        _currentThickness = _selectedShape.Thickness;
                        OnPropertyChanged(nameof(CurrentThickness));
                    }
                }
            }
        }
    }

    public IShape? LastCreatedShape { get; private set; }

    /// <summary>
    /// Updates the local dictionary AND ensures SelectedShape points to the new instance.
    /// This prevents "Version Mismatch" errors where the UI holds a stale object reference.
    /// </summary>
    protected void UpdateShapeFromNetwork(IShape shape)
    {
        // 1. Update Dictionary
        _shapes[shape.ShapeId] = shape;

        // 2. Sync Selection (Crucial for concurrency fix)
        if (_selectedShape != null && _selectedShape.ShapeId == shape.ShapeId)
        {
            // Directly update backing field to avoid triggering recursion via property setter
            _selectedShape = shape;
            OnPropertyChanged(nameof(SelectedShape));

            // Reset undo buffer since the baseline has changed
            _originalShapeForUndo = null;

            // --- FIX: SYNC UI PROPERTIES ---
            // Since we bypassed the setter, we must manually sync the UI properties
            // so the slider/color picker reflect the update from the network.
            if (_currentThickness != shape.Thickness)
            {
                _currentThickness = shape.Thickness;
                OnPropertyChanged(nameof(CurrentThickness));
            }
            if (_currentColor != shape.Color)
            {
                _currentColor = shape.Color;
                OnPropertyChanged(nameof(CurrentColor));
            }
            // -------------------------------
        }
    }

    protected virtual void ProcessAction(CanvasAction action)
    {
        ApplyActionLocally(action);
    }

    public virtual void CommitModification()
    {
        if (_originalShapeForUndo != null && SelectedShape != null &&
            _originalShapeForUndo.ShapeId == SelectedShape.ShapeId)
        {
            if (_originalShapeForUndo.Color != SelectedShape.Color ||
                _originalShapeForUndo.Thickness != SelectedShape.Thickness)
            {
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForUndo, SelectedShape);
                ProcessAction(action);
            }
        }
        _originalShapeForUndo = null;
    }

    public virtual void DeleteSelectedShape()
    {
        CommitModification();
        if (SelectedShape == null) { return; }

        IShape deletedShape = SelectedShape.WithDelete(CurrentUserId);

        var deleteAction = new CanvasAction(
            CanvasActionType.Delete,
            SelectedShape,
            deletedShape
        );

        ProcessAction(deleteAction);
        SelectedShape = null;
    }

    public virtual void Undo()
    {
        CommitModification();
        SelectedShape = null;
        CanvasAction? undoneAction = _stateManager.Undo();
        if (undoneAction != null)
        {
            SyncDictionaryFromAction(undoneAction, true);
            RaiseRequestRedraw();
        }
    }

    public virtual void Redo()
    {
        SelectedShape = null;
        CanvasAction? redoneAction = _stateManager.Redo();
        if (redoneAction != null)
        {
            SyncDictionaryFromAction(redoneAction, false);
            RaiseRequestRedraw();
        }
    }

    protected void SyncDictionaryFromAction(CanvasAction action, bool isUndo)
    {
        IShape? shapeToApply = isUndo ? action.PrevShape : action.NewShape;

        if (action.ActionType == CanvasActionType.Create)
        {
            if (isUndo)
            {
                if (action.NewShape != null)
                    UpdateShapeFromNetwork(action.NewShape.WithDelete("system"));
            }
            else
            {
                if (action.NewShape != null)
                    UpdateShapeFromNetwork(action.NewShape);
            }
        }
        else if (shapeToApply != null)
        {
            UpdateShapeFromNetwork(shapeToApply);
        }
    }

    protected void ApplyActionLocally(CanvasAction action)
    {
        _stateManager.AddAction(action);

        if (action.NewShape != null)
        {
            UpdateShapeFromNetwork(action.NewShape);
        }
        RaiseRequestRedraw();
    }

    // --- INTERACTION LOGIC ---

    public void SelectShapeAt(Point point)
    {
        CommitModification();
        SelectedShape = null;
        if (_shapes == null) { return; }
        foreach (IShape shape in _shapes.Values.Reverse())
        {
            if (!shape.IsDeleted && shape.IsHit(point))
            {
                SelectedShape = shape;
                return;
            }
        }
    }

    public void StartTracking(Point point)
    {
        CommitModification();
        LastCreatedShape = null;

        if (CurrentMode == DrawingMode.Select)
        {
            _isTracking = false;
            SelectShapeAt(point);

            if (SelectedShape != null && SelectedShape.IsHit(point))
            {
                _isMovingShape = true;
                _moveStartPoint = point;
                _originalShapeForMove = SelectedShape;
            }
            else
            {
                _isMovingShape = false;
            }
            return;
        }

        _isTracking = true;
        _isMovingShape = false;
        SelectedShape = null;

        if (CurrentMode == DrawingMode.FreeHand)
        {
            _trackedPoints.Clear();
            _trackedPoints.Add(point);
        }
        else if (CurrentMode != DrawingMode.Select)
        {
            _trackedPoints.Clear();
            _trackedPoints.Add(point);
            _trackedPoints.Add(point);
        }
    }

    public void TrackPoint(Point point)
    {
        if (_isMovingShape && SelectedShape != null && _originalShapeForMove != null)
        {
            Point offset = new Point(point.X - _moveStartPoint.X, point.Y - _moveStartPoint.Y);
            IShape movedShape = _originalShapeForMove.WithMove(offset, CanvasBounds, CurrentUserId);

            // Visual update only - Use UpdateShapeFromNetwork for consistency (even though it's local)
            UpdateShapeFromNetwork(movedShape);

            RaiseRequestRedraw();
        }
        else if (_isTracking && _trackedPoints.Count > 0)
        {
            if (CurrentMode == DrawingMode.FreeHand) _trackedPoints.Add(point);
            else _trackedPoints[1] = point;
        }
    }

    public void StopTracking()
    {
        if (_isMovingShape)
        {
            _isMovingShape = false;
            if (_originalShapeForMove != null && SelectedShape != null &&
                _originalShapeForMove.ShapeId == SelectedShape.ShapeId &&
                !_originalShapeForMove.Points.SequenceEqual(SelectedShape.Points))
            {
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForMove, SelectedShape);
                ProcessAction(action);
            }
            _originalShapeForMove = null;
            return;
        }

        if (CurrentMode == DrawingMode.Select || !_isTracking) { _isTracking = false; return; }

        _isTracking = false;
        if (_trackedPoints.Count == 0) return;

        IShape? newShape = null;
        if (CurrentMode == DrawingMode.FreeHand && _trackedPoints.Count >= 2)
            newShape = new FreeHand(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        else if (CurrentMode == DrawingMode.StraightLine && _trackedPoints.Count >= 2)
            newShape = new StraightLine(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        else if (CurrentMode == DrawingMode.Rectangle && _trackedPoints.Count >= 2)
            newShape = new RectangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        else if (CurrentMode == DrawingMode.EllipseShape && _trackedPoints.Count >= 2)
            newShape = new EllipseShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        else if (CurrentMode == DrawingMode.TriangleShape && _trackedPoints.Count >= 2)
            newShape = new TriangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);

        if (newShape != null)
        {
            var action = new CanvasAction(CanvasActionType.Create, null, newShape);
            ProcessAction(action);
            LastCreatedShape = newShape;
        }
    }

    public IShape? CurrentPreviewShape
    {
        get
        {
            if (!_isTracking || _trackedPoints.Count < 2 || CurrentMode == DrawingMode.Select) return null;

            switch (CurrentMode)
            {
                case DrawingMode.FreeHand: return new FreeHand(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.StraightLine: return new StraightLine(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.Rectangle: return new RectangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.EllipseShape: return new EllipseShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.TriangleShape: return new TriangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                default: return null;
            }
        }
    }

    protected CanvasAction GetInverseAction(CanvasAction original, string userId)
    {
        switch (original.ActionType)
        {
            case CanvasActionType.Create:
                IShape? shapeToDelete = original.NewShape;
                IShape? deletedShape = shapeToDelete?.WithDelete(userId);
                return new CanvasAction(original.ActionId, CanvasActionType.Delete, shapeToDelete, deletedShape);

            case CanvasActionType.Delete:
                return new CanvasAction(original.ActionId, CanvasActionType.Resurrect, original.NewShape, original.PrevShape);

            case CanvasActionType.Modify:
                return new CanvasAction(original.ActionId, CanvasActionType.Modify, original.NewShape, original.PrevShape);

            case CanvasActionType.Resurrect:
                IShape? shapeToKill = original.NewShape;
                IShape? killedShape = shapeToKill?.WithDelete(userId);
                return new CanvasAction(original.ActionId, CanvasActionType.Delete, original.NewShape, killedShape);

            default:
                return original;
        }
    }

    // --- SAVE/LOAD LOGIC ---
    public void SaveShapes()
    {
        if (_shapes == null) return;
        SaveFileDialog saveDialog = new SaveFileDialog
        {
            Filter = "Canvas JSON (*.json)|*.json",
            FileName = "canvas_shapes.json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                string json = CanvasDataModelSerializer.SerializeShapesDictionary(_shapes);
                File.WriteAllText(saveDialog.FileName, json);
                Console.WriteLine($"[Host] Shapes saved to {saveDialog.FileName}");
            }
            catch (Exception ex) { Console.WriteLine($"[Host] Save failed: {ex.Message}"); }
        }
    }

    // Called by Client when receiving RESTORE, or Host when loading
    public void ApplyRestore(string jsonDictionary)
    {
        try
        {
            var loadedShapes = CanvasDataModelSerializer.DeserializeShapesDictionary(jsonDictionary);
            if (loadedShapes != null)
            {
                // 1. Replace Dictionary
                _shapes = loadedShapes;

                // 2. Clear Selection & Undo Stack
                SelectedShape = null;
                _stateManager.ImportState(new SerializedActionStack()); // Clear stack (or use a Reset method)

                // 3. Redraw
                RaiseRequestRedraw();
                Console.WriteLine("[Canvas] State Restored.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Canvas] Restore failed: {ex.Message}");
        }
    }
}
