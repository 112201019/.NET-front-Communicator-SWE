using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Canvas;

/// <summary>
/// Defines the type of action performed on the canvas for state management.
/// </summary>
public enum CanvasActionType
{
    /// <summary>
    /// Represents the initial empty state.
    /// </summary>
    Initial,

    /// <summary>
    /// Represents the creation of a new shape.
    /// </summary>
    Create,

    /// <summary>
    /// Represents the deletion of a shape (by setting IsDeleted = true).
    /// </summary>
    Delete, // <-- ADDED THIS

    /// <summary>
    /// Represents the modification of a shape's properties (color, thickness, points).
    /// </summary>
    Modify,

    /// <summary>
    /// Represents the resurrection of a shape (by setting IsDeleted = false).
    /// </summary>
    Resurrect // <-- ADDED THIS
}
