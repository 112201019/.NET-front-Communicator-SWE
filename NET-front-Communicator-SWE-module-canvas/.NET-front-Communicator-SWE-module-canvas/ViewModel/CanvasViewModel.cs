using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CanvasDataModel;
using Microsoft.Win32;

namespace ViewModel;

/// <summary>
/// The core logic for the Canvas application. 
/// Implements the **MVVM (Model-View-ViewModel) Pattern**.
/// Manages the state of shapes, handles user input logic, and coordinates with the StateManager.
/// Also acts as the **Observer** subject via INotifyPropertyChanged.
/// </summary>
public class CanvasViewModel : INotifyPropertyChanged
{
    // Implementation of INotifyPropertyChanged for data binding (Observer Pattern)
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // --- Properties ---

    /// <summary>
    /// Indicates if this instance is the Host (Server) or Client.
    /// </summary>
    public virtual bool IsHost => false;

    /// <summary>
    /// Event triggered when the visual canvas needs to be repainted.
    /// </summary>
    public event Action? RequestRedraw;
    protected void RaiseRequestRedraw()
    {
        RequestRedraw?.Invoke();
    }

    /// <summary>
    /// Enumeration for the current interaction tool selected by the user.
    /// </summary>
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
                // Deselect any shape when switching drawing tools to avoid accidental edits
                if (_currentMode != DrawingMode.Select)
                {
                    SelectedShape = null;
                }
            }
        }
    }

    // Collection of shapes meant to be displayed transiently (e.g., remote user drawing)
    public List<IShape> GhostShapes { get; } = new();

    // State for tracking mouse movement during drawing/dragging
    private List<Point> _trackedPoints = new();
    public bool _isTracking = false;
    private bool _isMovingShape = false;
    public bool IsMovingShape => _isMovingShape;
    private Point _moveStartPoint;
    private IShape? _originalShapeForMove;

    public Rectangle CanvasBounds { get; set; }
    public Dictionary<string, IShape> _shapes = new();
    protected readonly StateManager _stateManager = new();

    // Used to store the state of a shape before modification for Undo purposes
    public IShape? _originalShapeForUndo = null;

    public string CurrentUserId { get; set; } = "user_default";

    // --- Visual Properties ---

    private Color _currentColor = Color.Black;
    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (_currentColor == value)
            {
                return;
            }

            _currentColor = value;
            OnPropertyChanged();

            // Immediate Feedback: Update selected shape if one exists
            if (SelectedShape != null && SelectedShape.Color != value)
            {
                _originalShapeForUndo ??= SelectedShape;
                // Prototype Pattern: Create a new instance with updated color
                IShape newShape = SelectedShape.WithUpdates(value, null, CurrentUserId);
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
            if (_currentThickness == value)
            {
                return;
            }

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
    /// <summary>
    /// The currently selected shape for editing.
    /// </summary>
    public IShape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                // If we were editing a previous shape, ensure changes are committed
                if (_selectedShape != null)
                {
                    CommitModification();
                }
                _selectedShape = value;
                OnPropertyChanged();

                // Sync UI controls to the newly selected shape's properties
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
    // --- NEW: Analysis Result Text ---
    private string _analysisResult = "Ready to analyze...";
    public string AnalysisResult
    {
        get => _analysisResult;
        set
        {
            _analysisResult = value;
            OnPropertyChanged();
        }
    }
    // --- Core Logic ---

    /// <summary>
    /// Updates the local dictionary with a new/modified shape.
    /// Also handles synchronization if the updated shape was currently selected.
    /// </summary>
    protected void UpdateShapeFromNetwork(IShape shape)
    {
        // 1. Update Dictionary (Single Source of Truth)
        _shapes[shape.ShapeId] = shape;

        // 2. Sync Selection
        // If the shape updated from the network is the one the user has selected,
        // we must update the reference to prevent "stale object" issues.
        if (_selectedShape != null && _selectedShape.ShapeId == shape.ShapeId)
        {
            _selectedShape = shape;
            OnPropertyChanged(nameof(SelectedShape));
            _originalShapeForUndo = null; // Reset baseline

            // Sync UI sliders/pickers
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
        }
    }

    /// <summary>
    /// Template Method for processing actions. 
    /// Derived classes (Client/Host) override this to add networking logic.
    /// </summary>
    protected virtual void ProcessAction(CanvasAction action)
    {
        ApplyActionLocally(action);
    }

    /// <summary>
    /// Finalizes a modification (e.g., release mouse after drag, release slider).
    /// Creates a 'Modify' action for the Undo stack.
    /// </summary>
    public virtual void CommitModification()
    {
        if (_originalShapeForUndo != null && SelectedShape != null &&
            _originalShapeForUndo.ShapeId == SelectedShape.ShapeId)
        {
            // Check if actual changes occurred
            if (_originalShapeForUndo.Color != SelectedShape.Color ||
                _originalShapeForUndo.Thickness != SelectedShape.Thickness)
            {
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForUndo, SelectedShape);
                ProcessAction(action);
            }
        }
        _originalShapeForUndo = null;
    }

    /// <summary>
    /// Performs a soft delete on the selected shape.
    /// </summary>
    public virtual void DeleteSelectedShape()
    {
        CommitModification();
        if (SelectedShape == null)
        {
            return;
        }

        // Prototype Pattern: Create a deleted copy
        IShape deletedShape = SelectedShape.WithDelete(CurrentUserId);

        var deleteAction = new CanvasAction(
            CanvasActionType.Delete,
            SelectedShape,
            deletedShape
        );

        ProcessAction(deleteAction);
        SelectedShape = null;
    }

    // --- Interaction Logic ---

    /// <summary>
    /// Handles the completion of a mouse interaction (drawing or dropping).
    /// </summary>
    public void StopTracking()
    {
        // Case 1: Finishing a Move operation
        if (_isMovingShape)
        {
            _isMovingShape = false;
            if (_originalShapeForMove != null && SelectedShape != null &&
                _originalShapeForMove.ShapeId == SelectedShape.ShapeId &&
                !_originalShapeForMove.Points.SequenceEqual(SelectedShape.Points))
            {
                // Create Modify Action for the move
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForMove, SelectedShape);
                ProcessAction(action);
            }
            _originalShapeForMove = null;
            return;
        }

        // Case 2: Finishing a Drawing operation
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

        // --- REFACTORED: Use ShapeFactory ---
        // Map DrawingMode to ShapeType
        ShapeType? typeToCreate = CurrentMode switch
        {
            DrawingMode.FreeHand => ShapeType.FREEHAND,
            DrawingMode.StraightLine => ShapeType.LINE,
            DrawingMode.Rectangle => ShapeType.RECTANGLE,
            DrawingMode.EllipseShape => ShapeType.ELLIPSE,
            DrawingMode.TriangleShape => ShapeType.TRIANGLE,
            _ => null
        };

        if (typeToCreate.HasValue && _trackedPoints.Count >= 2)
        {
            // Factory Pattern creates the concrete instance
            IShape newShape = ShapeFactory.CreateShape(
                typeToCreate.Value,
                _trackedPoints,
                CurrentColor,
                CurrentThickness,
                CurrentUserId
            );

            var action = new CanvasAction(CanvasActionType.Create, null, newShape);
            ProcessAction(action);
            LastCreatedShape = newShape;
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
            return;
        }

        _isTracking = true;
        _isMovingShape = false;
        SelectedShape = null;

        _trackedPoints.Clear();
        _trackedPoints.Add(point);
        if (CurrentMode != DrawingMode.FreeHand)
        {
            _trackedPoints.Add(point); // Add duplicate for preview
        }
    }

    public void SelectShapeAt(Point point)
    {
        CommitModification();
        SelectedShape = null;
        if (_shapes == null) { return; }
        // Reverse iterate to select top-most shape first
        foreach (IShape shape in _shapes.Values.Reverse())
        {
            if (!shape.IsDeleted && shape.IsHit(point))
            {
                SelectedShape = shape;
                return;
            }
        }
    }

    public void TrackPoint(Point point)
    {
        if (_isMovingShape && SelectedShape != null && _originalShapeForMove != null)
        {
            Point offset = new Point(point.X - _moveStartPoint.X, point.Y - _moveStartPoint.Y);
            IShape movedShape = _originalShapeForMove.WithMove(offset, CanvasBounds, CurrentUserId);
            UpdateShapeFromNetwork(movedShape);
            RaiseRequestRedraw();
        }
        else if (_isTracking && _trackedPoints.Count > 0)
        {
            if (CurrentMode == DrawingMode.FreeHand)
            {
                _trackedPoints.Add(point);
            }
            else
            {
                _trackedPoints[1] = point;
            }
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

            ShapeType? type = CurrentMode switch
            {
                DrawingMode.FreeHand => ShapeType.FREEHAND,
                DrawingMode.StraightLine => ShapeType.LINE,
                DrawingMode.Rectangle => ShapeType.RECTANGLE,
                DrawingMode.EllipseShape => ShapeType.ELLIPSE,
                DrawingMode.TriangleShape => ShapeType.TRIANGLE,
                _ => null
            };

            if (type.HasValue)
            {
                // Factory Pattern
                return ShapeFactory.CreateShape(type.Value, _trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            }
            return null;
        }
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

    protected void ApplyActionLocally(CanvasAction action)
    {
        _stateManager.AddAction(action);
        if (action.NewShape != null)
        {
            UpdateShapeFromNetwork(action.NewShape);
        }
        RaiseRequestRedraw();
    }

    protected void SyncDictionaryFromAction(CanvasAction action, bool isUndo)
    {
        IShape? shapeToApply = isUndo ? action.PrevShape : action.NewShape;

        if (action.ActionType == CanvasActionType.Create)
        {
            if (isUndo)
            {
                // Undo Creation = Soft Delete
                if (action.NewShape != null)
                {
                    UpdateShapeFromNetwork(action.NewShape.WithDelete("system"));
                }
            }
            else
            {
                if (action.NewShape != null)
                {
                    UpdateShapeFromNetwork(action.NewShape);
                }
            }
        }
        else if (shapeToApply != null)
        {
            UpdateShapeFromNetwork(shapeToApply);
        }
    }
    // --- NEW: Regularize Feature ---
    public void RegularizeSelectedShape()
    {
        if (SelectedShape == null) return;
        CommitModification(); // Ensure any pending edits are saved first

        // 1. Serialize Current Shape
        string inputJson = CanvasDataModelSerializer.SerializeShapeManual(SelectedShape);

        // 2. Call the Black Box Function
        string outputJson = ProcessingService.RegularizeShape(inputJson);

        // 3. Deserialize
        IShape? regularizedShape = CanvasDataModelSerializer.DeserializeShapeManual(outputJson);

        if (regularizedShape != null)
        {
            // In case the mock didn't change anything, let's force a visual change (Prototype pattern)
            // to ensure the user sees something happening in this demo.
            regularizedShape = regularizedShape.WithUpdates(null, 5.0, CurrentUserId); // Enforce thickness

            // 4. Create Modification Action
            var action = new CanvasAction(CanvasActionType.Modify, SelectedShape, regularizedShape);

            // 5. Process
            ProcessAction(action);
            SelectedShape = regularizedShape; // Keep selected
        }
    }
    // -------------------------------

    // --- NEW: Analyze Feature ---
    public void PerformAnalysis(string imagePath)
    {
        AnalysisResult = "Analyzing...";

        // Call the Black Box Function
        string result = ProcessingService.AnalyzeCanvasImage(imagePath);

        AnalysisResult = result;
    }
    // ----------------------------
    // Helper to calculate inverse actions for network transmission
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

    public void SaveShapes()
    {
        if (_shapes == null)
        {
            return;
        }

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

    public void ApplyRestore(string jsonDictionary)
    {
        try
        {
            Dictionary<string, IShape> loadedShapes = CanvasDataModelSerializer.DeserializeShapesDictionary(jsonDictionary);
            if (loadedShapes != null)
            {
                _shapes = loadedShapes;
                SelectedShape = null;
                _stateManager.ImportState(new SerializedActionStack());
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
