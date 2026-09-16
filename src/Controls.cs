using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QuickShot
{
    internal sealed class IconButton : Control
    {
        internal char Glyph;
        internal string Fallback;
        internal bool Selected;
        internal Action<Graphics, Color> CustomGlyph;
        bool _hover;
        readonly Font _font;

        internal IconButton(char glyph, string fallback, string tip)
        {
            Glyph = glyph;
            Fallback = fallback;
            int s = (int)Math.Round(32f * IconFont.Scale);
            Width = s;
            Height = s;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            _font = IconFont.Make(14f * IconFont.Scale);
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (Selected)
            {
                float padX = 2.6f * IconFont.Scale;
                float padY = 4f * IconFont.Scale;
                using (GraphicsPath path = IconFont.RoundedRect(
                    new RectangleF(padX, padY, Width - padX * 2f, Height - padY * 2f), 5f * IconFont.Scale))
                using (SolidBrush b = new SolidBrush(Theme.SelectBg))
                {
                    g.FillPath(b, path);
                }
            }

            Color fg = (Selected || _hover) ? Theme.Blue : Theme.Icon;
            if (CustomGlyph != null)
            {
                CustomGlyph(g, fg);
                return;
            }
            using (SolidBrush b = new SolidBrush(fg))
            {
                string text = IconFont.Family != null ? Glyph.ToString() : Fallback;
                g.DrawString(text, _font, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _font.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ToolSlider : Control
    {
        internal float Min, Max, Val;
        internal event Action ValueChanged;
        bool _hover, _drag;

        internal ToolSlider(float max, float min, float val)
        {
            Max = max;
            Min = min;
            Val = val;
            Width = (int)Math.Round(80f * IconFont.Scale) + 10;
            Height = (int)Math.Round(28f * IconFont.Scale);
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;

            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
            MouseDown += delegate { _drag = true; SetFromX(PointToClient(Control.MousePosition).X); };
            MouseMove += delegate { if (_drag) SetFromX(PointToClient(Control.MousePosition).X); };
            MouseUp += delegate { _drag = false; };
            MouseWheel += OnWheel;
        }

        void OnWheel(object sender, MouseEventArgs e)
        {
            Val += e.Delta > 0 ? 1f : -1f;
            if (Val > Max) Val = Max;
            if (Val < Min) Val = Min;
            Invalidate();
            if (ValueChanged != null) ValueChanged();
        }

        void SetFromX(int x)
        {
            float pad = 5f * IconFont.Scale;
            float t = (x - pad) / (Width - pad * 2f);
            if (t < 0f || t > 1f) return;
            Val = Min + t * (Max - Min);
            Invalidate();
            if (ValueChanged != null) ValueChanged();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float pad = 5f * IconFont.Scale;
            float y = Height / 2f;
            using (Pen line = new Pen(Theme.Splitter, 2f * IconFont.Scale))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                g.DrawLine(line, pad, y, Width - pad, y);
            }

            float t = (Val - Min) / (Max - Min);
            float cx = pad + (Width - pad * 2f) * t;
            float r = 4f * IconFont.Scale;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(0x88, 0x88, 0x88)))
            {
                g.FillEllipse(b, cx - r, y - r, r * 2f, r * 2f);
            }

            if (_hover || _drag)
            {
                string s = Math.Round(Val).ToString();
                using (Font f = new Font("Microsoft YaHei", 9f * IconFont.Scale, GraphicsUnit.Pixel))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(230, 0, 0, 0)))
                using (SolidBrush fg = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString(s, f);
                    float bx = cx - sz.Width / 2f - 3f;
                    float by = y - r - sz.Height - 4f;
                    if (by < 0) by = y + r + 2f;
                    g.FillRectangle(bg, bx, by, sz.Width + 6, sz.Height + 2);
                    g.DrawString(s, f, fg, bx + 3, by + 1);
                }
            }
        }
    }

    internal sealed class BrushPreview : Control
    {
        internal float Value;

        internal BrushPreview(float value)
        {
            Value = value;
            int s = (int)Math.Round(28f * IconFont.Scale);
            Width = s;
            Height = s;
            Margin = new Padding(0);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
        }

        internal void SetValue(float v)
        {
            if (Math.Abs(v - Value) < 0.01f) return;
            Value = v;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float pad = 2f * IconFont.Scale;
            using (Pen p = new Pen(Theme.Splitter, Math.Max(1f, IconFont.Scale)))
            {
                g.DrawRectangle(p, pad, pad, Width - pad * 2, Height - pad * 2);
            }

            float inner = Math.Min(Width, Height) - pad * 2 - 2f * IconFont.Scale;
            float d = Math.Min(Math.Max(1f, Value), Math.Max(1f, inner));
            using (SolidBrush b = new SolidBrush(Theme.Blue))
            {
                g.FillEllipse(b, (Width - d) / 2f, (Height - d) / 2f, d, d);
            }
        }
    }

    internal sealed class ColorCell : Control
    {
        internal Color Value;
        internal bool Selected;
        bool _hover;
        internal event Action Clicked;

        internal ColorCell(Color value)
        {
            Value = value;
            Width = (int)Math.Round(23f * IconFont.Scale);
            Height = (int)Math.Round(28f * IconFont.Scale);
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
            Click += delegate { if (Clicked != null) Clicked(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            float cell = 12f * IconFont.Scale;
            float cx = Width / 2f;
            float cy = Height / 2f;
            RectangleF box = new RectangleF(cx - cell / 2f, cy - cell / 2f, cell, cell);

            if (Selected || _hover)
            {
                float padY = 4f * IconFont.Scale;
                float padX = 2f * IconFont.Scale;
                using (GraphicsPath path = IconFont.RoundedRect(
                    new RectangleF(padX, padY, Width - padX * 2f, Height - padY * 2f), 5.6f * IconFont.Scale))
                using (SolidBrush b = new SolidBrush(Theme.SelectBg))
                {
                    g.FillPath(b, path);
                }
            }

            using (SolidBrush b = new SolidBrush(Value)) g.FillRectangle(b, box);
            using (Pen p = new Pen(Color.FromArgb(0x88, 0x88, 0x88), Math.Max(0.5f, 0.5f * IconFont.Scale)))
            {
                g.DrawRectangle(p, box.X, box.Y, box.Width, box.Height);
            }

            if (Selected)
            {
                bool light = Value.GetBrightness() > 0.6;
                using (Font f = IconFont.Make(9f * IconFont.Scale))
                using (SolidBrush b = new SolidBrush(light ? Color.Black : Color.White))
                {
                    g.DrawString("\uE6AD", f, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
                }
            }
        }
    }

    internal sealed class FillButton : Control
    {
        internal bool Filled;
        internal string Label;
        internal char Glyph;
        internal string Fallback;
        bool _hover;

        internal FillButton(bool filled, string label, char glyph, string fallback)
        {
            Filled = filled;
            Label = label;
            Glyph = glyph;
            Fallback = fallback;
            int s = (int)Math.Round(28f * IconFont.Scale);
            Width = s;
            Height = s;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (Filled)
            {
                float pad = 3f * IconFont.Scale;
                using (GraphicsPath path = IconFont.RoundedRect(
                    new RectangleF(pad, pad, Width - pad * 2f, Height - pad * 2f), 5f * IconFont.Scale))
                using (SolidBrush b = new SolidBrush(Theme.SelectBg))
                {
                    g.FillPath(b, path);
                }
            }

            Color fg = (Filled || _hover) ? Theme.Blue : Theme.Icon;
            using (SolidBrush b = new SolidBrush(fg))
            {
                if (IconFont.Family != null)
                {
                    using (Font f = IconFont.Make(14f * IconFont.Scale))
                        g.DrawString(Glyph.ToString(), f, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
                }
                else
                {
                    using (Font f = new Font("Microsoft YaHei", 14f * IconFont.Scale, GraphicsUnit.Pixel))
                        g.DrawString(Fallback, f, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
                }
            }
        }
    }

    internal sealed class TextToggleButton : Control
    {
        internal bool On;
        internal char Glyph;
        internal string Fallback;
        bool _hover;

        internal TextToggleButton(bool on, char glyph, string fallback, string tip)
        {
            On = on;
            Glyph = glyph;
            Fallback = fallback;
            int s = (int)Math.Round(28f * IconFont.Scale);
            Width = s;
            Height = s;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (On)
            {
                float pad = 3f * IconFont.Scale;
                using (GraphicsPath path = IconFont.RoundedRect(
                    new RectangleF(pad, pad, Width - pad * 2f, Height - pad * 2f), 5f * IconFont.Scale))
                using (SolidBrush b = new SolidBrush(Theme.SelectBg))
                {
                    g.FillPath(b, path);
                }
            }

            Color fg = (On || _hover) ? Theme.Blue : Theme.Icon;
            using (SolidBrush b = new SolidBrush(fg))
            {
                if (IconFont.Family != null)
                {
                    using (Font f = IconFont.Make(14f * IconFont.Scale))
                        g.DrawString(Glyph.ToString(), f, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
                }
                else
                {
                    FontStyle style = (Fallback == "B") ? FontStyle.Bold : FontStyle.Italic;
                    using (Font f = new Font("Microsoft YaHei", 14f * IconFont.Scale, style, GraphicsUnit.Pixel))
                        g.DrawString(Fallback, f, b, new RectangleF(0f, 0f, Width, Height), IconFont.Center);
                }
            }
        }
    }
}
