using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Communicator.Canvas;

public class EllipseShape : IShape
{
    public string ShapeId { get; }
    public ShapeType Type => ShapeType.ELLIPSE;
    public List<Point> Points { get; } = new();
    public Color Color { get; }
    public double Thickness { get; }
    public string CreatedBy { get; }
    public string LastModifiedBy { get; }
    public bool IsDeleted { get; }

    public EllipseShape(List<Point> points, Color color, double thickness, string createdByUserId)
    {
        ShapeId = Guid.NewGuid().ToString();
        Points.AddRange(points);
        Color = color;
        Thickness = thickness;
        CreatedBy = createdByUserId;
        LastModifiedBy = createdByUserId;
        IsDeleted = false;
    }

    public EllipseShape(string shapeId, List<Point> points, Color color, double thickness, string createdBy, string lastModifiedBy, bool isDeleted)
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
        return new EllipseShape(this.ShapeId, this.Points, newColor ?? this.Color, newThickness ?? this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
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
        return new EllipseShape(this.ShapeId, newPoints, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, this.IsDeleted);
    }

    public IShape WithDelete(string modifiedByUserId)
    {
        return new EllipseShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, true);
    }

    public IShape WithResurrect(string modifiedByUserId)
    {
        return new EllipseShape(this.ShapeId, this.Points, this.Color, this.Thickness, this.CreatedBy, modifiedByUserId, false);
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

        double centerX = (Points[0].X + Points[1].X) / 2.0;
        double centerY = (Points[0].Y + Points[1].Y) / 2.0;
        double radiusX = Math.Abs(Points[0].X - Points[1].X) / 2.0;
        double radiusY = Math.Abs(Points[0].Y - Points[1].Y) / 2.0;
        if (radiusX == 0 || radiusY == 0)
        {
            return false;
        }

        double tolerance = (Thickness / 2.0) + 2.0;
        double valOuter = Math.Pow(clickPoint.X - centerX, 2) / Math.Pow(radiusX + tolerance, 2) +
                          Math.Pow(clickPoint.Y - centerY, 2) / Math.Pow(radiusY + tolerance, 2);
        double valInner = Math.Pow(clickPoint.X - centerX, 2) / Math.Pow(radiusX - tolerance, 2) +
                          Math.Pow(clickPoint.Y - centerY, 2) / Math.Pow(radiusY - tolerance, 2);
        return (valOuter <= 1.0 && valInner >= 1.0);
    }
}
