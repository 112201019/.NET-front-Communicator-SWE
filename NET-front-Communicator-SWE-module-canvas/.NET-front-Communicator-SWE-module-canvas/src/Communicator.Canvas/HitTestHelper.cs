// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Canvas;
public class HitTestHelper
{
    /// <summary>
    /// Calculates the minimum distance from a point to a line segment.
    /// </summary>
    public static double GetDistanceToLineSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;

        // If the segment is just a point, return distance to that point
        if (dx == 0 && dy == 0)
        {
            return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
        }

        // Project p onto the line, but parameterized (t)
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);

        Point closestPoint;
        if (t < 0)
        {
            closestPoint = a; // Closest to endpoint a
        }
        else if (t > 1)
        {
            closestPoint = b; // Closest to endpoint b
        }
        else
        {
            // Closest is on the segment
            closestPoint = new Point((int)(a.X + t * dx), (int)(a.Y + t * dy));
        }

        // Return distance to the closest point
        dx = p.X - closestPoint.X;
        dy = p.Y - closestPoint.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Checks if a point is within a rectangular area, including a tolerance.
    /// </summary>
    public static bool IsPointInRectangle(Point p, Rectangle rect, double tolerance)
    {
        return p.X >= rect.Left - tolerance &&
               p.X <= rect.Right + tolerance &&
               p.Y >= rect.Top - tolerance &&
               p.Y <= rect.Bottom + tolerance;
    }
}
