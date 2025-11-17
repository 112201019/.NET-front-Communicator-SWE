using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CanvasDataModel;
using static System.Collections.Specialized.BitVector32;

namespace ViewModel;

public class CanvasViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    private List<Point> _trackedPoints = new();
    public bool _isTracking = false;
    private bool _isMovingShape = false;
    public bool IsMovingShape => _isMovingShape;
    private Point _moveStartPoint;
    private IShape? _originalShapeForMove;
    public Rectangle CanvasBounds { get; set; }

    public Dictionary<string, IShape> _shapes = new();

    private readonly StateManager _stateManager = new();
    private IShape? _originalShapeForUndo = null;

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
                _shapes[newShape.ShapeId] = newShape;
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
                _shapes[newShape.ShapeId] = newShape;
                _selectedShape = newShape;
                OnPropertyChanged(nameof(SelectedShape));
            }
        }
    }

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

    public void CommitModification()
    {
        if (_originalShapeForUndo != null && SelectedShape != null &&
            _originalShapeForUndo.ShapeId == SelectedShape.ShapeId)
        {
            if (_originalShapeForUndo.Color != SelectedShape.Color ||
                _originalShapeForUndo.Thickness != SelectedShape.Thickness)
            {
                var action = new CanvasAction(CanvasActionType.Modify, _originalShapeForUndo, SelectedShape);
                _stateManager.AddAction(action);
                TestSerializeAction(action); // <-- TEST ADDED
            }
        }
        _originalShapeForUndo = null;
    }

    public void SelectShapeAt(Point point)
    {
        CommitModification();
        SelectedShape = null;

        // --- BUG FIX: Check if _shapes is null before accessing ---
        if (_shapes == null) { return; }
        // --- END BUG FIX ---

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
        else if (CurrentMode == DrawingMode.StraightLine || CurrentMode == DrawingMode.Rectangle || CurrentMode == DrawingMode.EllipseShape || CurrentMode == DrawingMode.TriangleShape)
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
                _stateManager.AddAction(action);
                TestSerializeAction(action); // <-- TEST ADDED
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
            var freehand = new FreeHand(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            newShape = freehand;
        }
        else if (CurrentMode == DrawingMode.StraightLine)
        {
            if (_trackedPoints.Count < 2) { return; }
            var line = new StraightLine(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            newShape = line;
        }
        else if (CurrentMode == DrawingMode.Rectangle)
        {
            if (_trackedPoints.Count < 2) { return; }
            var rectangle = new RectangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            newShape = rectangle;
        }
        else if (CurrentMode == DrawingMode.EllipseShape)
        {
            if (_trackedPoints.Count < 2) { return; }
            var ellipse = new EllipseShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            newShape = ellipse;
        }
        else if (CurrentMode == DrawingMode.TriangleShape)
        {
            if (_trackedPoints.Count < 2) { return; }
            var triangle = new TriangleShape(_trackedPoints, CurrentColor, CurrentThickness, CurrentUserId);
            newShape = triangle;
        }

        if (newShape != null)
        {
            _shapes.Add(newShape.ShapeId, newShape);

            var action = new CanvasAction(CanvasActionType.Create, null, newShape);
            _stateManager.AddAction(action);

            TestSerializeAction(action);


            LastCreatedShape = newShape;
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

    // ---
    // --- NEW SEPARATE FUNCTIONS (as requested) ---
    // ---

    /// <summary>
    /// Reusable function to delete a shape.
    /// This creates the deleted-state shape, updates the dictionary,
    /// and adds the specific DELETE action to the undo stack.
    /// </summary>
    private void DeleteShape(IShape shapeToDelete, string userId)
    {
        // 1. Create a new version of the shape with IsDeleted = true
        IShape deletedShape = shapeToDelete.WithDelete(userId);

        // 2. Update the shape in the dictionary to its new "deleted" state
        _shapes[shapeToDelete.ShapeId] = deletedShape;

        // 3. A deletion is now a specific DELETE action.
        var deleteAction = new CanvasAction(
            CanvasActionType.Delete,
            shapeToDelete,           // PrevShape is the shape *before* deletion
            deletedShape             // NewShape is the shape *after* deletion
        );
        _stateManager.AddAction(deleteAction);
    }

    /// <summary>
    /// Reusable function to resurrect a shape.
    /// This creates the non-deleted-state shape, updates the dictionary,
    // and adds the specific RESURRECT action to the undo stack.
    /// </summary>
    private void ResurrectShape(IShape shapeToResurrect, string userId)
    {
        // 1. Create a new version of the shape with IsDeleted = false
        IShape resurrectedShape = shapeToResurrect.WithResurrect(userId);

        // 2. Update the shape in the dictionary
        _shapes[shapeToResurrect.ShapeId] = resurrectedShape;

        // 3. Create the RESURRECT action
        var resurrectAction = new CanvasAction(
            CanvasActionType.Resurrect,
            shapeToResurrect,   // PrevShape is the shape *before* resurrection
            resurrectedShape    // NewShape is the shape *after* resurrection
        );
        _stateManager.AddAction(resurrectAction);
    }
    // ---
    // --- END NEW FUNCTIONS ---
    // ---

    /// <summary>
    /// Deletes the currently selected shape.
    /// </summary>
    public void DeleteSelectedShape()
    {
        CommitModification();
        if (SelectedShape == null) { return; }

        // --- MODIFIED: Uses the new separate function ---
        DeleteShape(SelectedShape, CurrentUserId);

        // 4. Deselect the shape
        SelectedShape = null;
        // ---
    }

    public void Undo()
    {
        CommitModification();
        SelectedShape = null;
        CanvasAction? undoneAction = _stateManager.Undo();

        if (undoneAction != null)
        {
            // ---
            // --- UPDATED SYMMETRIC LOGIC ---
            // ---
            switch (undoneAction.ActionType)
            {
                case CanvasActionType.Create:
                    // To UNDO a Create, we restore the state *before* it existed.
                    // (PrevShape is null, NewShape is the one created)
                    if (undoneAction.NewShape != null)
                    {
                        // We just mark it as deleted.
                        IShape deletedShape = undoneAction.NewShape.WithDelete("system_undo");
                        _shapes[deletedShape.ShapeId] = deletedShape;
                    }
                    break;

                case CanvasActionType.Modify:
                    // To UNDO a Modify, we restore the PrevShape.
                    if (undoneAction.PrevShape != null)
                    {
                        _shapes[undoneAction.PrevShape.ShapeId] = undoneAction.PrevShape;
                        if (!undoneAction.PrevShape.IsDeleted)
                        {
                            SelectedShape = undoneAction.PrevShape;
                        }
                    }
                    break;

                case CanvasActionType.Delete:
                    // To UNDO a Delete, we RESURRECT the shape.
                    // We restore the PrevShape, which has IsDeleted = false.
                    if (undoneAction.PrevShape != null)
                    {
                        _shapes[undoneAction.PrevShape.ShapeId] = undoneAction.PrevShape;
                        SelectedShape = undoneAction.PrevShape;
                    }
                    break;

                case CanvasActionType.Resurrect:
                    // To UNDO a Resurrect, we DELETE the shape again.
                    // We restore the PrevShape, which has IsDeleted = true.
                    if (undoneAction.PrevShape != null)
                    {
                        _shapes[undoneAction.PrevShape.ShapeId] = undoneAction.PrevShape;
                        // Do not select it
                    }
                    break;
            }
            // ---
            // ---
        }
    }

    public void Redo()
    {
        SelectedShape = null;
        CanvasAction? redoneAction = _stateManager.Redo();

        if (redoneAction != null)
        {
            // ---
            // --- UPDATED SYMMETRIC LOGIC ---
            // ---
            switch (redoneAction.ActionType)
            {
                case CanvasActionType.Create:
                    // To REDO a Create, we restore the NewShape.
                    if (redoneAction.NewShape != null)
                    {
                        _shapes[redoneAction.NewShape.ShapeId] = redoneAction.NewShape;
                        SelectedShape = redoneAction.NewShape;
                    }
                    break;

                case CanvasActionType.Modify:
                    // To REDO a Modify, we restore the NewShape.
                    if (redoneAction.NewShape != null)
                    {
                        _shapes[redoneAction.NewShape.ShapeId] = redoneAction.NewShape;
                        if (!redoneAction.NewShape.IsDeleted)
                        {
                            SelectedShape = redoneAction.NewShape;
                        }
                    }
                    break;

                case CanvasActionType.Delete:
                    // To REDO a Delete, we restore the NewShape (which has IsDeleted = true).
                    if (redoneAction.NewShape != null)
                    {
                        _shapes[redoneAction.NewShape.ShapeId] = redoneAction.NewShape;
                        // Do not select it
                    }
                    break;

                case CanvasActionType.Resurrect:
                    // To REDO a Resurrect, we restore the NewShape (which has IsDeleted = false).
                    if (redoneAction.NewShape != null)
                    {
                        _shapes[redoneAction.NewShape.ShapeId] = redoneAction.NewShape;
                        SelectedShape = redoneAction.NewShape;
                    }
                    break;
            }
            // ---
            // ---
        }
    }

    /// <summary>
    /// Saves the current shapes dictionary to a file. (Bound to 'T')
    /// </summary>
    public void AddTestShape()
    {
        // --- BUG FIX: Add null check ---
        if (_shapes == null)
        {
            Console.WriteLine("--- !!! CANVAS SAVE FAILED: Shapes dictionary is null. ---");
            return;
        }
        // --- END BUG FIX ---

        Console.WriteLine("--- SAVING SHAPES DICTIONARY... ---");
        try
        {
            string shapesJson = CanvasDataModelSerializer.SerializeShapesDictionary(_shapes);
            string filePath = "canvas_save.txt";
            File.WriteAllText(filePath, shapesJson);
            Console.WriteLine($"--- CANVAS (shapes only) SAVED SUCCESSFULLY to {Path.GetFullPath(filePath)} ---");
        }
        catch (Exception ex)
        {
            Console.WriteLine("--- !!! CANVAS SAVE FAILED !!! ---");
            Console.WriteLine(ex.ToString());
        }
    }

    /// <summary>
    /// Loads the shapes dictionary from a file. (Bound to 'N')
    /// </summary>
    public void LoadShapesDictionary()
    {
        string filePath = "canvas_save.txt";
        Console.WriteLine($"--- LOADING SHAPES DICTIONARY from {filePath}... ---");

        if (!File.Exists(filePath))
        {
            Console.WriteLine("--- !!! LOAD FAILED: File not found. ---");
            return;
        }

        try
        {
            string shapesJson = File.ReadAllText(filePath);
            Dictionary<string, IShape> loadedShapes = CanvasDataModelSerializer.DeserializeShapesDictionary(shapesJson);

            // ---
            // --- BUG FIX: Never set _shapes to null ---
            // ---
            if (loadedShapes != null)
            {
                _shapes = loadedShapes;
                Console.WriteLine($"--- CANVAS (shapes only) LOADED SUCCESSFULLY. Found {loadedShapes.Count} shapes. ---");
            }
            else
            {
                // If deserialization fails, reset to an EMPTY dictionary, not null.
                _shapes = new Dictionary<string, IShape>();
                Console.WriteLine("--- !!! LOAD FAILED: Deserialization returned null. Resetting to empty canvas. ---");
            }
            // --- END BUG FIX ---

            SelectedShape = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("--- !!! CANVAS LOAD FAILED !!! ---");
            Console.WriteLine(ex.ToString());

            // Safety: ensure _shapes is not null even if an exception occurs
            _shapes ??= new Dictionary<string, IShape>();
        }
    }
    // ---
    // --- NEW TEST FUNCTION ---
    // ---
    /// <summary>
    /// Tests the serialization and deserialization of a CanvasAction.
    /// </summary>
    private void TestSerializeAction(CanvasAction action)
    {
        Console.WriteLine("\n--- ACTION CREATED: TESTING SERIALIZATION ---");
        Console.WriteLine($"--- Action Type: {action.ActionType}, Action ID: {action.ActionId} ---");
        try
        {
            // 1. Serialize
            string actionJson = CanvasDataModelSerializer.SerializeActionManual(action);
            Console.WriteLine(actionJson);

            //// 2. Deserialize
            //CanvasAction? deserializedAction = CanvasDataModelSerializer.DeserializeActionManual(actionJson);

            //// 3. Check and Verify
            //if (deserializedAction == null)
            //{
            //    Console.WriteLine("--- !!! TEST FAILED: Deserialization returned null. !!! ---");
            //    return;
            //}

            //bool idMatch = deserializedAction.ActionId == action.ActionId;
            //bool typeMatch = deserializedAction.ActionType == action.ActionType;
            //bool prevShapeMatch = (deserializedAction.PrevShape?.ShapeId) == (action.PrevShape?.ShapeId);
            //bool newShapeMatch = (deserializedAction.NewShape?.ShapeId) == (action.NewShape?.ShapeId);

            //Console.WriteLine("--- VERIFICATION RESULTS ---");
            //Console.WriteLine($"ActionId Match: {idMatch}");
            //Console.WriteLine($"ActionType Match: {typeMatch}");
            //Console.WriteLine($"PrevShape ID Match: {prevShapeMatch}");
            //Console.WriteLine($"NewShape ID Match: {newShapeMatch}");

            //if (idMatch && typeMatch && prevShapeMatch && newShapeMatch)
            //{
            //    Console.WriteLine("--- TEST SUCCEEDED: Action round-trip successful. ---");
            //}
            //else
            //{
            //    Console.WriteLine("--- !!! TEST FAILED: Property mismatch after deserialization. !!! ---");
            //}
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--- !!! TEST FAILED: {ex.Message} !!! ---");
        }
        Console.WriteLine("--------------------------------------------------\n");
    }
    // ---
    // --- END NEW TEST FUNCTION ---
    // ---
}
