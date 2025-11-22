using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Communicator.Canvas;

/// <summary>
/// Represents a freehand drawing consisting of multiple connected points.
/// </summary>
public class FreeHand : IShape
{
    public string ShapeId { get; }
    public ShapeType Type => ShapeType.FREEHAND;
    public List<Point> Points { get; } = new();
    public Color Color { get; }
    public double Thickness { get; }
    public string CreatedBy { get; }
    public string LastModifiedBy { get; }
    public bool IsDeleted { get; }

    /// <summary>
    /// Primary constructor for new shape creation.
    /// </summary>
    public FreeHand(List<Point> points, Color color, double thickness, string createdByUserId)
    {
        ShapeId = Guid.NewGuid().ToString();
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdByUserId;
        LastModifiedBy = createdByUserId;
        IsDeleted = false;
    }

    /// <summary>
    /// Constructor for deserialization and cloning (internal use).
    /// </summary>
    public FreeHand(string shapeId, List<Point> points, Color color, double thickness, string createdBy, string lastModifiedBy, bool isDeleted)
    {
        ShapeId = shapeId;
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdBy;
        LastModifiedBy = lastModifiedBy;
        IsDeleted = isDeleted;
    }

    // --- Prototype Pattern Implementation ---

    public IShape WithUpdates(Color? newColor, double? newThickness, string modifiedByUserId)
    {
        return new FreeHand(this.ShapeId, this.Points, newColor ?? this.Color, newThickness ?? this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
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

        // Clamp logic to keep shape inside bounds
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
        foreach (Point p in this.Points)
        {
            newPoints.Add(new Point(p.X + offset.X, p.Y + offset.Y));
        }

        return new FreeHand(this.ShapeId, newPoints, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithDelete(string modifiedByUserId)
    {
        return new FreeHand(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, true);
    }

    public IShape WithResurrect(string modifiedByUserId)
    {
        return new FreeHand(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, false);
    }

    // --- Visitor Pattern Implementation ---

    /// <summary>
    /// Dispatches the call to the visitor's Visit(FreeHand) method.
    /// </summary>
    public T Accept<T>(IShapeVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }

    public Rectangle GetBoundingBox()
    {
        if (Points.Count == 0)
        {
            return new Rectangle(0, 0, 0, 0);
        }

        int minX = Points.Min(p => p.X);
        int minY = Points.Min(p => p.Y);
        int maxX = Points.Max(p => p.X);
        int maxY = Points.Max(p => p.Y);
        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    public bool IsHit(Point clickPoint)
    {
        double tolerance = (Thickness / 2.0) + 2.0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (HitTestHelper.GetDistanceToLineSegment(clickPoint, Points[i], Points[i + 1]) <= tolerance)
            {
                return true;
            }
        }
        return false;
    }
}
