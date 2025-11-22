using System;
using System.Collections.Generic;
using System.Drawing;

namespace Communicator.Canvas;

/// <summary>
/// Implements the **Factory Method Pattern**.
/// Centralizes the creation logic for different IShape implementations.
/// </summary>
public static class ShapeFactory
{
    /// <summary>
    /// Creates a specific IShape instance based on the provided ShapeType.
    /// </summary>
    /// <param name="type">The type of shape to create.</param>
    /// <param name="points">The points defining the shape.</param>
    /// <param name="color">The color of the shape.</param>
    /// <param name="thickness">The stroke thickness.</param>
    /// <param name="userId">The ID of the user creating the shape.</param>
    /// <returns>A new IShape instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the ShapeType is not supported.</exception>
    public static IShape CreateShape(ShapeType type, List<Point> points, Color color, double thickness, string userId)
    {
        // C# 8.0 Switch Expression for clean factory logic
        return type switch
        {
            ShapeType.FREEHAND => new FreeHand(points, color, thickness, userId),
            ShapeType.LINE => new StraightLine(points, color, thickness, userId),
            ShapeType.RECTANGLE => new RectangleShape(points, color, thickness, userId),
            ShapeType.ELLIPSE => new EllipseShape(points, color, thickness, userId),
            ShapeType.TRIANGLE => new TriangleShape(points, color, thickness, userId),
            _ => throw new ArgumentException($"Unsupported shape type: {type}")
        };
    }
}
