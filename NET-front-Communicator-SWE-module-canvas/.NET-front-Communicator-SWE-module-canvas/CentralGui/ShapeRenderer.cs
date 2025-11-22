using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Communicator.Canvas;
using Drawing = System.Drawing;

namespace CentralGui;

/// <summary>
/// Responsible for converting IShape data models into WPF UIElements.
/// Acts as a Facade for the Rendering Visitor logic.
/// </summary>
public static class ShapeRenderer
{
    /// <summary>
    /// Renders a single shape onto the provided Canvas.
    /// Uses the **Visitor Pattern** to dispatch the specific rendering logic without type casting.
    /// </summary>
    /// <param name="canvas">The WPF Canvas container.</param>
    /// <param name="shape">The data model to render.</param>
    /// <returns>The created UIElement.</returns>
    public static UIElement? Render(Canvas canvas, IShape shape)
    {
        // Instantiate the concrete visitor that knows how to create WPF elements
        var visitor = new WpfShapeRenderingVisitor(canvas);

        // Double-dispatch: The shape calls the correct Visit method on the visitor
        return shape.Accept(visitor);
    }

    /// <summary>
    /// Renders a collection of shapes.
    /// </summary>
    public static void RenderAll(Canvas canvas, IEnumerable<IShape> shapes)
    {
        canvas.Children.Clear();
        foreach (IShape shape in shapes) { Render(canvas, shape); }
    }

    /// <summary>
    /// Internal Visitor implementation for WPF rendering.
    /// This encapsulates all the WPF-specific construction logic (Separation of Concerns).
    /// </summary>
    private class WpfShapeRenderingVisitor : IShapeVisitor<UIElement>
    {
        private readonly Canvas _canvas;

        public WpfShapeRenderingVisitor(Canvas canvas)
        {
            _canvas = canvas;
        }

        /// <summary>
        /// Helper to convert System.Drawing.Color to WPF SolidColorBrush.
        /// </summary>
        private SolidColorBrush ToWpfBrush(Drawing.Color color)
        {
            var wpfColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            return new SolidColorBrush(wpfColor);
        }

        public UIElement Visit(FreeHand freeHand)
        {
            if (freeHand.Points.Count < 2)
            {
                return new FrameworkElement();
            }

            PointCollection wpfPoints = new PointCollection();
            foreach (Drawing.Point p in freeHand.Points)
            {
                wpfPoints.Add(new System.Windows.Point(p.X, p.Y));
            }

            Polyline polyline = new Polyline
            {
                Points = wpfPoints,
                Stroke = ToWpfBrush(freeHand.Color),
                StrokeThickness = freeHand.Thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            _canvas.Children.Add(polyline);
            return polyline;
        }

        public UIElement Visit(StraightLine line)
        {
            Line uiLine = new Line
            {
                X1 = line.Points[0].X,
                Y1 = line.Points[0].Y,
                X2 = line.Points[1].X,
                Y2 = line.Points[1].Y,
                Stroke = ToWpfBrush(line.Color),
                StrokeThickness = line.Thickness
            };
            _canvas.Children.Add(uiLine);
            return uiLine;
        }

        public UIElement Visit(RectangleShape rectangle)
        {
            Drawing.Point p1 = rectangle.Points[0];
            Drawing.Point p2 = rectangle.Points[1];

            double x = Math.Min(p1.X, p2.X);
            double y = Math.Min(p1.Y, p2.Y);
            double width = Math.Abs(p2.X - p1.X);
            double height = Math.Abs(p2.Y - p1.Y);

            Rectangle uiRect = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = ToWpfBrush(rectangle.Color),
                StrokeThickness = rectangle.Thickness
            };

            Canvas.SetLeft(uiRect, x);
            Canvas.SetTop(uiRect, y);
            _canvas.Children.Add(uiRect);
            return uiRect;
        }

        public UIElement Visit(EllipseShape ellipse)
        {
            Drawing.Point p1 = ellipse.Points[0];
            Drawing.Point p2 = ellipse.Points[1];

            double x = Math.Min(p1.X, p2.X);
            double y = Math.Min(p1.Y, p2.Y);
            double width = Math.Abs(p2.X - p1.X);
            double height = Math.Abs(p2.Y - p1.Y);

            Ellipse uiEllipse = new Ellipse
            {
                Width = width,
                Height = height,
                Stroke = ToWpfBrush(ellipse.Color),
                StrokeThickness = ellipse.Thickness
            };

            Canvas.SetLeft(uiEllipse, x);
            Canvas.SetTop(uiEllipse, y);
            _canvas.Children.Add(uiEllipse);
            return uiEllipse;
        }

        public UIElement Visit(TriangleShape triangle)
        {
            Drawing.Point p1 = triangle.Points[0];
            Drawing.Point p2 = triangle.Points[1];

            // Calculate vertices for an isosceles triangle within the bounding box
            System.Windows.Point v1 = new System.Windows.Point(p1.X, p2.Y); // Bottom Left
            System.Windows.Point v2 = new System.Windows.Point((p1.X + p2.X) / 2, p1.Y); // Top Center
            System.Windows.Point v3 = new System.Windows.Point(p2.X, p2.Y); // Bottom Right

            Polygon uiTriangle = new Polygon
            {
                Points = new PointCollection { v1, v2, v3 },
                Stroke = ToWpfBrush(triangle.Color),
                StrokeThickness = triangle.Thickness,
                StrokeLineJoin = PenLineJoin.Miter
            };

            _canvas.Children.Add(uiTriangle);
            return uiTriangle;
        }
    }

    /// <summary>
    /// Helper to create a visual selection box around a shape.
    /// </summary>
    public static Rectangle CreateSelectionBox(Drawing.Rectangle bounds)
    {
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
