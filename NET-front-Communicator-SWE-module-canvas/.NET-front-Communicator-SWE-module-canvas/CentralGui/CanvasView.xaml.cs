using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Communicator.Canvas;
using ViewModel;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using System;
using Drawing = System.Drawing;

namespace CentralGui;

public partial class CanvasView : UserControl
{
    private CanvasViewModel? _vm;
    private UIElement? _currentPreviewElement = null;
    private Rectangle? _selectionBox = null;
    
    // --- FIXED: Panning State ---
    private bool _isPanning = false;
    private Point _panLastPosition; // Tracks position relative to the static View, not the moving Canvas
    // ----------------------------

    private const double ZOOM_FACTOR = 1.1;
    private const double MAX_ZOOM = 5.0;
    private const double MIN_ZOOM = 0.5;

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        "Header", typeof(string), typeof(CanvasView), new PropertyMetadata("Canvas"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value);
    }

    public CanvasView()
    {
        InitializeComponent();
        HeaderLabel.Text = Header;
        this.Loaded += CanvasView_Loaded;

        ThicknessSlider.PreviewMouseLeftButtonUp += (s, e) =>
        {
            _vm?.CommitModification();
        };

        // Keep bounds updated on resize
        this.SizeChanged += (s, e) => 
        {
            if (_vm != null && CanvasBorder.ActualWidth > 0 && CanvasBorder.ActualHeight > 0)
            {
                _vm.CanvasBounds = new Drawing.Rectangle(0, 0, (int)CanvasBorder.ActualWidth, (int)CanvasBorder.ActualHeight);
            }
        };
    }

    private void CanvasView_Loaded(object sender, RoutedEventArgs e)
    {
        HeaderLabel.Text = Header;
        if (DataContext is CanvasViewModel vm)
        {
            _vm = vm;
            InitializeViewModelConnections();
            this.Focus();
        }
    }

    private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        this.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_vm == null) { return; }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            _vm.Undo();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
        {
            _vm.Redo();
            e.Handled = true;
        }
        else if ((e.Key == Key.Delete || e.Key == Key.D) && _vm.SelectedShape != null)
        {
            _vm.DeleteSelectedShape();
            e.Handled = true;
        }
        else if (e.Key == Key.T)
        {
            if (_vm.IsHost) { _vm.SaveShapes(); }
        }
        else if (e.Key == Key.S)
        {
            SaveCanvasSnapshot();
        }
    }

    private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        SaveCanvasSnapshot();
    }

    private void BtnRegularize_Click(object sender, RoutedEventArgs e)
    {
        _vm?.RegularizeSelectedShape();
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        string tempPath = System.IO.Path.GetTempFileName() + ".png";

        try
        {
            SaveCanvasToPath(tempPath);
            _vm.PerformAnalysis(tempPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to capture canvas for analysis: {ex.Message}");
        }
    }
    private void BtnCloseAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.IsAnalysisVisible = false;
    }
    private void SaveCanvasToPath(string filePath)
    {
        FrameworkElement elementToRender = CanvasBorder;

        // Force layout update if needed, though ActualWidth usually suffices
        if (elementToRender.ActualWidth == 0 || elementToRender.ActualHeight == 0) return;

        RenderTargetBitmap rtb = new RenderTargetBitmap(
            (int)elementToRender.ActualWidth,
            (int)elementToRender.ActualHeight,
            96d,
            96d,
            PixelFormats.Pbgra32
        );

        rtb.Render(elementToRender);

        PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
        pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

        using (FileStream fs = System.IO.File.OpenWrite(filePath))
        {
            pngEncoder.Save(fs);
        }
    }

    private void SaveCanvasSnapshot()
    {
        SaveFileDialog dialog = new SaveFileDialog
        {
            FileName = "screen shot",
            DefaultExt = ".png",
            Filter = "PNG Image (.png)|*.png"
        };

        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            try
            {
                SaveCanvasToPath(dialog.FileName);
                Console.WriteLine($"[GUI] Snapshot saved to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save snapshot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void InitializeViewModelConnections()
    {
        if (_vm == null) { return; }

        // Initialize bounds with ActualWidth/Height if available, else fall back to Width/Height
        int w = (int)(CanvasBorder.ActualWidth > 0 ? CanvasBorder.ActualWidth : CanvasBorder.Width);
        int h = (int)(CanvasBorder.ActualHeight > 0 ? CanvasBorder.ActualHeight : CanvasBorder.Height);
        
        _vm.CanvasBounds = new Drawing.Rectangle(0, 0, w, h);

        _vm.PropertyChanged += Vm_PropertyChanged;
        _vm.RequestRedraw += () => Dispatcher.Invoke(SyncCanvasState);

        CanvasBorder.MouseWheel += CanvasBorder_MouseWheel;
        CanvasBorder.MouseLeftButtonDown += CanvasBorder_MouseLeftButtonDown;
        CanvasBorder.MouseMove += CanvasBorder_MouseMove;
        CanvasBorder.MouseLeftButtonUp += CanvasBorder_MouseLeftButtonUp;
        CanvasBorder.MouseRightButtonDown += CanvasBorder_MouseRightButtonDown;
        CanvasBorder.MouseRightButtonUp += CanvasBorder_MouseRightButtonUp;

        UpdateToolButtons();
        UpdateCurrentColorUI();
        SyncCanvasState();
    }

    private void SyncCanvasState()
    {
        if (_vm == null || _vm._shapes == null) { return; }

        var visibleShapes = _vm._shapes.Values.Where(shape => !shape.IsDeleted).ToList();
        var ghosts = _vm.GhostShapes.ToList();

        ShapeRenderer.RenderAll(DrawArea, visibleShapes);

        foreach (IShape ghost in ghosts)
        {
            UIElement? element = ShapeRenderer.Render(DrawArea, ghost);
            if (element != null)
            {
                element.Opacity = 0.4;
                if (element is Shape s) { s.StrokeDashArray = new DoubleCollection { 2, 2 }; }
            }
        }

        UpdateSelectionBox();
    }

    // --- MOUSE HANDLERS ---

    private void CanvasBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) { return; }
        if (_currentPreviewElement != null)
        {
            DrawArea.Children.Remove(_currentPreviewElement);
            _currentPreviewElement = null;
        }

        Point pos = e.GetPosition(DrawArea);
        _vm.StartTracking(new Drawing.Point((int)pos.X, (int)pos.Y));

        if (_vm._isTracking || _vm.IsMovingShape)
        {
            (sender as UIElement)?.CaptureMouse();
        }
    }

    private void CanvasBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) { return; }
        if (_currentPreviewElement != null)
        {
            DrawArea.Children.Remove(_currentPreviewElement);
            _currentPreviewElement = null;
        }

        bool wasMoving = _vm.IsMovingShape;

        _vm.StopTracking();
        (sender as UIElement)?.ReleaseMouseCapture();

        if (wasMoving)
        {
            SyncCanvasState();
        }
    }

    // --- FIX: Logic Updated here ---
    private void CanvasBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (_vm == null) { return; }

        if (_isPanning)
        {
            // KEY FIX: Use GetPosition(this) instead of GetPosition(DrawArea)
            // 'this' refers to the UserControl, which is stationary. 
            // 'DrawArea' moves, which causes the jitter loop.
            Point currentPos = e.GetPosition(this);
            Vector delta = currentPos - _panLastPosition;

            CanvasTranslateTransform.X += delta.X;
            CanvasTranslateTransform.Y += delta.Y;

            _panLastPosition = currentPos;
        }
        else
        {
            Point pos = e.GetPosition(DrawArea);

            if (_vm._isTracking)
            {
                _vm.TrackPoint(new Drawing.Point((int)pos.X, (int)pos.Y));

                if (_currentPreviewElement != null) { DrawArea.Children.Remove(_currentPreviewElement); }

                IShape? previewData = _vm.CurrentPreviewShape;
                if (previewData != null)
                {
                    _currentPreviewElement = ShapeRenderer.Render(DrawArea, previewData);
                }
                else
                {
                    _currentPreviewElement = null;
                }
            }
            else if (_vm.IsMovingShape)
            {
                _vm.TrackPoint(new Drawing.Point((int)pos.X, (int)pos.Y));
                SyncCanvasState();
            }
        }
    }

    private void CanvasBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        // KEY FIX: Capture position relative to stationary container
        _panLastPosition = e.GetPosition(this); 
        (sender as UIElement)?.CaptureMouse();
    }
    // -------------------------------

    private void CanvasBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void CanvasBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        Point mousePos = e.GetPosition(DrawArea);
        ScaleTransform scaleTransform = CanvasScaleTransform;
        scaleTransform.CenterX = mousePos.X;
        scaleTransform.CenterY = mousePos.Y;

        double zoomFactor = (e.Delta > 0) ? ZOOM_FACTOR : (1.0 / ZOOM_FACTOR);
        double newScale = scaleTransform.ScaleX * zoomFactor;
        newScale = Math.Max(MIN_ZOOM, Math.Min(newScale, MAX_ZOOM));

        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm == null) { return; }
        if (e.PropertyName == nameof(CanvasViewModel.SelectedShape)) { UpdateSelectionBox(); }
        if (e.PropertyName == nameof(CanvasViewModel.CurrentMode)) { UpdateToolButtons(); }
        if (e.PropertyName == nameof(CanvasViewModel.CurrentColor)) { UpdateCurrentColorUI(); }
    }

    private void UpdateSelectionBox()
    {
        if (_selectionBox != null)
        {
            DrawArea.Children.Remove(_selectionBox);
            _selectionBox = null;
        }

        if (_vm != null && _vm.SelectedShape != null && !_vm.SelectedShape.IsDeleted)
        {
            _selectionBox = ShapeRenderer.CreateSelectionBox(_vm.SelectedShape.GetBoundingBox());
            DrawArea.Children.Add(_selectionBox);
        }
    }

    private void UpdateToolButtons()
    {
        if (_vm == null) { return; }
        BtnSelect.ClearValue(Button.BackgroundProperty);
        BtnFreehand.ClearValue(Button.BackgroundProperty);
        BtnLine.ClearValue(Button.BackgroundProperty);
        BtnRectangle.ClearValue(Button.BackgroundProperty);
        BtnEllipse.ClearValue(Button.BackgroundProperty);
        BtnTriangle.ClearValue(Button.BackgroundProperty);

        Brush? selectedBrush = Brushes.LightSteelBlue;
        try { selectedBrush = (Brush)FindResource("GlassyPressedBrush"); } catch { }

        switch (_vm.CurrentMode)
        {
            case CanvasViewModel.DrawingMode.Select: BtnSelect.Background = selectedBrush; break;
            case CanvasViewModel.DrawingMode.FreeHand: BtnFreehand.Background = selectedBrush; break;
            case CanvasViewModel.DrawingMode.StraightLine: BtnLine.Background = selectedBrush; break;
            case CanvasViewModel.DrawingMode.Rectangle: BtnRectangle.Background = selectedBrush; break;
            case CanvasViewModel.DrawingMode.EllipseShape: BtnEllipse.Background = selectedBrush; break;
            case CanvasViewModel.DrawingMode.TriangleShape: BtnTriangle.Background = selectedBrush; break;
        }
    }
    
    private void BtnSelect_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.Select; }
    private void BtnFreehand_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.FreeHand; }
    private void BtnLine_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.StraightLine; }
    private void BtnRectangle_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.Rectangle; }
    private void BtnTriangle_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.TriangleShape; }
    private void BtnEllipse_Click(object sender, RoutedEventArgs e) { if(_vm!=null) _vm.CurrentMode = CanvasViewModel.DrawingMode.EllipseShape; }
    private void BtnUndo_Click(object sender, RoutedEventArgs e) { _vm?.Undo(); }
    private void BtnRedo_Click(object sender, RoutedEventArgs e) { _vm?.Redo(); }
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null && _vm.IsHost) _vm.SaveShapes();
    }
    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is HostViewModel hostVm) hostVm.RestoreShapes();
    }
    private void BtnDelete_Click(object sender, RoutedEventArgs e) { _vm?.DeleteSelectedShape(); }
    private void CurrentColorButton_Click(object sender, RoutedEventArgs e) { ColorPopup.IsOpen = true; }
    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) { return; }
        if (sender is Button colorButton && colorButton.Background is SolidColorBrush brush)
        {
            System.Windows.Media.Color wpfColor = brush.Color;
            _vm.CurrentColor = Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            _vm.CommitModification();
            ColorPopup.IsOpen = false;
        }
    }

    private void UpdateCurrentColorUI()
    {
        if (_vm == null) { return; }
        Drawing.Color modelColor = _vm.CurrentColor;
        var wpfColor = System.Windows.Media.Color.FromArgb(modelColor.A, modelColor.R, modelColor.G, modelColor.B);
        CurrentColorButton.Background = new SolidColorBrush(wpfColor);
    }
}