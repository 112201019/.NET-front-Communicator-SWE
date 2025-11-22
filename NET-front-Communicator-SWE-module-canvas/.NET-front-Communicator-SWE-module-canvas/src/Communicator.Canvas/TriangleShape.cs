using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Communicator.Canvas;

public class TriangleShape : IShape
{
    public string ShapeId { get; }
    public ShapeType Type => ShapeType.TRIANGLE;
    public List<Point> Points { get; } = new();
    public Color Color { get; }
    public double Thickness { get; }
    public string CreatedBy { get; }
    public string LastModifiedBy { get; }
    public bool IsDeleted { get; }

    public TriangleShape(List<Point> points, Color color, double thickness, string createdByUserId)
    {
        ShapeId = Guid.NewGuid().ToString();
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdByUserId;
        LastModifiedBy = createdByUserId;
        IsDeleted = false;
    }

    public TriangleShape(string shapeId, List<Point> points, Color color, double thickness, string createdBy, string lastModifiedBy, bool isDeleted)
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
        return new TriangleShape(this.ShapeId, this.Points, newColor ?? this.Color, newThickness ?? this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
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
        return new TriangleShape(this.ShapeId, newPoints, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithDelete(string modifiedByUserId)
    {
        return new TriangleShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, true);
    }

    public IShape WithResurrect(string modifiedByUserId)
    {
        return new TriangleShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, false);
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
        int width = Math.Abs(Points[0].X - Points[1].X);
        int height = Math.Abs(Points[0].Y - Points[1].Y);
        return new Rectangle(minX, minY, width, height);
    }

    public bool IsHit(Point clickPoint)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        Point p1 = Points[0];
        Point p2 = Points[1];
        Point vertex1 = new Point(p1.X, p2.Y);
        Point vertex2 = new Point((p1.X + p2.X) / 2, p1.Y);
        Point vertex3 = new Point(p2.X, p2.Y);
        double tolerance = (Thickness / 2.0) + 2.0;
        if (HitTestHelper.GetDistanceToLineSegment(clickPoint, vertex1, vertex2) <= tolerance)
        {
            return true;
        }

        if (HitTestHelper.GetDistanceToLineSegment(clickPoint, vertex2, vertex3) <= tolerance)
        {
            return true;
        }

        if (HitTestHelper.GetDistanceToLineSegment(clickPoint, vertex3, vertex1) <= tolerance)
        {
            return true;
        }

        return false;
    }
}
