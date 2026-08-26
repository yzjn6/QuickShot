using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace QuickShot
{
    internal enum ToolKind
    {
        Drag,
        Rect,
        Ellipse,
        Arrow,
        Number,
        Line,
        Text,
        Mosaic,
        Eraser
    }

    internal sealed class PaintContext
    {
        internal Bitmap Screen;
        internal Rectangle Vs;
        internal float Scale;
        internal List<ShapeBase> Shapes;
    }

    internal static class MosaicUtil
    {
        internal static int BlockSize(int strokeWidth)
        {
            return Math.Max(6, (int)Math.Round(strokeWidth / 3.0f, MidpointRounding.AwayFromZero));
        }

        internal static Bitmap Make(PaintContext ctx, Rectangle bounds, int block, ShapeBase exclude = null)
        {
            Rectangle b = Rectangle.Intersect(bounds,
                new Rectangle(ctx.Vs.Left, ctx.Vs.Top, ctx.Screen.Width, ctx.Screen.Height));
            if (b.Width <= 0 || b.Height <= 0) return null;

            Bitmap composed = new Bitmap(b.Width, b.Height);
            using (Graphics g = Graphics.FromImage(composed))
            {
                g.TranslateTransform(-b.X, -b.Y);
                Rectangle src = new Rectangle(b.X - ctx.Vs.Left, b.Y - ctx.Vs.Top, b.Width, b.Height);
                g.DrawImage(ctx.Screen, b, src, GraphicsUnit.Pixel);
                if (ctx.Shapes != null)
                {
                    foreach (ShapeBase s in ctx.Shapes)
                    {
                        if (s == exclude) continue;
                        if (!s.Undone) s.Paint(g, ctx);
                    }
                }
            }

            BlockAverage(composed, block);
            return composed;
        }

        static void BlockAverage(Bitmap bmp, int block)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                const int bpp = 4;
                int stride = bd.Stride;
                byte[] data = new byte[stride * bmp.Height];
                Marshal.Copy(bd.Scan0, data, 0, data.Length);

                for (int y = 0; y < bmp.Height; y += block)
                {
                    int yEnd = Math.Min(y + block, bmp.Height);
                    for (int x = 0; x < bmp.Width; x += block)
                    {
                        int xEnd = Math.Min(x + block, bmp.Width);
                        long bs = 0, gs = 0, rs = 0, als = 0, count = 0;
                        for (int yy = y; yy < yEnd; yy++)
                        {
                            int row = yy * stride;
                            for (int xx = x; xx < xEnd; xx++)
                            {
                                int i = row + xx * bpp;
                                bs += data[i];
                                gs += data[i + 1];
                                rs += data[i + 2];
                                als += data[i + 3];
                                count++;
                            }
                        }
                        if (count == 0) continue;
                        byte bb = (byte)(bs / count), gg = (byte)(gs / count),
                             rr = (byte)(rs / count), aa = (byte)(als / count);
                        for (int yy = y; yy < yEnd; yy++)
                        {
                            int row = yy * stride;
                            for (int xx = x; xx < xEnd; xx++)
                            {
                                int i = row + xx * bpp;
                                data[i] = bb;
                                data[i + 1] = gg;
                                data[i + 2] = rr;
                                data[i + 3] = aa;
                            }
                        }
                    }
                }
                Marshal.Copy(data, 0, bd.Scan0, data.Length);
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
        }
    }

    internal abstract class ShapeBase
    {
        internal Color Color;
        internal bool Undone;

        internal abstract void Paint(Graphics g, PaintContext ctx);
        internal abstract Rectangle GetBounds();
        internal abstract bool HitTest(Point p);
        internal virtual void Commit(PaintContext ctx) { }
        internal virtual bool IsEmpty() { return false; }

        internal virtual void DisposeResources() { }

        internal virtual Rectangle GetFrame() { return GetBounds(); }

        internal virtual void BeginTransform() { }

        internal virtual void Transform(Frame from, Frame to, PaintContext ctx) { }

        protected static Rectangle ToImage(Rectangle screenRect, Rectangle vs)
        {
            return new Rectangle(screenRect.X - vs.Left, screenRect.Y - vs.Top,
                screenRect.Width, screenRect.Height);
        }

        protected static Rectangle NormRect(Point a, Point b)
        {
            return new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        protected static double Dist(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        protected static double DistToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 == 0) return Dist(p, a);
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Dist(p, new Point((int)Math.Round(a.X + t * dx), (int)Math.Round(a.Y + t * dy)));
        }

        protected static bool RectRingHit(Rectangle r, Point p, int half)
        {
            Rectangle outer = r;
            outer.Inflate(half, half);
            if (!outer.Contains(p)) return false;
            Rectangle inner = r;
            inner.Inflate(-half, -half);
            return !inner.Contains(p);
        }
    }

    internal abstract class ShapeBrushBase : ShapeBase
    {
        internal List<Point> Points = new List<Point>();
        internal int BrushWidth;

        internal void AddPoint(Point p)
        {
            Points.Add(p);
        }

        internal GraphicsPath MakePath()
        {
            GraphicsPath path = new GraphicsPath();
            if (Points.Count > 1)
            {
                path.AddLines(Points.ToArray());
            }
            else if (Points.Count == 1)
            {
                float d = Math.Max(1f, BrushWidth);
                path.AddEllipse(Points[0].X - d / 2f, Points[0].Y - d / 2f, d, d);
            }
            return path;
        }

        internal override Rectangle GetBounds()
        {
            Rectangle r = PointBounds();
            if (r.IsEmpty) return Rectangle.Empty;
            r.Inflate(BrushWidth / 2 + 2, BrushWidth / 2 + 2);
            return r;
        }

        internal Rectangle PointBounds()
        {
            if (Points.Count == 0) return Rectangle.Empty;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Point p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new Rectangle(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }

        internal override Rectangle GetFrame()
        {
            return Points.Count == 0 ? Rectangle.Empty : PointBounds();
        }

        List<Point> _points0;

        internal override void BeginTransform()
        {
            _points0 = new List<Point>(Points);
        }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            if (_points0 == null || _points0.Count != Points.Count) return;
            for (int i = 0; i < Points.Count; i++) Points[i] = ShapeDragger.Map(_points0[i], from, to);
        }

        internal override bool HitTest(Point p)
        {
            double tol = BrushWidth / 2.0 + IconFont.Scale;
            if (Points.Count == 1) return Dist(p, Points[0]) <= tol;
            for (int i = 0; i + 1 < Points.Count; i++)
            {
                if (DistToSegment(p, Points[i], Points[i + 1]) <= tol) return true;
            }
            return false;
        }

        internal override bool IsEmpty()
        {
            return Points.Count == 0;
        }

        protected void FillStrokeWithBitmap(Graphics g, PaintContext ctx, Bitmap img, Point origin)
        {
            if (Points.Count == 0) return;
            using (GraphicsPath path = MakePath())
            using (Pen widen = new Pen(Color.Black, Math.Max(1f, BrushWidth * ctx.Scale)))
            {
                path.Widen(widen);
                GraphicsState state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                g.DrawImage(img, origin);
                g.Restore(state);
            }
        }
    }

}
