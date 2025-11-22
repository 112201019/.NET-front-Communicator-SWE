using System;

namespace Communicator.Canvas;

/// <summary>
/// Interface for the Visitor Design Pattern.
/// Allows operations (like Rendering or Collision Detection) to be performed on specific shape types 
/// without using runtime type checking (is/as) or polluting the data models with logic.
/// </summary>
/// <typeparam name="T">The return type of the visitor operation (e.g., UIElement for rendering).</typeparam>
public interface IShapeVisitor<T>
{
    /// <summary>
    /// Visits a FreeHand shape instance.
    /// </summary>
    T Visit(FreeHand freeHand);

    /// <summary>
    /// Visits a RectangleShape instance.
    /// </summary>
    T Visit(RectangleShape rectangle);

    /// <summary>
    /// Visits a TriangleShape instance.
    /// </summary>
    T Visit(TriangleShape triangle);

    /// <summary>
    /// Visits a StraightLine shape instance.
    /// </summary>
    T Visit(StraightLine line);

    /// <summary>
    /// Visits an EllipseShape instance.
    /// </summary>
    T Visit(EllipseShape ellipse);
}
