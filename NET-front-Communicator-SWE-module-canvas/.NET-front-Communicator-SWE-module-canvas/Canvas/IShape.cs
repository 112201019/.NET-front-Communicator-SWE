using System.Drawing;
using System.Text.Json.Serialization;

namespace CanvasDataModel;

// --- NO JSON ATTRIBUTES NEEDED FOR MANUAL SERIALIZER ---
public interface IShape
{
    string ShapeId { get; }
    ShapeType Type { get; }
    List<Point> Points { get; }
    Color Color { get; }
    double Thickness { get; }
    string CreatedBy { get; }
    string LastModifiedBy { get; }
    bool IsDeleted { get; }

    /// <summary>
    /// Creates a new shape instance with updated properties (e.g., color, thickness).
    /// </summary>
    IShape WithUpdates(Color? newColor, double? newThickness, string modifiedByUserId);
    /// <summary>
    /// Creates a new shape instance with all points translated by an offset.
    /// </summary>
    IShape WithMove(Point offset, Rectangle canvasBounds, string modifiedByUserId);
    /// <summary>
    /// Creates a new shape instance marked as deleted.
    /// </summary>
    IShape WithDelete(string modifiedByUserId);
    /// <summary>
    /// Creates a new shape instance marked as not deleted.
    /// </summary>
    IShape WithResurrect(string modifiedByUserId);

    Rectangle GetBoundingBox();
    bool IsHit(Point clickPoint);
}
