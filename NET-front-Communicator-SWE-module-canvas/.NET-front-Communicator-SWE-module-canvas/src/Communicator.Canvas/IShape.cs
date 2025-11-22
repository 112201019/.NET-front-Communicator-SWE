using System.Collections.Generic;
using System.Drawing;

namespace Communicator.Canvas;

/// <summary>
/// Defines the contract for all geometric shapes on the canvas.
/// Implements the **Prototype Pattern** via the 'With...' methods for immutable state updates
/// and the **Visitor Pattern** via the 'Accept' method.
/// </summary>
public interface IShape
{
    /// <summary>
    /// Unique identifier for the shape (GUID).
    /// </summary>
    string ShapeId { get; }

    /// <summary>
    /// The specific type of the shape (used for serialization metadata).
    /// </summary>
    ShapeType Type { get; }

    /// <summary>
    /// The ordered list of points defining the shape's geometry.
    /// </summary>
    List<Point> Points { get; }

    /// <summary>
    /// The primary color of the shape.
    /// </summary>
    Color Color { get; }

    /// <summary>
    /// The thickness of the shape's stroke.
    /// </summary>
    double Thickness { get; }

    /// <summary>
    /// ID of the user who created this shape.
    /// </summary>
    string CreatedBy { get; }

    /// <summary>
    /// ID of the user who last modified this shape.
    /// </summary>
    string LastModifiedBy { get; }

    /// <summary>
    /// Indicates if the shape has been "soft deleted" (logical deletion).
    /// </summary>
    bool IsDeleted { get; }

    // --- Prototype Pattern Methods (Immutable Updates) ---

    /// <summary>
    /// Creates a new instance (clone) with updated visual properties (Color/Thickness).
    /// </summary>
    IShape WithUpdates(Color? newColor, double? newThickness, string modifiedByUserId);

    /// <summary>
    /// Creates a new instance (clone) translated by a specific offset.
    /// </summary>
    IShape WithMove(Point offset, Rectangle canvasBounds, string modifiedByUserId);

    /// <summary>
    /// Creates a new instance (clone) marked as deleted.
    /// </summary>
    IShape WithDelete(string modifiedByUserId);

    /// <summary>
    /// Creates a new instance (clone) marked as active (undoing a delete).
    /// </summary>
    IShape WithResurrect(string modifiedByUserId);

    // --- Geometry Methods ---

    /// <summary>
    /// Calculates the bounding rectangle of the shape.
    /// </summary>
    Rectangle GetBoundingBox();

    /// <summary>
    /// Determines if a specific point interacts with the shape (collision detection).
    /// </summary>
    bool IsHit(Point clickPoint);

    // --- Visitor Pattern Method ---

    /// <summary>
    /// Accepts a visitor to perform an operation on this specific shape type.
    /// This enables double-dispatch.
    /// </summary>
    /// <typeparam name="T">Return type of the visitor.</typeparam>
    /// <param name="visitor">The visitor instance.</param>
    /// <returns>The result of the visitor's operation.</returns>
    T Accept<T>(IShapeVisitor<T> visitor);
}
