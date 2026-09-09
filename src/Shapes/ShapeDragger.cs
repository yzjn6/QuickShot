using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuickShot
{
    internal struct Frame
    {
        internal int Left, Top, Right, Bottom;

        internal Frame(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }

        internal static Frame FromRect(Rectangle r) { return new Frame(r.Left, r.Top, r.Right, r.Bottom); }

        internal int Width { get { return Right - Left; } }
        internal int Height { get { return Bottom - Top; } }

        internal Rectangle Bounds
        {
            get
            {
                return Rectangle.FromLTRB(Math.Min(Left, Right), Math.Min(Top, Bottom),
                    Math.Max(Left, Right), Math.Max(Top, Bottom));
            }
        }

        internal Frame Offset(int dx, int dy)
        {
            return new Frame(Left + dx, Top + dy, Right + dx, Bottom + dy);
        }
    }

    internal static class ShapeDragger
    {
        internal const int None = -1;
        internal const int Body = 8;
        internal const int HandleCount = 8;

        static int Half(float scale)
        {
            return Math.Max(2, (int)Math.Round(3f * scale));
        }

        internal static Point HandleCenter(Rectangle f, int i)
        {
            int cx = (f.Left + f.Right) / 2;
            int cy = (f.Top + f.Bottom) / 2;
            switch (i)
            {
                case 0: return new Point(f.Left, f.Top);
                case 1: return new Point(cx, f.Top);
                case 2: return new Point(f.Right, f.Top);
                case 3: return new Point(f.Right, cy);
                case 4: return new Point(f.Right, f.Bottom);
                case 5: return new Point(cx, f.Bottom);
                case 6: return new Point(f.Left, f.Bottom);
                case 7: return new Point(f.Left, cy);
            }
            return new Point(cx, cy);
        }

        internal static Rectangle HandleRect(Rectangle f, int i, float scale)
        {
            Point c = HandleCenter(f, i);
            int half = Half(scale);
            return new Rectangle(c.X - half, c.Y - half, half * 2, half * 2);
        }

        internal static int HitHandle(Rectangle f, Point p, float scale)
        {
            int half = Half(scale) + 1;
            for (int i = 0; i < HandleCount; i++)
            {
                Point c = HandleCenter(f, i);
                if (Math.Abs(p.X - c.X) <= half && Math.Abs(p.Y - c.Y) <= half) return i;
            }
            return None;
        }

        internal static Cursor CursorFor(int handle)
        {
            switch (handle)
            {
                case 0: case 4: return Cursors.SizeNWSE;
                case 2: case 6: return Cursors.SizeNESW;
                case 1: case 5: return Cursors.SizeNS;
                case 3: case 7: return Cursors.SizeWE;
                case Body: return Cursors.SizeAll;
            }
            return Cursors.Cross;
        }

        internal static Frame ResizeFrame(Rectangle f, int handle, Point p)
        {
            int l = f.Left, t = f.Top, r = f.Right, b = f.Bottom;
            switch (handle)
            {
                case 0: l = p.X; t = p.Y; break;
                case 1: t = p.Y; break;
                case 2: r = p.X; t = p.Y; break;
                case 3: r = p.X; break;
                case 4: r = p.X; b = p.Y; break;
                case 5: b = p.Y; break;
                case 6: l = p.X; b = p.Y; break;
                case 7: l = p.X; break;
            }
            return new Frame(l, t, r, b);
        }

        internal static void Paint(Graphics g, Rectangle frame, float scale)
        {
            float w = Math.Max(1f, scale);
            using (Pen box = new Pen(Theme.Blue, w))
            {
                box.DashStyle = DashStyle.Dash;
                box.DashPattern = new float[] { 2f, 2f };
                g.DrawRectangle(box, frame);
            }
            using (Pen edge = new Pen(Color.Black, w))
            using (SolidBrush fill = new SolidBrush(Color.White))
            {
                for (int i = 0; i < HandleCount; i++)
                {
                    Rectangle r = HandleRect(frame, i, scale);
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(edge, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }
        }

        internal static Point Map(Point p, Frame from, Frame to)
        {
            int x = from.Width == 0
                ? p.X + (to.Left - from.Left)
                : to.Left + (int)Math.Round((p.X - from.Left) * (double)to.Width / from.Width);
            int y = from.Height == 0
                ? p.Y + (to.Top - from.Top)
                : to.Top + (int)Math.Round((p.Y - from.Top) * (double)to.Height / from.Height);
            return new Point(x, y);
        }

        internal static float ScaleFactor(Frame from, Frame to)
        {
            float sx = from.Width == 0 ? 1f : Math.Abs((float)to.Width / from.Width);
            float sy = from.Height == 0 ? 1f : Math.Abs((float)to.Height / from.Height);
            if (from.Width == 0) return sy;
            if (from.Height == 0) return sx;
            return Math.Min(sx, sy);
        }

        internal static float ScaleFactorX(Frame from, Frame to)
        {
            return from.Width == 0 ? 1f : Math.Abs((float)to.Width / from.Width);
        }

        internal static float ScaleFactorY(Frame from, Frame to)
        {
            return from.Height == 0 ? 1f : Math.Abs((float)to.Height / from.Height);
        }
    }
}
