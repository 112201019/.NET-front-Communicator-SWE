using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using CanvasDataModel;
using ViewModel;
using Drawing = System.Drawing;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using static ViewModel.BaseCanvasViewModel;

namespace CentralGui;

public partial class MainWindow : Window
{
    // --- MODIFIED: ViewModels are now passed in ---
    private readonly BaseCanvasViewModel _vm;
    private readonly INetworkService _networkService;
    // --- END MODIFIED ---

    private UIElement? _currentPreviewElement = null;
    private Rectangle? _selectionBox = null;
    private UIElement? _ghostElement = null;

    private bool _isPanning = false;
    private Point _panStartPoint;

    private const double ZOOM_FACTOR = 1.1;
    private const double MAX_ZOOM = 5.0;
    private const double MIN_ZOOM = 0.5;

    // ---
    // --- NEW CONSTRUCTOR ---
    // ---
    public MainWindow(BaseCanvasViewModel viewModel, INetworkService networkService)
    {
        InitializeComponent();

        _vm = viewModel;
        _networkService = networkService;
        DataContext = _vm;

        // --- END NEW ---

        _vm.CanvasBounds = new Drawing.Rectangle(0, 0, (int)DrawArea.Width, (int)DrawArea.Height);

        _vm.PropertyChanged += Vm_PropertyChanged;

        CanvasBorder.MouseWheel += CanvasBorder_MouseWheel;
        CanvasBorder.MouseLeftButtonDown += CanvasBorder_MouseLeftButtonDown;
        CanvasBorder.MouseMove += CanvasBorder_MouseMove;
        CanvasBorder.MouseLeftButtonUp += CanvasBorder_MouseLeftButtonUp;
        CanvasBorder.MouseRightButtonDown += CanvasBorder_MouseRightButtonDown;
        CanvasBorder.MouseRightButtonUp += CanvasBorder_MouseRightButtonUp;

        this.KeyDown += (s, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                _vm.OnUndoRequested();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                _vm.OnRedoRequested();
            }
            else if ((e.Key == Key.Delete || e.Key == Key.D) && _vm.SelectedShape != null)
            {
                _vm.DeleteSelectedShape();
            }
            else if (e.Key == Key.T) // Save
            {
                // Save/Load are disabled in network mode for this example
            }
            else if (e.Key == Key.N) // Load
            {
                // Save/Load are disabled in network mode for this example
            }
            else if (e.Key == Key.S) // Snapshot
            {
                SaveCanvasSnapshot();
            }
        };
        UpdateToolButtons();
        UpdateCurrentColorUI();

        ThicknessSlider.ValueChanged += (s, e) =>
        {
            if (ThicknessSlider.IsMouseCaptureWithin)
            {
                SyncCanvasState();
            }
        };

        ThicknessSlider.PreviewMouseLeftButtonUp += (s, e) =>
        {
            _vm.CommitModification();
            ThicknessPopup.IsOpen = false;
        };
    }

    // ... (All other methods: Handlers, SyncCanvasState, UpdateToolButtons, etc. are identical to the previous version) ...

    private void CanvasBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning) return;
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
        if (_isPanning) return;
        if (_currentPreviewElement != null)
        {
            DrawArea.Children.Remove(_currentPreviewElement);
            _currentPreviewElement = null;
        }
        _vm.StopTracking();
        (sender as UIElement)?.ReleaseMouseCapture();
        SyncCanvasState();
    }

    private void CanvasBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition((UIElement)CanvasBorder.Parent);
            (sender as UIElement)?.CaptureMouse();
        }
    }

    private void CanvasBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void CanvasBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            Point currentPanPoint = e.GetPosition((UIElement)CanvasBorder.Parent);
            Vector panDelta = currentPanPoint - _panStartPoint;
            double currentScale = CanvasScaleTransform.ScaleX;
            CanvasTranslateTransform.X += panDelta.X / currentScale;
            CanvasTranslateTransform.Y += panDelta.Y / currentScale;
            _panStartPoint = currentPanPoint;
        }
        else
        {
            Point pos = e.GetPosition(DrawArea);
            if (_vm._isTracking)
            {
                _vm.TrackPoint(new Drawing.Point((int)pos.X, (int)pos.Y));
                if (_currentPreviewElement != null)
                {
                    DrawArea.Children.Remove(_currentPreviewElement);
                }
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
        if (e.PropertyName == nameof(BaseCanvasViewModel.SelectedShape))
        {
            UpdateSelectionBox();
            SyncCanvasState();
        }
        if (e.PropertyName == nameof(BaseCanvasViewModel.CurrentMode))
        {
            UpdateToolButtons();
        }
        if (e.PropertyName == nameof(BaseCanvasViewModel.CurrentColor))
        {
            UpdateCurrentColorUI();
            SyncCanvasState();
        }
        if (e.PropertyName == nameof(BaseCanvasViewModel.CurrentThickness))
        {
            // SyncCanvasState(); // This spams, let's rely on the slider mouse up
        }
        if (e.PropertyName == nameof(BaseCanvasViewModel.GhostShape))
        {
            UpdateGhostShape();
        }
    }

    private void UpdateGhostShape()
    {
        if (_ghostElement != null)
        {
            GhostCanvas.Children.Remove(_ghostElement);
            _ghostElement = null;
        }

        if (_vm.GhostShape != null)
        {
            _ghostElement = ShapeRenderer.RenderGhost(GhostCanvas, _vm.GhostShape);
        }
    }

    private void UpdateSelectionBox()
    {
        if (_selectionBox != null)
        {
            DrawArea.Children.Remove(_selectionBox);
            _selectionBox = null;
        }

        if (_vm.SelectedShape != null && !_vm.SelectedShape.IsDeleted)
        {
            _selectionBox = ShapeRenderer.CreateSelectionBox(_vm.SelectedShape.GetBoundingBox());
            DrawArea.Children.Add(_selectionBox);
        }
    }

    private void SyncCanvasState()
    {
        if (_vm._shapes == null) return;

        IEnumerable<IShape> visibleShapes = _vm._shapes.Values
                                .Where(shape => !shape.IsDeleted);

        ShapeRenderer.RenderAll(DrawArea, visibleShapes);
        UpdateSelectionBox();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button colorButton && colorButton.Background is SolidColorBrush brush)
        {
            System.Windows.Media.Color wpfColor = brush.Color;
            Drawing.Color modelColor = Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            _vm.CurrentColor = modelColor;
            _vm.CommitModification();
            UpdateCurrentColorUI();
            ColorPopup.IsOpen = false;
        }
    }

    private void CurrentColorButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.IsOpen = true;
    }

    private void BtnThickness_Click(object sender, RoutedEventArgs e)
    {
        ThicknessPopup.IsOpen = true;
    }

    private void UpdateCurrentColorUI()
    {
        Drawing.Color modelColor = _vm.CurrentColor;
        var wpfColor = System.Windows.Media.Color.FromArgb(modelColor.A, modelColor.R, modelColor.G, modelColor.B);
        CurrentColorButton.Background = new SolidColorBrush(wpfColor);
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.Select;
        UpdateToolButtons();
    }

    private void BtnFreehand_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.FreeHand;
        UpdateToolButtons();
    }

    private void BtnLine_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.StraightLine;
        UpdateToolButtons();
    }

    private void BtnRectangle_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.Rectangle;
        UpdateToolButtons();
    }

    private void BtnTriangle_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.TriangleShape;
        UpdateToolButtons();
    }

    private void BtnEllipse_Click(object sender, RoutedEventArgs e)
    {
        _vm.CurrentMode = DrawingMode.EllipseShape;
        UpdateToolButtons();
    }

    private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        SaveCanvasSnapshot();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        _vm.OnUndoRequested();
    }

    private void BtnRedo_Click(object sender, RoutedEventArgs e)
    {
        _vm.OnRedoRequested();
    }

    private void UpdateToolButtons()
    {
        BtnSelect.ClearValue(Button.BackgroundProperty);
        BtnSelect.ClearValue(Button.BorderBrushProperty);
        BtnFreehand.ClearValue(Button.BackgroundProperty);
        BtnFreehand.ClearValue(Button.BorderBrushProperty);
        BtnLine.ClearValue(Button.BackgroundProperty);
        BtnLine.ClearValue(Button.BorderBrushProperty);
        BtnRectangle.ClearValue(Button.BackgroundProperty);
        BtnRectangle.ClearValue(Button.BorderBrushProperty);
        BtnEllipse.ClearValue(Button.BackgroundProperty);
        BtnEllipse.ClearValue(Button.BorderBrushProperty);
        BtnTriangle.ClearValue(Button.BackgroundProperty);
        BtnTriangle.ClearValue(Button.BorderBrushProperty);
        BtnUndo.ClearValue(Button.BackgroundProperty);
        BtnUndo.ClearValue(Button.BorderBrushProperty);
        BtnRedo.ClearValue(Button.BackgroundProperty);
        BtnRedo.ClearValue(Button.BorderBrushProperty);
        BtnSnapshot.ClearValue(Button.BackgroundProperty);
        BtnSnapshot.ClearValue(Button.BorderBrushProperty);
        BtnThickness.ClearValue(Button.BackgroundProperty);
        BtnThickness.ClearValue(Button.BorderBrushProperty);

        Brush? selectedBrush = null;
        Brush? selectedBorder = null;
        try
        {
            selectedBrush = (Brush)FindResource("GlassyPressedBrush");
        }
        catch
        {
            selectedBrush = Brushes.LightSteelBlue;
        }

        selectedBorder = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100));

        Button? selectedButton = _vm.CurrentMode switch
        {
            DrawingMode.Select => BtnSelect,
            DrawingMode.FreeHand => BtnFreehand,
            DrawingMode.StraightLine => BtnLine,
            DrawingMode.Rectangle => BtnRectangle,
            DrawingMode.EllipseShape => BtnEllipse,
            DrawingMode.TriangleShape => BtnTriangle,
            _ => null
        };

        if (selectedButton != null)
        {
            selectedButton.Background = selectedBrush;
        }
    }

    private void SaveCanvasSnapshot()
    {
        SaveFileDialog dialog = new SaveFileDialog
        {
            FileName = "canvas_snapshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png",
            DefaultExt = ".png",
            Filter = "PNG Image (.png)|*.png"
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
        {
            Console.WriteLine("--- Snapshot save cancelled by user. ---");
            return;
        }

        try
        {
            FrameworkElement elementToRender = CanvasBorder;

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)elementToRender.ActualWidth,
                (int)elementToRender.ActualHeight,
                96d, 96d, PixelFormats.Pbgra32
            );

            rtb.Render(elementToRender);

            PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

            using (FileStream fs = File.OpenWrite(dialog.FileName))
            {
                pngEncoder.Save(fs);
            }

            Console.WriteLine($"--- Snapshot saved successfully to {dialog.FileName} ---");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--- !!! Snapshot save FAILED !!! ---");
            Console.WriteLine(ex.ToString());
            MessageBox.Show($"Failed to save snapshot:\n{ex.Message}", "Snapshot Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
