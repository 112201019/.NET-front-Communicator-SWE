using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Communicator.Canvas;

public class StraightLine : IShape
{
    public string ShapeId { get; }
    public ShapeType Type => ShapeType.LINE;
    public List<Point> Points { get; } = new();
    public Color Color { get; }
    public double Thickness { get; }
    public string CreatedBy { get; }
    public string LastModifiedBy { get; }
    public bool IsDeleted { get; }

    public StraightLine(List<Point> points, Color color, double thickness, string createdByUserId)
    {
        ShapeId = Guid.NewGuid().ToString();
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdByUserId;
        LastModifiedBy = createdByUserId;
        IsDeleted = false;
    }

    public StraightLine(string shapeId, List<Point> points, Color color, double thickness, string createdBy, string lastModifiedBy, bool isDeleted)
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
        return new StraightLine(this.ShapeId, this.Points, newColor ?? this.Color, newThickness ?? this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithMove(Point offset, Rectangle canvasBounds, string modifiedByUserId)
    {
        Rectangle oldBounds = GetBoundingBox();
        if (oldBounds.Width == 0 && oldBounds.Height == 0)
        {
            return this;
        }

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
        return new StraightLine(this.ShapeId, newPoints, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithDelete(string modifiedByUserId)
    {
        return new StraightLine(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, true);
    }

    public IShape WithResurrect(string modifiedByUserId)
    {
        return new StraightLine(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, false);
    }

    public T Accept<T>(IShapeVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    public Rectangle GetBoundingBox()
    {
        if (Points.Count < 2)
        {
            return new Rectangle(0, 0, 0, 0);
        }

        int minX = Math.Min(Points[0].X, Points[1].X);
        int minY = Math.Min(Points[0].Y, Points[1].Y);
        int maxX = Math.Max(Points[0].X, Points[1].X);
        int maxY = Math.Max(Points[0].Y, Points[1].Y);
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    public bool IsHit(Point clickPoint)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        double tolerance = (Thickness / 2.0) + 2.0;
        return HitTestHelper.GetDistanceToLineSegment(clickPoint, Points[0], Points[1]) <= tolerance;
    }
}
