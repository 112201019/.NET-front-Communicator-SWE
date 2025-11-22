using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Communicator.Canvas;

public class RectangleShape : IShape
{
    public string ShapeId { get; }
    public ShapeType Type => ShapeType.RECTANGLE;
    public List<Point> Points { get; } = new();
    public Color Color { get; }
    public double Thickness { get; }
    public string CreatedBy { get; }
    public string LastModifiedBy { get; }
    public bool IsDeleted { get; }

    public RectangleShape(List<Point> points, Color color, double thickness, string createdByUserId)
    {
        ShapeId = Guid.NewGuid().ToString();
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdByUserId;
        LastModifiedBy = createdByUserId;
        IsDeleted = false;
    }

    public RectangleShape(string shapeId, List<Point> points, Color color, double thickness, string createdBy, string lastModifiedBy, bool isDeleted)
    {
        ShapeId = shapeId;
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdBy;
        LastModifiedBy = lastModifiedBy;
        IsDeleted = isDeleted;
    }

    public IShape WithUpdates(Color? newColor, double? newThickness, string modifiedByUserId)
    {
        return new RectangleShape(this.ShapeId, this.Points, newColor ?? this.Color, newThickness ?? this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithMove(Point offset, Rectangle canvasBounds, string modifiedByUserId)
    {
        Rectangle oldBounds = GetBoundingBox();
        int newLeft = oldBounds.Left + offset.X;
        int newTop = oldBounds.Top + offset.Y;
        if (newLeft < canvasBounds.Left)
        {
            offset.X = canvasBounds.Left - oldBounds.Left;
        }

        if (newTop < canvasBounds.Top)
        {
            offset.Y = canvasBounds.Top - oldBounds.Top;
        }

        if (newLeft + oldBounds.Width > canvasBounds.Right)
        {
            offset.X = canvasBounds.Right - oldBounds.Right;
        }

        if (newTop + oldBounds.Height > canvasBounds.Bottom)
        {
            offset.Y = canvasBounds.Bottom - oldBounds.Bottom;
        }

        List<Point> newPoints = new List<Point>();
        foreach (Point p in this.Points) { newPoints.Add(new Point(p.X + offset.X, p.Y + offset.Y)); }
        return new RectangleShape(this.ShapeId, newPoints, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithDelete(string modifiedByUserId)
    {
        return new RectangleShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, true);
    }

    public IShape WithResurrect(string modifiedByUserId)
    {
        return new RectangleShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, false);
    }

    public T Accept<T>(IShapeVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    private Rectangle GetBoundsInternal()
    {
        if (Points.Count < 2)
        {
            return new Rectangle(0, 0, 0, 0);
        }

        int minX = Math.Min(Points[0].X, Points[1].X);
        int minY = Math.Min(Points[0].Y, Points[1].Y);
        int width = Math.Abs(Points[0].X - Points[1].X);
        int height = Math.Abs(Points[0].Y - Points[1].Y);
        return new Rectangle(minX, minY, width, height);
    }

    public Rectangle GetBoundingBox()
    {
        return GetBoundsInternal();
    }

    public bool IsHit(Point clickPoint)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        Rectangle bounds = GetBoundsInternal();
        double tolerance = (Thickness / 2.0) + 2.0;
        if (HitTestHelper.IsPointInRectangle(clickPoint, bounds, 0))
        {
            Rectangle innerBounds = new Rectangle(
                (int)(bounds.Left + tolerance), (int)(bounds.Top + tolerance),
                (int)(bounds.Width - 2 * tolerance), (int)(bounds.Height - 2 * tolerance)
            );
            return !HitTestHelper.IsPointInRectangle(clickPoint, innerBounds, 0);
        }
        return false;
    }
}
