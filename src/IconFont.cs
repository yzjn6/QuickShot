using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace QuickShot
{
    internal static class IconFont
    {
        internal static FontFamily Family;
        internal static float Scale = 1f;
        internal static readonly StringFormat Center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        static IntPtr _mem;
        static PrivateFontCollection _pfc;

        internal static void Load()
        {
            if (_pfc != null) return;
            try
            {
                Stream s = typeof(IconFont).Assembly.GetManifestResourceStream("QuickShot.iconfont.ttf");
                if (s == null) return;
                byte[] data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                s.Dispose();
                _mem = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, _mem, data.Length);
                _pfc = new PrivateFontCollection();
                _pfc.AddMemoryFont(_mem, data.Length);
                if (_pfc.Families.Length > 0) Family = _pfc.Families[0];
            }
            catch
            {
                Family = null;
            }
        }

        internal static Font Make(float pixelSize)
        {
            if (Family != null) return new Font(Family, pixelSize, GraphicsUnit.Pixel);
            return new Font("Segoe UI Symbol", pixelSize, GraphicsUnit.Pixel);
        }

        internal static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }
    }
}
