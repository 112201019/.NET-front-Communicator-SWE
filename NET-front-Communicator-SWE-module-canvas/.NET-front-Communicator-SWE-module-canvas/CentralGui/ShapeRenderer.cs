using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;
namespace CanvasDataModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using System;
using Drawing = System.Drawing;
public static class ShapeRenderer
{
    private static SolidColorBrush ToWpfBrush(System.Drawing.Color color)
    {
        var wpfColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(wpfColor);
    }

    public static UIElement? Render(Canvas canvas, IShape shape)
    {
        switch (shape.Type)
        {
            case ShapeType.FREEHAND:
                return RenderFreeHand(canvas, (FreeHand)shape);
            case ShapeType.LINE:
                return RenderStraightLine(canvas, (StraightLine)shape);
            case ShapeType.RECTANGLE:
                return RenderRectangle(canvas, (RectangleShape)shape);
            case ShapeType.ELLIPSE:
                return RenderEllipse(canvas, (EllipseShape)shape);
            case ShapeType.TRIANGLE:
                return RenderTriangle(canvas, (TriangleShape)shape);
        }
        return null;
    }

    // --- NEW GHOST RENDERER ---
    /// <summary>
    /// Renders a shape with 50% opacity for client-side feedback.
    /// </summary>
    public static UIElement? RenderGhost(Canvas canvas, IShape shape)
    {
        UIElement? uiElement = Render(canvas, shape);
        if (uiElement != null)
        {
            uiElement.Opacity = 0.5;
        }
        return uiElement;
    }
    // --- END NEW ---

    private static UIElement RenderStraightLine(Canvas canvas, StraightLine line)
    {
        // ... (implementation unchanged) ...
        Line uiLine = new Line
        {
            X1 = line.Points[0].X,
            Y1 = line.Points[0].Y,
            X2 = line.Points[1].X,
            Y2 = line.Points[1].Y,
            Stroke = ToWpfBrush(line.Color),
            StrokeThickness = line.Thickness
        };
        canvas.Children.Add(uiLine);
        return uiLine;
    }
    private static UIElement RenderFreeHand(Canvas canvas, FreeHand freeHand)
    {
        // ... (implementation unchanged) ...
        if (freeHand.Points.Count < 2)
        {
            return new FrameworkElement();
        }
        SolidColorBrush brush = ToWpfBrush(freeHand.Color);
        PointCollection wpfPoints = new PointCollection();
        foreach (System.Drawing.Point point in freeHand.Points)
        {
            wpfPoints.Add(new System.Windows.Point(point.X, point.Y));
        }
        Polyline polyline = new Polyline
        {
            Points = wpfPoints,
            Stroke = brush,
            StrokeThickness = freeHand.Thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(polyline);
        return polyline;
    }
    private static UIElement RenderRectangle(Canvas canvas, RectangleShape rectangle)
    {
        // ... (implementation unchanged) ...
        System.Drawing.Point topLeftPoint = rectangle.Points[0];
        System.Drawing.Point bottomRightPoint = rectangle.Points[1];
        var topLeft = new System.Windows.Point(topLeftPoint.X, topLeftPoint.Y);
        var bottomRight = new System.Windows.Point(bottomRightPoint.X, bottomRightPoint.Y);
        double x = Math.Min(topLeft.X, bottomRight.X);
        double y = Math.Min(topLeft.Y, bottomRight.Y);
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);
        Rectangle uiRectangle = new Rectangle
        {
            Width = width,
            Height = height,
            Stroke = ToWpfBrush(rectangle.Color),
            StrokeThickness = rectangle.Thickness
        };
        Canvas.SetLeft(uiRectangle, x);
        Canvas.SetTop(uiRectangle, y);
        canvas.Children.Add(uiRectangle);
        return uiRectangle;
    }
    public static UIElement RenderEllipse(Canvas canvas, EllipseShape ellipse)
    {
        // ... (implementation unchanged) ...
        System.Drawing.Point topLeft = ellipse.Points[0];
        System.Drawing.Point bottomRight = ellipse.Points[1];
        double x = Math.Min(topLeft.X, bottomRight.X);
        double y = Math.Min(topLeft.Y, bottomRight.Y);
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);
        Ellipse uiEllipse = new Ellipse
        {
            Width = width,
            Height = height,
            Stroke = ToWpfBrush(ellipse.Color),
            StrokeThickness = ellipse.Thickness
        };
        Canvas.SetLeft(uiEllipse, x);
        Canvas.SetTop(uiEllipse, y);
        canvas.Children.Add(uiEllipse);
        return uiEllipse;
    }
    public static UIElement RenderTriangle(Canvas canvas, TriangleShape triangle)
    {
        // ... (implementation unchanged) ...
        System.Drawing.Point p1 = triangle.Points[0];
        System.Drawing.Point p2 = triangle.Points[1];
        System.Windows.Point vertex1 = new System.Windows.Point(p1.X, p2.Y);
        System.Windows.Point vertex2 = new System.Windows.Point((p1.X + p2.X) / 2, p1.Y);
        System.Windows.Point vertex3 = new System.Windows.Point(p2.X, p2.Y);
        PointCollection wpfPoints = new PointCollection
        {
            vertex1,
            vertex2,
            vertex3
        };
        Polygon uiTriangle = new Polygon
        {
            Points = wpfPoints,
            Stroke = ToWpfBrush(triangle.Color),
            StrokeThickness = triangle.Thickness,
            StrokeLineJoin = PenLineJoin.Miter
        };
        canvas.Children.Add(uiTriangle);
        return uiTriangle;
    }

    public static void RenderAll(Canvas canvas, IEnumerable<IShape> shapes)
    {
        canvas.Children.Clear();
        foreach (IShape shape in shapes) { Render(canvas, shape); }
    }

    public static Rectangle CreateSelectionBox(Drawing.Rectangle bounds)
    {
        // ... (implementation unchanged) ...
        Rectangle selectionBox = new Rectangle
        {
            Width = bounds.Width + 4,
            Height = bounds.Height + 4,
            Fill = Brushes.Transparent,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        Canvas.SetLeft(selectionBox, bounds.Left - 2);
        Canvas.SetTop(selectionBox, bounds.Top - 2);
        return selectionBox;
    }
}
