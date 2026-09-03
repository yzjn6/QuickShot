using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QuickShot
{
    internal sealed class ShapeRect : ShapeBase
    {
        internal Point Start, End;
        internal int StrokeWidth = 3;
        internal bool Filled;

        Rectangle R { get { return NormRect(Start, End); } }

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            Rectangle r = R;
            if (r.Width <= 0 || r.Height <= 0) return;
            if (Filled)
            {
                using (SolidBrush b = new SolidBrush(Color)) g.FillRectangle(b, r);
            }
            else
            {
                using (Pen p = new Pen(Color, Math.Max(1f, StrokeWidth * ctx.Scale))) g.DrawRectangle(p, r);
            }
        }

        internal override Rectangle GetBounds() { return R; }

        internal override bool HitTest(Point p)
        {
            Rectangle r = R;
            int half = (int)(StrokeWidth / 2f + IconFont.Scale);
            return RectRingHit(r, p, half);
        }

        internal override bool IsEmpty() { return R.Width < 3 && R.Height < 3; }

        Point _start0, _end0;

        internal override void BeginTransform() { _start0 = Start; _end0 = End; }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            Start = ShapeDragger.Map(_start0, from, to);
            End = ShapeDragger.Map(_end0, from, to);
        }
    }

    internal sealed class ShapeEllipse : ShapeBase
    {
        internal Point Start, End;
        internal int StrokeWidth = 3;
        internal bool Filled;

        Rectangle R { get { return NormRect(Start, End); } }

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            Rectangle r = R;
            if (r.Width <= 0 || r.Height <= 0) return;
            if (Filled)
            {
                using (SolidBrush b = new SolidBrush(Color)) g.FillEllipse(b, r);
            }
            else
            {
                using (Pen p = new Pen(Color, Math.Max(1f, StrokeWidth * ctx.Scale))) g.DrawEllipse(p, r);
            }
        }

        internal override Rectangle GetBounds() { return R; }

        internal override bool HitTest(Point p)
        {
            Rectangle r = R;
            if (r.Width <= 0 || r.Height <= 0) return false;
            float cx = (r.Left + r.Right) / 2f, cy = (r.Top + r.Bottom) / 2f;
            float rx = (r.Right - r.Left) / 2f, ry = (r.Bottom - r.Top) / 2f;
            float half = StrokeWidth / 2f + IconFont.Scale;
            float dx = p.X - cx, dy = p.Y - cy;
            float outerRx = rx + half, outerRy = ry + half;
            float innerRx = rx - half, innerRy = ry - half;
            bool insideOuter = (dx / outerRx) * (dx / outerRx) + (dy / outerRy) * (dy / outerRy) <= 1f;
            bool insideInner = innerRx > 0 && innerRy > 0 &&
                (dx / innerRx) * (dx / innerRx) + (dy / innerRy) * (dy / innerRy) <= 1f;
            return insideOuter && !insideInner;
        }

        internal override bool IsEmpty() { return R.Width < 3 && R.Height < 3; }

        Point _start0, _end0;

        internal override void BeginTransform() { _start0 = Start; _end0 = End; }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            Start = ShapeDragger.Map(_start0, from, to);
            End = ShapeDragger.Map(_end0, from, to);
        }
    }

    internal sealed class ShapeArrow : ShapeBase
    {
        internal Point Start, End;
        internal int ArrowSize = 18;
        internal bool Filled = true;

        GraphicsPath BuildPath(float scale)
        {
            float size = ArrowSize * scale;
            float sx = Start.X, sy = Start.Y, ex = End.X, ey = End.Y;
            double dx = ex - sx, dy = ey - sy;
            double len = Math.Sqrt(dx * dx + dy * dy);
            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            if (len < 1f) return path;

            double ux = dx / len, uy = dy / len;
            double vx = -uy, vy = ux;
            float v1 = size / 4f;
            float v2 = size * 2f / 3f;

            path.AddPolygon(new PointF[]
            {
                new PointF(sx, sy),
                new PointF((float)(ex - size * ux - v1 * vx), (float)(ey - size * uy - v1 * vy)),
                new PointF((float)(ex - (size + v1) * ux - v2 * vx), (float)(ey - (size + v1) * uy - v2 * vy)),
                new PointF(ex, ey),
                new PointF((float)(ex - (size + v1) * ux + v2 * vx), (float)(ey - (size + v1) * uy + v2 * vy)),
                new PointF((float)(ex - size * ux + v1 * vx), (float)(ey - size * uy + v1 * vy))
            });
            return path;
        }

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            using (GraphicsPath path = BuildPath(ctx.Scale))
            {
                if (path.PointCount == 0) return;
                if (Filled)
                {
                    using (SolidBrush b = new SolidBrush(Color)) g.FillPath(b, path);
                }
                else
                {
                    using (Pen p = new Pen(Color, Math.Max(1f, ctx.Scale))) g.DrawPath(p, path);
                }
            }
        }

        internal override Rectangle GetBounds()
        {
            Rectangle r = NormRect(Start, End);
            r.Inflate(ArrowSize, ArrowSize);
            return r;
        }

        internal override bool HitTest(Point p)
        {
            using (GraphicsPath path = BuildPath(IconFont.Scale)) return path.IsVisible(p);
        }

        internal override bool IsEmpty() { return Dist(Start, End) < 2; }

        internal override Rectangle GetFrame()
        {
            Rectangle r = NormRect(Start, End);
            return r.Width < 1 || r.Height < 1 ? Rectangle.Inflate(r, 1, 1) : r;
        }

        Point _start0, _end0;

        internal override void BeginTransform() { _start0 = Start; _end0 = End; }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            Start = ShapeDragger.Map(_start0, from, to);
            End = ShapeDragger.Map(_end0, from, to);
        }
    }

    internal sealed class ShapeNumber : ShapeBase
    {
        internal Point Center;
        internal float Radius = 16;
        internal int Value = 1;
        internal bool Filled = true;

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            float r = Radius * ctx.Scale;
            float cx = Center.X, cy = Center.Y;

            using (GraphicsPath path = BadgePath(cx, cy, r))
            {
                if (Filled)
                {
                    using (SolidBrush b = new SolidBrush(Color)) g.FillPath(b, path);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color, Math.Max(1f, ctx.Scale))) g.DrawPath(p, path);
                }
            }

            using (Font f = new Font("Microsoft YaHei", r, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush tb = new SolidBrush(Filled ? Color.White : Color))
            {
                RectangleF box = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
                g.DrawString(Value.ToString(), f, tb, box, IconFont.Center);
            }
        }

        static GraphicsPath BadgePath(float cx, float cy, float r)
        {
            GraphicsPath p = new GraphicsPath(FillMode.Winding);
            const float startDeg = 100f, sweep = 340f;
            p.AddArc(cx - r, cy - r, r * 2f, r * 2f, startDeg, sweep);

            float endDeg = startDeg + sweep;
            PointF arcStart = Polar(cx, cy, r, startDeg);
            PointF arcEnd = Polar(cx, cy, r, endDeg);
            PointF tip = new PointF(cx, cy + r + r / 3f);
            p.AddLine(arcEnd, tip);
            p.AddLine(tip, arcStart);
            return p;
        }

        static PointF Polar(float cx, float cy, float r, float degrees)
        {
            double a = degrees * Math.PI / 180.0;
            return new PointF(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a));
        }

        internal override Rectangle GetBounds()
        {
            float r = Radius * IconFont.Scale;
            return new Rectangle((int)(Center.X - r), (int)(Center.Y - r),
                (int)(r * 2f), (int)(r * 2f + r / 3f));
        }

        internal override bool HitTest(Point p)
        {
            return GetBounds().Contains(p);
        }

        Point _center0;
        float _radius0;

        internal override void BeginTransform() { _center0 = Center; _radius0 = Radius; }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            Center = ShapeDragger.Map(_center0, from, to);
            Radius = Math.Max(6f, _radius0 * ShapeDragger.ScaleFactor(from, to));
        }
    }

    internal sealed class ShapeText : ShapeBase
    {
        internal Point Position;
        internal string Text = "";
        internal float FontSize = 26;
        internal bool Bold;
        internal bool Italic;
        internal Size SizeCache;

        Font MakeFont(float scale)
        {
            FontStyle style = FontStyle.Regular;
            if (Bold) style |= FontStyle.Bold;
            if (Italic) style |= FontStyle.Italic;
            return new Font("Microsoft YaHei", Math.Max(1f, FontSize * scale), style, GraphicsUnit.Pixel);
        }

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            if (string.IsNullOrEmpty(Text)) return;
            using (Font f = MakeFont(ctx.Scale))
            using (SolidBrush b = new SolidBrush(Color))
            {
                g.DrawString(Text, f, b, Position);
            }
        }

        internal override void Commit(PaintContext ctx)
        {
            using (Bitmap tmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tmp))
            using (Font f = MakeFont(ctx.Scale))
            {
                Size sz = TextRenderer.MeasureText(g, Text, f);
                SizeCache = new Size(sz.Width, sz.Height);
            }
        }

        internal override Rectangle GetBounds()
        {
            Size s = SizeCache.IsEmpty ? new Size((int)(FontSize * 2), (int)(FontSize * 2)) : SizeCache;
            int pad = (int)Math.Round(10f * IconFont.Scale);
            return new Rectangle(Position.X - pad, Position.Y - pad, s.Width + pad * 2, s.Height + pad * 2);
        }

        internal override bool HitTest(Point p)
        {
            return GetBounds().Contains(p);
        }

        internal override bool IsEmpty() { return string.IsNullOrEmpty(Text); }

        internal override Rectangle GetFrame()
        {
            Size s = SizeCache.IsEmpty ? new Size((int)(FontSize * 2), (int)(FontSize * 2)) : SizeCache;
            return new Rectangle(Position, s);
        }

        float _fontSize0;

        internal override void BeginTransform() { _fontSize0 = FontSize; }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            float sx = from.Width == 0 ? 1f : Math.Abs((float)to.Width / from.Width);
            float sy = from.Height == 0 ? 1f : Math.Abs((float)to.Height / from.Height);
            float scale;
            if (to.Width == from.Width) scale = sy;
            else if (to.Height == from.Height) scale = sx;
            else scale = Math.Min(sx, sy);
            FontSize = Math.Max(1f, _fontSize0 * scale);

            Commit(ctx);

            Position = new Point(
                to.Width >= 0 ? to.Left : to.Right,
                to.Height >= 0 ? to.Top : to.Bottom);
        }
    }

    internal sealed class ShapeLine : ShapeBrushBase
    {
        internal bool SemiTransparent;

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            if (Points.Count == 0) return;
            Color c = SemiTransparent ? Color.FromArgb(128, Color) : Color;
            using (Pen p = new Pen(c, Math.Max(1f, BrushWidth * ctx.Scale)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                using (GraphicsPath path = MakePath()) g.DrawPath(p, path);
            }
        }
    }

    internal sealed class ShapeMosaic : ShapeBrushBase
    {
        internal bool RectMode = true;
        internal Point RectStart, RectEnd;
        Bitmap _mosaic;
        Point _mosaicOrigin;

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            if (_mosaic == null)
            {
                float w = Math.Max(1f, BrushWidth * ctx.Scale);
                if (RectMode)
                {
                    Rectangle r = NormRect(RectStart, RectEnd);
                    using (SolidBrush b = new SolidBrush(Theme.Placeholder)) g.FillRectangle(b, r);
                }
                else
                {
                    using (Pen p = new Pen(Theme.Placeholder, w))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        using (GraphicsPath path = MakePath()) g.DrawPath(p, path);
                    }
                }
                return;
            }
            if (RectMode)
            {
                Rectangle r = NormRect(RectStart, RectEnd);
                Rectangle src = new Rectangle(r.X - _mosaicOrigin.X, r.Y - _mosaicOrigin.Y, r.Width, r.Height);
                if (src.Width <= 0 || src.Height <= 0) return;
                g.DrawImage(_mosaic, r, src, GraphicsUnit.Pixel);
            }
            else
            {
                FillStrokeWithBitmap(g, ctx, _mosaic, _mosaicOrigin);
            }
        }

        internal override void Commit(PaintContext ctx)
        {
            Rectangle b = RectMode ? NormRect(RectStart, RectEnd) : GetBounds();
            if (b.Width <= 0 || b.Height <= 0) return;
            int block = MosaicUtil.BlockSize(BrushWidth);
            b.Inflate(block, block);
            Bitmap made = MosaicUtil.Make(ctx, b, block, this);
            ReleaseMosaic();
            _mosaicOrigin = b.Location;
            _mosaic = made;
        }

        void ReleaseMosaic()
        {
            Bitmap cur = _mosaic;
            if (object.ReferenceEquals(_mosaic0, cur)) _mosaic0 = null;
            _mosaic = null;
            if (cur != null) cur.Dispose();
        }

        internal override void DisposeResources()
        {
            Bitmap cur = _mosaic;
            Bitmap old = _mosaic0;
            _mosaic = null;
            _mosaic0 = null;
            if (cur != null) cur.Dispose();
            if (old != null && !object.ReferenceEquals(old, cur)) old.Dispose();
        }

        internal override Rectangle GetBounds()
        {
            return RectMode ? NormRect(RectStart, RectEnd) : base.GetBounds();
        }

        internal override Rectangle GetFrame()
        {
            return RectMode ? NormRect(RectStart, RectEnd) : base.GetFrame();
        }

        Point _rs0, _re0;
        Bitmap _mosaic0;
        Point _origin0;

        internal override void BeginTransform()
        {
            base.BeginTransform();
            _rs0 = RectStart;
            _re0 = RectEnd;
            _mosaic0 = _mosaic;
            _origin0 = _mosaicOrigin;
        }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            bool pureMove = to.Width == from.Width && to.Height == from.Height;
            if (RectMode)
            {
                RectStart = ShapeDragger.Map(_rs0, from, to);
                RectEnd = ShapeDragger.Map(_re0, from, to);
            }
            else
            {
                base.Transform(from, to, ctx);
            }
            if (pureMove && _mosaic0 != null)
            {
                _mosaic = _mosaic0;
                _mosaicOrigin = new Point(_origin0.X + (to.Left - from.Left), _origin0.Y + (to.Top - from.Top));
                return;
            }
            ReleaseMosaic();
            Commit(ctx);
        }

        internal override bool HitTest(Point p)
        {
            if (!RectMode) return base.HitTest(p);
            int half = (int)(BrushWidth / 2f + IconFont.Scale);
            return RectRingHit(GetBounds(), p, half);
        }

        internal override bool IsEmpty()
        {
            if (RectMode)
            {
                Rectangle r = NormRect(RectStart, RectEnd);
                return r.Width < 3 && r.Height < 3;
            }
            return base.IsEmpty();
        }
    }

    internal sealed class ShapeEraser : ShapeBrushBase
    {
        internal bool RectMode = true;
        internal Point RectStart, RectEnd;
        bool _erasing;

        internal override void Commit(PaintContext ctx)
        {
            if (RectMode)
            {
                Rectangle r = NormRect(RectStart, RectEnd);
                if (r.Width > 0 && r.Height > 0) _erasing = true;
            }
            else if (Points.Count > 1) _erasing = true;
        }

        internal override void Paint(Graphics g, PaintContext ctx)
        {
            if (!_erasing)
            {
                if (RectMode)
                {
                    Rectangle r = NormRect(RectStart, RectEnd);
                    using (SolidBrush b = new SolidBrush(Theme.Placeholder)) g.FillRectangle(b, r);
                }
                else
                {
                    float w = Math.Max(1f, BrushWidth * ctx.Scale);
                    using (Pen p = new Pen(Theme.Placeholder, w))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        using (GraphicsPath path = MakePath()) g.DrawPath(p, path);
                    }
                }
                return;
            }
            if (RectMode)
            {
                Rectangle r = NormRect(RectStart, RectEnd);
                if (r.Width <= 0 || r.Height <= 0) return;
                g.DrawImage(ctx.Screen, r, ToImage(r, ctx.Vs), GraphicsUnit.Pixel);
            }
            else
            {
                FillStrokeWithBitmap(g, ctx, ctx.Screen, ctx.Vs.Location);
            }
        }

        internal override Rectangle GetBounds()
        {
            return RectMode ? NormRect(RectStart, RectEnd) : base.GetBounds();
        }

        internal override Rectangle GetFrame()
        {
            return RectMode ? NormRect(RectStart, RectEnd) : base.GetFrame();
        }

        internal override bool HitTest(Point p)
        {
            if (!RectMode) return base.HitTest(p);
            int half = (int)(BrushWidth / 2f + IconFont.Scale);
            return RectRingHit(GetBounds(), p, half);
        }

        internal override bool IsEmpty()
        {
            if (RectMode)
            {
                Rectangle r = NormRect(RectStart, RectEnd);
                return r.Width < 3 && r.Height < 3;
            }
            return base.IsEmpty();
        }

        Point _rs0, _re0;

        internal override void BeginTransform()
        {
            base.BeginTransform();
            _rs0 = RectStart;
            _re0 = RectEnd;
        }

        internal override void Transform(Frame from, Frame to, PaintContext ctx)
        {
            if (RectMode)
            {
                RectStart = ShapeDragger.Map(_rs0, from, to);
                RectEnd = ShapeDragger.Map(_re0, from, to);
            }
            else
            {
                base.Transform(from, to, ctx);
            }
        }
    }
}
