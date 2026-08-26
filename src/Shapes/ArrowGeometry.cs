using System;
using System.Drawing;

namespace QuickShot
{
    internal static class ArrowGeometry
    {
        const double Tan225 = 0.41421356237;

        internal static Point ConstrainToEightDirections(Point anchor, Point pt)
        {
            int dx = pt.X - anchor.X, dy = pt.Y - anchor.Y;
            int absX = Math.Abs(dx), absY = Math.Abs(dy);
            if (absY <= absX * Tan225)
                return new Point(anchor.X + dx, anchor.Y);
            if (absX <= absY * Tan225)
                return new Point(anchor.X, anchor.Y + dy);
            int span = absX > absY ? absX : absY;
            return new Point(anchor.X + (dx >= 0 ? span : -span),
                             anchor.Y + (dy >= 0 ? span : -span));
        }
    }
}
