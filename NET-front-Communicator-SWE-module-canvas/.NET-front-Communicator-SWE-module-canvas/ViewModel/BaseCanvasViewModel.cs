using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using CanvasDataModel;

namespace ViewModel;

/// <summary>
/// An abstract base class containing all common UI properties
/// and local user input logic (drawing, moving) for both
/// the Host and Client ViewModels.
/// </summary>
public abstract class BaseCanvasViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum DrawingMode { Select, FreeHand, StraightLine, Rectangle, EllipseShape, TriangleShape }

    // --- UI Properties (shared by Host & Client) ---

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

    private Color _currentColor = Color.Black;
    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (_currentColor == value) { return; }
            _currentColor = value;
            OnPropertyChanged();

            // This is now a "provisional" modification
            if (SelectedShape != null && SelectedShape.Color != value)
            {
                _originalShapeForUndo ??= SelectedShape;
                // Create the modified shape locally for UI feedback
                IShape newShape = SelectedShape.WithUpdates(value, null, CurrentUserId);
                // Update the *local* dictionary
                _shapes[newShape.ShapeId] = newShape;
                _selectedShape = newShape;
                OnPropertyChanged(nameof(SelectedShape));
            }
        }
    }

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
                _shapes[newShape.ShapeId] = newShape;
                _selectedShape = newShape;
                OnPropertyChanged(nameof(SelectedShape));
            }
        }
    }

    // --- State Properties (managed by child classes) ---

    public abstract string CurrentUserId { get; }
    public Dictionary<string, IShape> _shapes { get; set; } = new();

    private IShape? _selectedShape;
    public IShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                if (_selectedShape != null)
                {
                    // When deselecting, commit any pending modification
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

    // --- Ghost Shape for Client UI ---
    private IShape? _ghostShape;
    public IShape? GhostShape
    {
        get => _ghostShape;
        protected set
        {
            _ghostShape = value;
            OnPropertyChanged();
        }
    }

    // --- Local Input Tracking (shared) ---
    protected List<Point> _trackedPoints = new();
    public bool _isTracking = false;
    protected bool _isMovingShape = false;
    public bool IsMovingShape => _isMovingShape;
    protected Point _moveStartPoint;
    protected IShape? _originalShapeForMove;
    protected IShape? _originalShapeForUndo = null;
    public Rectangle CanvasBounds { get; set; }


    // --- Abstract Methods (to be implemented by Host/Client) ---

    /// <summary>
    /// Called when the user finishes drawing/moving/modifying a shape.
    /// </summary>
    protected abstract void OnActionFinalized(CanvasAction action, MessageType msgType);

    /// <summary>
    /// Called when the user requests an Undo.
    /// </summary>
    public abstract void OnUndoRequested();

    /// <summary>
    /// Called when the user requests a Redo.
    /// </summary>
    public abstract void OnRedoRequested();

    /// <summary>
    /// Processes an incoming message from the network.
    /// </summary>
    public abstract void ProcessIncomingMessage(NetworkMessage message);


    // --- Common Local Input Logic ---

    public void CommitModification()
    {
        if (_originalShapeForUndo != null && SelectedShape != null &&
            _originalShapeForUndo.ShapeId == SelectedShape.ShapeId)
        {
            if (_originalShapeForUndo.Color != SelectedShape.Color ||
                _originalShapeForUndo.Thickness != SelectedShape.Thickness)
            {
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForUndo, SelectedShape);
                OnActionFinalized(action, MessageType.NORMAL);
            }
        }
        _originalShapeForUndo = null;
    }

    public void SelectShapeAt(Point point)
    {
        CommitModification();
        SelectedShape = null;
        if (_shapes == null) return;

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
        GhostShape = null; // Clear any old ghost

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
        else if (CurrentMode == DrawingMode.StraightLine || CurrentMode == DrawingMode.Rectangle || CurrentMode == DrawingMode.EllipseShape || CurrentMode == DrawingMode.TriangleShape)
        {
            _trackedPoints.Clear();
            _trackedPoints.Add(point);
            _trackedPoints.Add(point);
        }
    }

    public void TrackPoint(Point point)
    {
        // This logic is now provisional. It modifies the *local*
        // dictionary for immediate UI feedback.
        if (_isMovingShape && SelectedShape != null && _originalShapeForMove != null)
        {
            Point offset = new Point(point.X - _moveStartPoint.X, point.Y - _moveStartPoint.Y);
            IShape movedShape = _originalShapeForMove.WithMove(offset, CanvasBounds, CurrentUserId);
            _shapes[movedShape.ShapeId] = movedShape;
            _selectedShape = movedShape;
            OnPropertyChanged(nameof(SelectedShape));
        }
        else if (_isTracking && _trackedPoints.Count > 0)
        {
            if (CurrentMode == DrawingMode.FreeHand)
            {
                _trackedPoints.Add(point);
            }
            else if (CurrentMode == DrawingMode.StraightLine || CurrentMode == DrawingMode.Rectangle || CurrentMode == DrawingMode.EllipseShape || CurrentMode == DrawingMode.TriangleShape)
            {
                _trackedPoints[1] = point;
            }
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
                OnActionFinalized(action, MessageType.NORMAL);
            }
            _originalShapeForMove = null;
            return;
        }

        if (CurrentMode == DrawingMode.Select || !_isTracking)
        {
            _isTracking = false;
            return;
        }

        _isTracking = false;
        if (_trackedPoints.Count == 0)
        {
            return;
        }
        IShape? newShape = null;

        if (CurrentMode == DrawingMode.FreeHand)
        {
            if (_trackedPoints.Count < 2) { return; }
            newShape = new FreeHand(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        }
        else if (CurrentMode == DrawingMode.StraightLine)
        {
            if (_trackedPoints.Count < 2) { return; }
            newShape = new StraightLine(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        }
        else if (CurrentMode == DrawingMode.Rectangle)
        {
            if (_trackedPoints.Count < 2) { return; }
            newShape = new RectangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        }
        else if (CurrentMode == DrawingMode.EllipseShape)
        {
            if (_trackedPoints.Count < 2) { return; }
            newShape = new EllipseShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        }
        else if (CurrentMode == DrawingMode.TriangleShape)
        {
            if (_trackedPoints.Count < 2) { return; }
            newShape = new TriangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
        }

        if (newShape != null)
        {
            var action = new CanvasAction(CanvasActionType.Create, null, newShape);
            OnActionFinalized(action, MessageType.NORMAL);
        }
    }

    public IShape? CurrentPreviewShape
    {
        get
        {
            if (!_isTracking || _trackedPoints.Count < 2 || CurrentMode == DrawingMode.Select)
            {
                return null;
            }
            switch (CurrentMode)
            {
                case DrawingMode.FreeHand:
                    return new FreeHand(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.StraightLine:
                    return new StraightLine(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.Rectangle:
                    return new RectangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.EllipseShape:
                    return new EllipseShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                case DrawingMode.TriangleShape:
                    return new TriangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
                default:
                    return null;
            }
        }
    }

    public void DeleteSelectedShape()
    {
        CommitModification();
        if (SelectedShape == null) { return; }

        IShape shapeToDelete = SelectedShape;
        IShape deletedShape = shapeToDelete.WithDelete(CurrentUserId);

        // This is a "provisional" delete
        _shapes[shapeToDelete.ShapeId] = deletedShape;
        SelectedShape = null;

        var deleteAction = new CanvasAction(CanvasActionType.Delete, shapeToDelete, deletedShape);
        OnActionFinalized(deleteAction, MessageType.NORMAL);
    }
}
