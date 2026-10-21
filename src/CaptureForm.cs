using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QuickShot
{
    internal sealed class ToolbarPanel : Panel
    {
        internal ToolbarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    internal sealed class CaptureForm : Form
    {
        readonly Bitmap _screen;
        readonly Rectangle _vs;
        readonly List<ShapeBase> _shapes = new List<ShapeBase>();
        readonly PaintContext _ctx;

        bool _selecting;
        bool _selected;
        Point _startPt;
        Point _curPt;
        Rectangle _sel;

        ToolKind _tool = ToolKind.Arrow;
        Color _color = Color.FromArgb(0xCF, 0x13, 0x22);
        int _strokeWidth = 3;
        int _arrowSize = 18;
        int _brushWidth = 3;
        int _fontSize = 26;
        int _mosaicBrush = 32;
        bool _filled;
        bool _semiTransparent;
        bool _bold;
        bool _italic;
        bool _rectMode;
        bool _toolActive;

        static float _numberRadius = 16f;

        ShapeBase _drawing;
        ShapeBase _hover;
        ShapeBase _active;
        int _dragHandle = ShapeDragger.None;
        Rectangle _dragFrame0;
        Rectangle _dragFramePaint;
        Point _dragStart;
        Panel _toolbar;
        FlowLayoutPanel _mainBar;
        FlowLayoutPanel _subBar;

        ToolTip _tip;
        TextBox _textBox;
        bool _committingText;
        List<IconButton> _toolBtns;
        List<ColorCell> _colorCells;

        SolidBrush _brushDim, _brushSizeBg, _brushSizeFg, _brushHintBg, _brushHintFg;
        SolidBrush _brushPixBg, _brushPixFg, _brushCross;
        Pen _penPixBorder;
        Font _fontSizeInfo, _fontHint, _fontPix;
        Font _textFont;

        float _subArrowX;
        Rectangle _lastPixRect;
        bool _rightPending;

        internal CaptureForm(Bitmap screen, Rectangle virtualScreen)
        {
            _screen = screen;
            _vs = virtualScreen;
            _ctx = new PaintContext
            {
                Screen = screen,
                Vs = virtualScreen,
                Scale = IconFont.Scale,
                Shapes = _shapes
            };

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _curPt = Control.MousePosition;
        }

        Point ToLocal(Point screenPt)
        {
            return new Point(screenPt.X - _vs.Left, screenPt.Y - _vs.Top);
        }

        int NextNumber()
        {
            int max = 0;
            foreach (ShapeBase s in _shapes)
            {
                ShapeNumber n = s as ShapeNumber;
                if (n != null && !n.Undone && n.Value > max) max = n.Value;
            }
            return max + 1;
        }

        ShapeBase CreateShape(Point pt)
        {
            if (!_toolActive) return null;
            switch (_tool)
            {
                case ToolKind.Rect:
                    return new ShapeRect { Start = pt, End = pt, Color = _color, StrokeWidth = _strokeWidth, Filled = _filled };
                case ToolKind.Ellipse:
                    return new ShapeEllipse { Start = pt, End = pt, Color = _color, StrokeWidth = _strokeWidth, Filled = _filled };
                case ToolKind.Arrow:
                    return new ShapeArrow { Start = pt, End = pt, Color = _color, ArrowSize = _arrowSize, Filled = _filled };
                case ToolKind.Number:
                    return new ShapeNumber { Center = pt, Radius = _numberRadius, Value = NextNumber(), Color = _color, Filled = _filled };
                case ToolKind.Line:
                    return NewBrush(new ShapeLine { Color = _color, BrushWidth = _brushWidth, SemiTransparent = _semiTransparent }, pt);
                case ToolKind.Mosaic:
                    {
                        if (_rectMode) return new ShapeMosaic { RectMode = true, BrushWidth = _mosaicBrush, RectStart = pt, RectEnd = pt };
                        return NewBrush(new ShapeMosaic { RectMode = false, BrushWidth = _mosaicBrush }, pt);
                    }
                case ToolKind.Eraser:
                    {
                        if (_rectMode) return new ShapeEraser { RectMode = true, RectStart = pt, RectEnd = pt };
                        return NewBrush(new ShapeEraser { RectMode = false, BrushWidth = _mosaicBrush }, pt);
                    }
                case ToolKind.Drag:
                    return null;
            }
            return null;
        }

        static ShapeBase NewBrush(ShapeBrushBase brush, Point pt)
        {
            brush.AddPoint(pt);
            if ((Control.ModifierKeys & Keys.Shift) != 0) brush.AddPoint(pt);
            return brush;
        }

        void UpdateDrawing(Point pt, bool shift)
        {
            if (_drawing == null) return;

            ShapeRect rect = _drawing as ShapeRect;
            if (rect != null) { rect.End = shift ? Square(rect.Start, pt) : pt; return; }

            ShapeEllipse ellipse = _drawing as ShapeEllipse;
            if (ellipse != null) { ellipse.End = shift ? Square(ellipse.Start, pt) : pt; return; }

            ShapeArrow arrow = _drawing as ShapeArrow;
            if (arrow != null) { arrow.End = shift ? Snap45(arrow.Start, pt) : pt; return; }

            ShapeMosaic mosaic = _drawing as ShapeMosaic;
            if (mosaic != null && mosaic.RectMode) { mosaic.RectEnd = shift ? Square(mosaic.RectStart, pt) : pt; return; }

            ShapeEraser eraser = _drawing as ShapeEraser;
            if (eraser != null && eraser.RectMode) { eraser.RectEnd = shift ? Square(eraser.RectStart, pt) : pt; return; }

            ShapeBrushBase brush = _drawing as ShapeBrushBase;
            if (brush != null)
            {
                if (shift && brush.Points.Count > 0) brush.Points[brush.Points.Count - 1] = pt;
                else brush.AddPoint(pt);
            }
        }

        static Point Square(Point anchor, Point pt)
        {
            int dx = pt.X - anchor.X, dy = pt.Y - anchor.Y;
            int adx = Math.Abs(dx), ady = Math.Abs(dy);
            if (adx > ady) dy = dy < 0 ? -adx : adx;
            else dx = dx < 0 ? -ady : ady;
            return new Point(anchor.X + dx, anchor.Y + dy);
        }

        static Point Snap45(Point anchor, Point pt)
        {
            return ArrowGeometry.ConstrainToEightDirections(anchor, pt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImage(_screen, 0, 0);

            if (_brushDim == null) _brushDim = new SolidBrush(Color.FromArgb(117, 0, 0, 0));
            g.FillRectangle(_brushDim, ClientRectangle);

            Rectangle sel = _selected ? _sel : (_selecting ? NormRect(_startPt, _curPt) : Rectangle.Empty);
            if (sel.Width <= 0 || sel.Height <= 0)
            {
                DrawHint(g);
                if (!_selecting) DrawMagnifier(g, _curPt);
                return;
            }

            Rectangle src = new Rectangle(sel.X - _vs.Left, sel.Y - _vs.Top, sel.Width, sel.Height);
            Rectangle dst = new Rectangle(ToLocal(sel.Location), sel.Size);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_screen, dst, src, GraphicsUnit.Pixel);
            g.InterpolationMode = InterpolationMode.Default;

            g.TranslateTransform(-_vs.Left, -_vs.Top);
            foreach (ShapeBase s in _shapes)
            {
                if (!s.Undone) s.Paint(g, _ctx);
            }
            if (_drawing != null) _drawing.Paint(g, _ctx);
            if (_active != null && !_active.Undone) ShapeDragger.Paint(g, FrameForPaint(_active), IconFont.Scale);
            else if (_hover != null) DrawHoverMark(g, _hover);
            if (_textBox != null) DrawTextEditingBox(g);
            g.ResetTransform();

            using (Pen border = new Pen(Theme.Blue, 2f * IconFont.Scale))
            {
                g.DrawRectangle(border, dst.X, dst.Y, dst.Width - 1, dst.Height - 1);
            }

            DrawSizeInfo(g, dst);
        }

        void DrawSizeInfo(Graphics g, Rectangle dst)
        {
            string info = string.Format("X:{0} Y:{1} R:{2} B:{3} W:{4} H:{5}",
                dst.X + _vs.Left, dst.Y + _vs.Top,
                dst.Right + _vs.Left, dst.Bottom + _vs.Top, dst.Width, dst.Height);
            if (_fontSizeInfo == null) _fontSizeInfo = new Font("Microsoft YaHei", 10f * IconFont.Scale, GraphicsUnit.Pixel);
            if (_brushSizeBg == null) _brushSizeBg = new SolidBrush(Color.FromArgb(117, 0, 0, 0));
            if (_brushSizeFg == null) _brushSizeFg = new SolidBrush(Color.White);
            SizeF sz = g.MeasureString(info, _fontSizeInfo);
            int margin = (int)Math.Round(5f * IconFont.Scale);
            int pad = (int)Math.Round(3f * IconFont.Scale);
            float tx = dst.X;
            float ty = dst.Y - margin - sz.Height - pad * 2;
            if (ty < 0) { ty = dst.Y + margin / 2f; tx = dst.X + margin; }
            g.FillRectangle(_brushSizeBg, tx - pad, ty - pad, sz.Width + pad * 2, sz.Height + pad * 2);
            g.DrawString(info, _fontSizeInfo, _brushSizeFg, tx, ty);
        }

        void DrawHint(Graphics g)
        {
            string tip = "拖拽框选区域    ·    右键/ESC 取消    ·    Ctrl+H 复制颜色    ·    双击或 Enter 复制到剪贴板";
            if (_fontHint == null) _fontHint = new Font("Microsoft YaHei", 12f * IconFont.Scale, GraphicsUnit.Pixel);
            if (_brushHintBg == null) _brushHintBg = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
            if (_brushHintFg == null) _brushHintFg = new SolidBrush(Color.White);
            SizeF sz = g.MeasureString(tip, _fontHint);
            float x = (ClientSize.Width - sz.Width) / 2;
            float y = 40;
            g.FillRectangle(_brushHintBg, x - 12, y - 8, sz.Width + 24, sz.Height + 16);
            g.DrawString(tip, _fontHint, _brushHintFg, x, y);
        }

        void DrawTextEditingBox(Graphics g)
        {
            Size sz = MeasureTextSize(_textBox.Text, _textBox.Font);
            DrawTextDashBox(g, new Point(_textBox.Location.X + _vs.Left, _textBox.Location.Y + _vs.Top), sz, _color);
        }

        void DrawTextDashBox(Graphics g, Point pos, Size textSize, Color color)
        {
            int pad = (int)Math.Round(6f * IconFont.Scale);
            Rectangle r = new Rectangle(pos.X - pad, pos.Y - pad, textSize.Width + pad * 2, textSize.Height + pad * 2);
            float w = Math.Max(1f, IconFont.Scale);
            using (Pen p = new Pen(color, w))
            {
                p.DashStyle = DashStyle.Dash;
                p.DashPattern = new float[] { 2f * IconFont.Scale / w, 2f * IconFont.Scale / w };
                g.DrawRectangle(p, r);
            }
        }

        void DrawHoverMark(Graphics g, ShapeBase s)
        {
            ShapeText t = s as ShapeText;
            if (t != null)
            {
                Size sz = t.SizeCache.IsEmpty ? new Size((int)(t.FontSize * 2), (int)(t.FontSize * 2)) : t.SizeCache;
                DrawTextDashBox(g, t.Position, sz, t.Color);
                return;
            }

            Rectangle r = s.GetBounds();
            r.Inflate(6, 6);
            using (Pen p = new Pen(Theme.Hover, 1f))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawRectangle(p, r);
            }
        }

        internal const int PixSrcW = 50, PixSrcH = 30, PixScale = 3, PixTextH = 112;

        Rectangle MagnifierRect(Point pt)
        {
            int imgW = PixSrcW * PixScale;
            int span = (int)Math.Round(10f * IconFont.Scale);
            int textH = (int)Math.Round(PixTextH * IconFont.Scale);
            int winH = PixSrcH * PixScale + textH;
            int lx = pt.X - _vs.Left, ly = pt.Y - _vs.Top;
            int px = lx + span;
            if (px + imgW > ClientSize.Width) px = lx - span - imgW;
            int py = ly + span;
            if (py + winH > ClientSize.Height) py = ly - span - winH;
            if (px < 0) px = 0;
            if (py < 0) py = 0;
            return new Rectangle(px, py, imgW, winH);
        }

        void DrawMagnifier(Graphics g, Point pt)
        {
            int imgW = PixSrcW * PixScale;
            int imgH = PixSrcH * PixScale;
            int textH = (int)Math.Round(PixTextH * IconFont.Scale);
            int winH = imgH + textH;

            int lx = pt.X - _vs.Left, ly = pt.Y - _vs.Top;

            int sx = lx - PixSrcW / 2, sy = ly - PixSrcH / 2;
            int sw = PixSrcW, sh = PixSrcH, dx = 0, dy = 0;
            if (sx < 0) { dx = -sx * PixScale; sw += sx; sx = 0; }
            if (sy < 0) { dy = -sy * PixScale; sh += sy; sy = 0; }
            if (sx + sw > _screen.Width) sw = _screen.Width - sx;
            if (sy + sh > _screen.Height) sh = _screen.Height - sy;
            if (sw <= 0 || sh <= 0) return;

            int px = MagnifierRect(pt).X;
            int py = MagnifierRect(pt).Y;

            if (_brushPixBg == null) _brushPixBg = new SolidBrush(Color.FromArgb(153, 0, 0, 0));
            g.FillRectangle(_brushPixBg, px, py, imgW, winH);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.DrawImage(_screen, new Rectangle(px + dx, py + dy, sw * PixScale, sh * PixScale),
                new Rectangle(sx, sy, sw, sh), GraphicsUnit.Pixel);
            g.PixelOffsetMode = PixelOffsetMode.Default;

            if (_penPixBorder == null) _penPixBorder = new Pen(Color.Black, 2f);
            g.DrawRectangle(_penPixBorder, px, py, imgW, winH);

            int c = (int)Math.Round(5f * IconFont.Scale);
            if (_brushCross == null) _brushCross = new SolidBrush(Color.FromArgb(128, 26, 128, 255));
            {
                int cx = px + imgW / 2, cy = py + imgH / 2;
                g.FillRectangle(_brushCross, px, cy - c, cx - px - c, c * 2);
                g.FillRectangle(_brushCross, cx + c, cy - c, px + imgW - cx - c, c * 2);
                g.FillRectangle(_brushCross, cx - c, py, c * 2, cy - py - c);
                g.FillRectangle(_brushCross, cx - c, cy + c, c * 2, py + imgH - cy - c);
            }

            Color col = GetPixelAt(pt);
            float fs = 15f * IconFont.Scale;
            int pad = (int)Math.Round(9f * IconFont.Scale);
            if (_fontPix == null) _fontPix = new Font("Microsoft YaHei", fs, GraphicsUnit.Pixel);
            if (_brushPixFg == null) _brushPixFg = new SolidBrush(Color.White);
            {
                string[] lines = new string[]
                {
                    "HEX (Ctrl+H) : #" + col.R.ToString("X2") + col.G.ToString("X2") + col.B.ToString("X2"),
                    "RGB (Ctrl+R) : " + col.R + "," + col.G + "," + col.B,
                    "CMYK (Ctrl+K) : " + CmykText(col),
                    "POS (Ctrl+P) : X:" + pt.X + " Y:" + pt.Y
                };
                float ty = py + imgH + pad;
                foreach (string line in lines)
                {
                    g.DrawString(line, _fontPix, _brushPixFg, px + pad, ty);
                    ty += fs + pad;
                }
            }

            _lastPixRect = new Rectangle(px, py, imgW, winH);
        }

        Color GetPixelAt(Point screenPt)
        {
            int x = screenPt.X - _vs.Left;
            int y = screenPt.Y - _vs.Top;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= _screen.Width) x = _screen.Width - 1;
            if (y >= _screen.Height) y = _screen.Height - 1;
            return _screen.GetPixel(x, y);
        }

        static Rectangle NormRect(Point a, Point b)
        {
            return new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Point pt = new Point(e.X + _vs.Left, e.Y + _vs.Top);
            if (e.Button == MouseButtons.Right)
            {
                _rightPending = true;
                return;
            }

            if (!_selected)
            {
                _selecting = true;
                _startPt = pt;
                _curPt = pt;
                if (!_lastPixRect.IsEmpty) { Invalidate(_lastPixRect); _lastPixRect = Rectangle.Empty; }
                return;
            }

            if (_textBox != null)
            {
                if (new Rectangle(_textBox.Location, _textBox.Size).Contains(ToLocal(pt)))
                {
                    _textBox.Focus();
                    return;
                }
                CommitText();
            }

            if (_active != null)
            {
                int handle = ShapeDragger.HitHandle(_active.GetFrame(), pt, IconFont.Scale);
                if (handle != ShapeDragger.None) { BeginDrag(_active, handle, pt); return; }
            }

            ShapeBase hit = HitTest(pt);
            if (hit != null) { BeginDrag(hit, ShapeDragger.Body, pt); return; }

            _active = null;

            if (_tool == ToolKind.Text)
            {
                StartTextInput(pt);
                return;
            }

            _drawing = CreateShape(pt);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point pt = new Point(e.X + _vs.Left, e.Y + _vs.Top);
            Point old = _curPt;
            _curPt = pt;

            if (_selecting)
            {
                Invalidate();
                return;
            }

            if (_drawing != null)
            {
                UpdateDrawing(pt, (Control.ModifierKeys & Keys.Shift) != 0);
                Invalidate();
                return;
            }

            if (!_selected)
            {
                Invalidate();
                return;
            }

            if (_dragHandle >= 0 && _active != null)
            {
                Frame baseFrame = Frame.FromRect(_dragFrame0);
                Frame to;
                if (_dragHandle == ShapeDragger.Body)
                {
                    to = baseFrame.Offset(pt.X - _dragStart.X, pt.Y - _dragStart.Y);
                }
                else
                {
                    to = ShapeDragger.ResizeFrame(_dragFrame0, _dragHandle, pt);
                }
                _active.Transform(baseFrame, to, _ctx);
                _dragFramePaint = to.Bounds;
                Invalidate();
                return;
            }

            int hoverHandle = _active == null
                ? ShapeDragger.None
                : ShapeDragger.HitHandle(_active.GetFrame(), pt, IconFont.Scale);
            ShapeBase hit = hoverHandle == ShapeDragger.None ? HitTest(pt) : null;
            Cursor desired;
            if (hoverHandle != ShapeDragger.None) desired = ShapeDragger.CursorFor(hoverHandle);
            else if (hit != null) desired = hit == _active ? Cursors.SizeAll : Cursors.Hand;
            else desired = IsDragMode ? Cursors.Default : Cursors.Cross;
            if (Cursor != desired) Cursor = desired;
            if (hit != _hover)
            {
                _hover = hit;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _rightPending)
            {
                _rightPending = false;
                if (CanFallBackToDrag()) { EnterDragMode(); Invalidate(); return; }
                if (_selected) { BackToViewing(); return; }
                Quit();
                return;
            }
            if (e.Button == MouseButtons.Right) return;

            if (_dragHandle >= 0)
            {
                _dragHandle = ShapeDragger.None;
                _dragFramePaint = Rectangle.Empty;
                if (_active != null) _active.Commit(_ctx);
                Invalidate();
                return;
            }

            if (_selecting)
            {
                _selecting = false;
                _sel = NormRect(_startPt, new Point(e.X + _vs.Left, e.Y + _vs.Top));
                if (_sel.Width < 4 || _sel.Height < 4) { Invalidate(); return; }
                _selected = true;
                ShowToolbar();
                Invalidate();
                return;
            }

            if (_drawing != null)
            {
                _drawing.Commit(_ctx);
                if (_drawing.IsEmpty())
                {
                    _drawing.DisposeResources();
                    _drawing = null;
                }
                else
                {
                    DropUndone();
                    _shapes.Add(_drawing);
                    _active = _drawing;
                    _drawing = null;
                }
                Invalidate();
            }
        }

        void BeginDrag(ShapeBase s, int handle, Point pt)
        {
            _active = s;
            _hover = s;
            _dragHandle = handle;
            Rectangle f = s.GetFrame();
            if (f.Width < 2 || f.Height < 2)
                f = new Rectangle(f.X, f.Y, Math.Max(2, f.Width), Math.Max(2, f.Height));
            _dragFrame0 = f;
            _dragFramePaint = f;
            _dragStart = pt;
            s.BeginTransform();
            Cursor = ShapeDragger.CursorFor(handle);
            Invalidate();
        }

        Rectangle FrameForPaint(ShapeBase s)
        {
            if (_dragHandle >= 0 && s is ShapeText && _dragFramePaint != Rectangle.Empty) return _dragFramePaint;
            return s.GetFrame();
        }

        void BeginEditText(ShapeText t)
        {
            _shapes.Remove(t);
            _active = null;
            _hover = null;
            _color = t.Color;
            _fontSize = Math.Max(16, Math.Min(66, (int)Math.Round(t.FontSize)));
            _bold = t.Bold;
            _italic = t.Italic;
            StartTextInput(t.Position);
            _textBox.Text = t.Text;
            LayoutTextBox();
            _textBox.SelectionStart = _textBox.TextLength;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ShapeNumber n = _hover as ShapeNumber;
            if (_selected && n != null)
            {
                float minR = 6f * IconFont.Scale;
                if (e.Delta < 0)
                {
                    if (n.Radius <= minR) return;
                    n.Radius -= IconFont.Scale;
                }
                else
                {
                    n.Radius += IconFont.Scale;
                }
                _numberRadius = n.Radius;
                Invalidate();
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (!_selected || e.Button != MouseButtons.Left) return;
            Point pt = new Point(e.X + _vs.Left, e.Y + _vs.Top);
            ShapeText text = HitTest(pt) as ShapeText;
            if (text != null) { BeginEditText(text); return; }
            CopyAndQuit();
        }

        ShapeBase HitTest(Point p)
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                ShapeBase s = _shapes[i];
                if (!s.Undone && s.HitTest(p)) return s;
            }
            return null;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_textBox != null) return;
                if (_drawing != null) { _drawing.DisposeResources(); _drawing = null; Invalidate(); return; }
                if (CanFallBackToDrag()) { EnterDragMode(); Invalidate(); return; }
                if (_selected) { BackToViewing(); return; }
                Quit();
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                if (_textBox != null) return;
                if (_selected) CopyAndQuit();
            }
            else if (e.Control && e.KeyCode == Keys.Z && e.Shift) Redo();
            else if (e.Control && e.KeyCode == Keys.Z) Undo();
            else if (e.Control && e.KeyCode == Keys.Y) Redo();
            else if (e.Control && e.KeyCode == Keys.S)
            {
                if (_selected) SaveToFile();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                ShapeBase target = _active ?? _hover;
                if (target != null)
                {
                    target.DisposeResources();
                    _shapes.Remove(target);
                    if (target == _hover) _hover = null;
                    if (target == _active) _active = null;
                    Cursor = Cursors.Cross;
                    Invalidate();
                }
            }
            else if (!_selected && e.Control && e.KeyCode == Keys.H)
            {
                Color c = GetPixelAt(_curPt);
                CopyText("#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
                Quit();
            }
            else if (!_selected && e.Control && e.KeyCode == Keys.R)
            {
                Color c = GetPixelAt(_curPt);
                CopyText("rgb(" + c.R + "," + c.G + "," + c.B + ")");
                Quit();
            }
            else if (!_selected && e.Control && e.KeyCode == Keys.K)
            {
                Color c = GetPixelAt(_curPt);
                CopyText("cmyk(" + CmykText(c) + ")");
                Quit();
            }
            else if (!_selected && e.Control && e.KeyCode == Keys.P)
            {
                CopyText(_curPt.X + "," + _curPt.Y);
                Quit();
            }
        }

        internal static string CmykText(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double k = 1.0 - Math.Max(r, Math.Max(g, b));
            if (k >= 1.0) return "0,0,0,100";
            double cm = (1.0 - r - k) / (1.0 - k);
            double mm = (1.0 - g - k) / (1.0 - k);
            double ym = (1.0 - b - k) / (1.0 - k);
            return Math.Round(cm * 100) + "," + Math.Round(mm * 100) + "," +
                   Math.Round(ym * 100) + "," + Math.Round(k * 100);
        }

        static void CopyText(string text)
        {
            try { Clipboard.SetText(text); }
            catch { }
        }

        void Undo()
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                if (!_shapes[i].Undone)
                {
                    _shapes[i].Undone = true;
                    if (_shapes[i] == _hover) _hover = null;
                    if (_shapes[i] == _active) _active = null;
                    Cursor = Cursors.Cross;
                    Invalidate();
                    return;
                }
            }
        }

        void Redo()
        {
            foreach (ShapeBase s in _shapes)
            {
                if (s.Undone)
                {
                    s.Undone = false;
                    Invalidate();
                    return;
                }
            }
        }

        void DropUndone()
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                if (_shapes[i].Undone)
                {
                    _shapes[i].DisposeResources();
                    _shapes.RemoveAt(i);
                }
                else break;
            }
        }

        void StartTextInput(Point screenPt)
        {
            if (_textBox != null) CommitText();

            _textBox = new TextBox();
            _textBox.Multiline = true;
            _textBox.AcceptsReturn = true;
            _textBox.WordWrap = false;
            SyncTextBoxStyle();
            _textBox.BackColor = Color.White;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Location = ToLocal(screenPt);
            _textBox.KeyDown += TextBoxKeyDown;
            _textBox.TextChanged += TextBoxTextChanged;
            _textBox.Leave += TextBoxLeave;
            _textBox.ContextMenu = new ContextMenu();
            Controls.Add(_textBox);
            LayoutTextBox();
            _textBox.Focus();
        }

        static Size MeasureTextSize(string text, Font font)
        {
            return TextRenderer.MeasureText(string.IsNullOrEmpty(text) ? " " : text, font);
        }

        void LayoutTextBox()
        {
            if (_textBox == null) return;
            int pad = Math.Max(1, (int)Math.Round(2f * IconFont.Scale));
            Size sz = MeasureTextSize(_textBox.Text, _textBox.Font);
            int w = Math.Max(sz.Width, _textBox.Font.Height) + pad * 2;
            int maxW = ClientSize.Width - _textBox.Left;
            if (maxW > 0 && w > maxW) w = maxW;
            _textBox.Size = new Size(w, sz.Height + pad * 2);
        }

        void TextBoxTextChanged(object sender, EventArgs e)
        {
            LayoutTextBox();
        }

        void SyncTextBoxStyle()
        {
            if (_textBox == null) return;
            if (_textFont != null) _textFont.Dispose();
            _textFont = new Font("Microsoft YaHei", _fontSize * IconFont.Scale,
                (_bold ? FontStyle.Bold : FontStyle.Regular) | (_italic ? FontStyle.Italic : FontStyle.Regular),
                GraphicsUnit.Pixel);
            _textBox.Font = _textFont;
            _textBox.ForeColor = _color;
            LayoutTextBox();
        }

        void TextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                int sel = _textBox.SelectionStart;
                _textBox.Text = _textBox.Text.Insert(sel, "    ");
                _textBox.SelectionStart = sel + 4;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                CommitText();
                return;
            }
            e.Handled = false;
        }

        void TextBoxLeave(object sender, EventArgs e)
        {
            CommitText();
        }

        ShapeText BuildTextShape(TextBox tb)
        {
            return new ShapeText
            {
                Position = new Point(tb.Location.X + _vs.Left, tb.Location.Y + _vs.Top),
                Text = tb.Text,
                Color = _color,
                FontSize = _fontSize,
                Bold = _bold,
                Italic = _italic
            };
        }

        void CommitText()
        {
            if (_textBox == null || _committingText) return;
            _committingText = true;
            TextBox tb = _textBox;
            _textBox = null;
            try
            {
                if (!string.IsNullOrEmpty(tb.Text))
                {
                    ShapeText s = BuildTextShape(tb);
                    s.Commit(_ctx);
                    DropUndone();
                    _shapes.Add(s);
                }
                tb.Leave -= TextBoxLeave;
                Controls.Remove(tb);
                tb.Dispose();
            }
            finally
            {
                _textBox = null;
                _committingText = false;
            }
            Invalidate();
            Focus();
        }

        static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(0xCF, 0x13, 0x22), Color.FromArgb(0xD4, 0x88, 0x06),
            Color.FromArgb(0x38, 0x9E, 0x0D), Color.FromArgb(0x13, 0xC2, 0xC2),
            Color.FromArgb(0x09, 0x58, 0xD9), Color.FromArgb(0x72, 0x2E, 0xD1),
            Color.FromArgb(0xEB, 0x2F, 0x96), Color.FromArgb(0x00, 0x00, 0x00),
            Color.FromArgb(0xFF, 0xFF, 0xFF)
        };

        void ShowToolbar()
        {
            if (_toolbar != null) { Controls.Remove(_toolbar); _toolbar.Dispose(); _toolbar = null; }
            if (_tip != null) { _tip.Dispose(); _tip = null; }
            _tip = new ToolTip();

            _toolbar = new ToolbarPanel();
            _toolbar.BackColor = Theme.Bg;
            _toolbar.BorderStyle = BorderStyle.None;
            _toolbar.Paint += ToolbarPaint;

            BuildMainBar();
            BuildSubBar(true);
            LayoutToolbar();
            Controls.Add(_toolbar);
            _toolbar.BringToFront();
            Focus();
        }

        void BuildMainBar()
        {
            int bs = (int)Math.Round(32f * IconFont.Scale);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.BackColor = Theme.Bg;
            bar.Padding = new Padding(2);

            ToolKind[] kinds = new ToolKind[]
            {
                ToolKind.Drag,
                ToolKind.Rect, ToolKind.Ellipse, ToolKind.Arrow, ToolKind.Number,
                ToolKind.Line, ToolKind.Text, ToolKind.Mosaic, ToolKind.Eraser
            };
            int[] codes = new int[] { 0, 0xE8E8, 0xE6BC, 0xE603, 0xE776, 0xE601, 0xE6EC, 0xE82E, 0xE6BE };
            string[] names = new string[] { "拖动", "矩形", "椭圆", "箭头", "编号", "线条", "文字", "马赛克", "擦除" };

            _toolBtns = new List<IconButton>();
            for (int i = 0; i < kinds.Length; i++)
            {
                ToolKind k = kinds[i];
                IconButton b = new IconButton((char)codes[i], names[i].Substring(0, 1), names[i]);
                if (k == ToolKind.Drag)
                {
                    IconButton self = b;
                    b.CustomGlyph = delegate (Graphics g, Color fg) { DrawMoveIcon(g, fg, self.Size); };
                }
                b.Selected = (_toolActive && _tool == k);
                b.Click += delegate
                {
                    if (k != ToolKind.Drag && _toolActive && _tool == k)
                    {
                        _toolActive = false;
                        foreach (IconButton t in _toolBtns) { t.Selected = false; t.Invalidate(); }
                        BuildSubBar(true);
                        LayoutToolbar();
                        return;
                    }
                    ApplyTool(k, b);
                };
                _tip.SetToolTip(b, names[i]);
                _toolBtns.Add(b);
                bar.Controls.Add(b);
            }
            bar.Controls.Add(MakeSeparator(bs));

            string[] colorNames = new string[] { "红", "黄", "绿", "青", "蓝", "紫", "粉", "黑", "白" };
            _colorCells = new List<ColorCell>();
            for (int i = 0; i < Palette.Length; i++)
            {
                Color c = Palette[i];
                ColorCell cell = new ColorCell(c);
                cell.Selected = (_color.ToArgb() == c.ToArgb());
                cell.Clicked += delegate
                {
                    _color = c;
                    SyncTextBoxStyle();
                    foreach (ColorCell t in _colorCells) { t.Selected = (t == cell); t.Invalidate(); }
                };
                _tip.SetToolTip(cell, colorNames[i]);
                _colorCells.Add(cell);
                bar.Controls.Add(cell);
            }
            bar.Controls.Add(MakeSeparator(bs));

            bar.Controls.Add(MakeActionButton('\uED85', "↶", "撤销 (Ctrl+Z)", delegate { Undo(); }));
            bar.Controls.Add(MakeActionButton('\uED8A', "↷", "重做 (Ctrl+Y)", delegate { Redo(); }));
            bar.Controls.Add(MakeActionButton('\uE62D', "✕", "退出 (ESC)", delegate { Quit(); }));
            bar.Controls.Add(MakeActionButton('\uE608', "⇩", "保存 (Ctrl+S)", delegate { SaveToFile(); }));
            bar.Controls.Add(MakeActionButton('\uE6AD', "✓", "复制到剪贴板 (Enter / 双击)", delegate { CopyAndQuit(); }));

            bar.PerformLayout();
            _mainBar = bar;
            _toolbar.Controls.Add(bar);
        }

        bool IsDragMode { get { return _toolActive && _tool == ToolKind.Drag; } }

        bool CanFallBackToDrag() { return _selected && _toolActive && _tool != ToolKind.Drag; }

        void ApplyTool(ToolKind k, IconButton btn)
        {
            _tool = k;
            _toolActive = true;
            if (k == ToolKind.Arrow || k == ToolKind.Number) _filled = true;
            if (_toolBtns != null) foreach (IconButton t in _toolBtns) { t.Selected = (t == btn); t.Invalidate(); }
            Cursor = k == ToolKind.Drag ? Cursors.Default : Cursors.Cross;
            RefreshSubBar(true);
        }

        void EnterDragMode()
        {
            if (_toolBtns == null || _toolBtns.Count == 0) { _tool = ToolKind.Drag; _toolActive = true; return; }
            ApplyTool(ToolKind.Drag, _toolBtns[0]);
        }

        void BackToViewing()
        {
            if (_textBox != null) CommitText();
            DisposeShapes();
            _active = null;
            _hover = null;
            _drawing = null;
            _dragHandle = ShapeDragger.None;
            _dragFramePaint = Rectangle.Empty;
            _selected = false;
            _selecting = false;
            _sel = Rectangle.Empty;
            _toolActive = false;
            HideToolbar();
            Cursor = Cursors.Cross;
            Invalidate();
        }

        void HideToolbar()
        {
            _toolBtns = null;
            _colorCells = null;
            _subBar = null;
            if (_toolbar != null) { Controls.Remove(_toolbar); _toolbar.Dispose(); _toolbar = null; }
            if (_tip != null) { _tip.Dispose(); _tip = null; }
        }

        static void DrawMoveIcon(Graphics g, Color color, Size size)
        {
            float cx = size.Width / 2f, cy = size.Height / 2f;
            float arm = 6f * IconFont.Scale;
            float head = 3.2f * IconFont.Scale;
            float tip = 2.8f * IconFont.Scale;
            float back = 0.6f * head;
            using (Pen p = new Pen(color, Math.Max(1f, 1.3f * IconFont.Scale)))
            using (SolidBrush b = new SolidBrush(color))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - arm, cy, cx + arm, cy);
                g.DrawLine(p, cx, cy - arm, cx, cy + arm);
                g.FillPolygon(b, new PointF[] {
                    new PointF(cx, cy - arm - tip),
                    new PointF(cx - head, cy - arm + back),
                    new PointF(cx + head, cy - arm + back) });
                g.FillPolygon(b, new PointF[] {
                    new PointF(cx, cy + arm + tip),
                    new PointF(cx - head, cy + arm - back),
                    new PointF(cx + head, cy + arm - back) });
                g.FillPolygon(b, new PointF[] {
                    new PointF(cx - arm - tip, cy),
                    new PointF(cx - arm + back, cy - head),
                    new PointF(cx - arm + back, cy + head) });
                g.FillPolygon(b, new PointF[] {
                    new PointF(cx + arm + tip, cy),
                    new PointF(cx + arm - back, cy - head),
                    new PointF(cx + arm - back, cy + head) });
            }
        }

        void RefreshSubBar(bool toolChanged)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(RefreshSubBar), toolChanged); return; }
            BuildSubBar(toolChanged);
            LayoutToolbar();
        }

        void ResetSubValues()
        {
            _filled = (_tool == ToolKind.Arrow || _tool == ToolKind.Number);
            _rectMode = false;
            _bold = false;
            ResetSliderValues();
        }

        void ResetSliderValues()
        {
            switch (_tool)
            {
                case ToolKind.Rect:
                case ToolKind.Ellipse: _strokeWidth = 3; break;
                case ToolKind.Arrow: _arrowSize = 18; break;
                case ToolKind.Line: _brushWidth = 3; break;
                case ToolKind.Text: _fontSize = 26; break;
                case ToolKind.Mosaic:
                case ToolKind.Eraser: _mosaicBrush = 32; break;
            }
        }

        void BuildSubBar(bool toolChanged)
        {
            if (toolChanged) ResetSubValues(); else ResetSliderValues();
            if (_subBar != null) { _toolbar.Controls.Remove(_subBar); _subBar.Dispose(); _subBar = null; }

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.BackColor = Theme.Bg;
            bar.Padding = new Padding(2);

            if (_toolActive)
            {
                switch (_tool)
                {
                    case ToolKind.Rect:
                        AddFillToggle(bar, "填充", '\uE602', "■");
                        if (!_filled) AddSlider(bar, 16f, 1f, _strokeWidth, delegate (float v) { _strokeWidth = (int)Math.Round(v); });
                        break;

                    case ToolKind.Ellipse:
                        AddFillToggle(bar, "填充", '\uE600', "●");
                        if (!_filled) AddSlider(bar, 16f, 1f, _strokeWidth, delegate (float v) { _strokeWidth = (int)Math.Round(v); });
                        break;

                    case ToolKind.Arrow:
                        AddFillToggle(bar, "实心", '\uE604', "▲");
                        AddSlider(bar, 60f, 8f, _arrowSize, delegate (float v) { _arrowSize = (int)Math.Round(v); });
                        break;

                    case ToolKind.Number:
                        AddFillToggle(bar, "填充", '\uE605', "●");
                        break;

                    case ToolKind.Line:
                        AddFillToggle(bar, "半透明", '\uE607', "◐", _semiTransparent, delegate { _semiTransparent = !_semiTransparent; });
                        AddSlider(bar, 60f, 1f, _brushWidth, delegate (float v) { _brushWidth = (int)Math.Round(v); });
                        break;

                    case ToolKind.Text:
                        AddTextToggle(bar, '\uE634', "B", _bold, delegate { _bold = !_bold; });
                        AddTextToggle(bar, '\uE682', "I", _italic, delegate { _italic = !_italic; });
                        AddSlider(bar, 66f, 16f, _fontSize, delegate (float v) { _fontSize = (int)Math.Round(v); });
                        break;

                    case ToolKind.Mosaic:
                    case ToolKind.Eraser:
                        AddFillToggle(bar, "矩形", '\uE602', "■", _rectMode, delegate { _rectMode = !_rectMode; });
                        if (!_rectMode) AddSlider(bar, 86f, 26f, _mosaicBrush, delegate (float v) { _mosaicBrush = (int)Math.Round(v); });
                        break;
                }
            }

            bar.PerformLayout();
            if (bar.Controls.Count == 0) { bar.Dispose(); _subBar = null; return; }
            _subBar = bar;

            _toolbar.Controls.Add(bar);
            SyncTextBoxStyle();
        }

        void AddFillToggle(FlowLayoutPanel bar, string label, char glyph, string fallback)
        {
            AddFillToggle(bar, label, glyph, fallback, _filled, delegate { _filled = !_filled; });
        }

        void AddFillToggle(FlowLayoutPanel bar, string label, char glyph, string fallback, bool state, Action toggle)
        {
            FillButton fb = new FillButton(state, label, glyph, fallback);
            fb.Click += delegate { toggle(); RefreshSubBar(false); };
            _tip.SetToolTip(fb, label);
            bar.Controls.Add(fb);
        }

        void AddTextToggle(FlowLayoutPanel bar, char glyph, string fallback, bool state, Action toggle)
        {
            TextToggleButton tb = new TextToggleButton(state, glyph, fallback, glyph == '\uE634' ? "粗体" : "斜体");
            tb.Click += delegate { toggle(); RefreshSubBar(false); };
            _tip.SetToolTip(tb, glyph == '\uE634' ? "粗体" : "斜体");
            bar.Controls.Add(tb);
        }

        void AddSlider(FlowLayoutPanel bar, float max, float min, float val, Action<float> changed)
        {
            ToolSlider sl = new ToolSlider(max, min, val);
            BrushPreview preview = new BrushPreview(val * IconFont.Scale);
            sl.ValueChanged += delegate
            {
                changed(sl.Val);
                SyncTextBoxStyle();
                preview.SetValue(sl.Val * IconFont.Scale);
            };
            _tip.SetToolTip(sl, "拖动或滚轮调节");
            bar.Controls.Add(sl);
            bar.Controls.Add(preview);
        }

        IconButton MakeActionButton(char glyph, string fallback, string tip, EventHandler action)
        {
            IconButton b = new IconButton(glyph, fallback, tip);
            b.Click += delegate { action(b, EventArgs.Empty); };
            _tip.SetToolTip(b, tip);
            return b;
        }

        Control MakeSeparator(int btnSize)
        {
            Panel p = new Panel();
            p.Width = 1;
            p.Height = (int)Math.Round(20f * IconFont.Scale);
            p.BackColor = Theme.Splitter;
            p.Margin = new Padding(4, Math.Max(0, (btnSize - p.Height) / 2), 4, 0);
            return p;
        }

        void ToolbarPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = _toolbar.Width, h = _toolbar.Height;
            g.Clear(Theme.Bg);

            using (Pen p = new Pen(Theme.Splitter, Math.Max(1f, IconFont.Scale)))
            {
                g.DrawRectangle(p, 0, 0, w - 1, h - 1);
                if (_subBar == null) return;

                float subTop = _subBar.Top - 1f * IconFont.Scale;
                float halfW = 3f * IconFont.Scale;
                float tipY = subTop - halfW;
                g.DrawLines(p, new PointF[]
                {
                    new PointF(0.5f, subTop),
                    new PointF(_subArrowX - halfW, subTop),
                    new PointF(_subArrowX, tipY),
                    new PointF(_subArrowX + halfW, subTop),
                    new PointF(w - 0.5f, subTop)
                });
            }
        }

        void LayoutToolbar()
        {
            if (_toolbar == null || _mainBar == null) return;

            int mainH = _mainBar.Height;
            bool hasSub = _subBar != null;
            int w = Math.Max(_mainBar.Width, hasSub ? _subBar.Width : 0) + 4;
            int h = mainH + (hasSub ? _subBar.Height + 3 : 0) + 4;
            _toolbar.Size = new Size(w, h);

            _mainBar.Location = new Point(2, 2);
            if (hasSub) _subBar.Location = new Point(2, mainH + (int)Math.Round(4f * IconFont.Scale));

            int bs = (int)Math.Round(32f * IconFont.Scale);
            int selIdx = 0;
            for (int i = 0; i < _toolBtns.Count; i++) if (_toolBtns[i].Selected) { selIdx = i; break; }
            _subArrowX = 2 + selIdx * bs + bs / 2;
            bool centerMode = hasSub && _subArrowX + (int)Math.Round(6f * IconFont.Scale) > w;

            int left = _sel.Left - _vs.Left;
            int top = _sel.Bottom - _vs.Top + 8;
            if (top + _toolbar.Height > ClientSize.Height - 8) top = _sel.Top - _vs.Top - _toolbar.Height - 8;
            if (top < 0) top = 0;
            if (centerMode)
            {
                int btnCenter = _sel.Left - _vs.Left + (int)_subArrowX;
                left = btnCenter - w / 2;
                _subArrowX = w / 2f;
            }
            if (left + _toolbar.Width > ClientSize.Width - 8) left = ClientSize.Width - _toolbar.Width - 8;
            if (left < 0) left = 0;
            _toolbar.Location = new Point(left, top);
            _toolbar.Invalidate();
        }

        Bitmap RenderResult()
        {
            Bitmap bmp = new Bitmap(_sel.Width, _sel.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Rectangle src = new Rectangle(_sel.X - _vs.Left, _sel.Y - _vs.Top, _sel.Width, _sel.Height);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_screen, new Rectangle(0, 0, bmp.Width, bmp.Height), src, GraphicsUnit.Pixel);
                g.InterpolationMode = InterpolationMode.Default;
                g.TranslateTransform(-_sel.X, -_sel.Y);
                foreach (ShapeBase s in _shapes)
                {
                    if (!s.Undone) s.Paint(g, _ctx);
                }
                if (_textBox != null) BuildTextShape(_textBox).Paint(g, _ctx);
                g.ResetTransform();
            }
            return bmp;
        }

        void CopyAndQuit()
        {
            using (Bitmap bmp = RenderResult())
            {
                ClipboardImage.Set(bmp);
            }
            Quit();
        }

        void SaveToFile()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG 图片|*.png";
                dlg.FileName = "QuickShot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using (Bitmap bmp = RenderResult())
                {
                    bmp.Save(dlg.FileName, ImageFormat.Png);
                }
            }
        }

        void Quit()
        {
            Close();
        }

        void DisposeShapes()
        {
            for (int i = 0; i < _shapes.Count; i++)
            {
                if (_shapes[i] != null) _shapes[i].DisposeResources();
            }
            _shapes.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            Font textFont = _textFont;
            _textFont = null;
            if (disposing)
            {
                ShapeBase lastDrawing = _drawing;
                ShapeBase lastActive = _active;
                bool drawingInList = lastDrawing != null && _shapes.Contains(lastDrawing);
                bool activeInList = lastActive != null && _shapes.Contains(lastActive);
                _drawing = null;
                _active = null;
                _hover = null;
                DisposeShapes();
                if (lastDrawing != null && !drawingInList) lastDrawing.DisposeResources();
                if (lastActive != null && !activeInList && !object.ReferenceEquals(lastActive, lastDrawing))
                {
                    lastActive.DisposeResources();
                }

                if (_screen != null) _screen.Dispose();
                if (_brushDim != null) _brushDim.Dispose();
                if (_brushSizeBg != null) _brushSizeBg.Dispose();
                if (_brushSizeFg != null) _brushSizeFg.Dispose();
                if (_brushHintBg != null) _brushHintBg.Dispose();
                if (_brushHintFg != null) _brushHintFg.Dispose();
                if (_brushPixBg != null) _brushPixBg.Dispose();
                if (_brushPixFg != null) _brushPixFg.Dispose();
                if (_brushCross != null) _brushCross.Dispose();
                if (_penPixBorder != null) _penPixBorder.Dispose();
                if (_fontSizeInfo != null) _fontSizeInfo.Dispose();
                if (_fontHint != null) _fontHint.Dispose();
                if (_fontPix != null) _fontPix.Dispose();
            }
            base.Dispose(disposing);
            if (textFont != null) textFont.Dispose();
        }
    }
}
