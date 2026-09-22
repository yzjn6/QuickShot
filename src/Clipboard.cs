using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace QuickShot
{
    internal static class ClipboardImage
    {
        internal static bool Set(Bitmap bmp)
        {
            byte[] pixels = GetBgraPixels(bmp);
            if (pixels == null) return false;

            byte[] dibV5 = BuildDibV5(bmp.Width, bmp.Height, pixels);
            byte[] dib = BuildDib(bmp.Width, bmp.Height, pixels);

            byte[] png;
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                png = ms.ToArray();
            }

            if (!Native.OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                Native.EmptyClipboard();
                Put(Native.CF_DIBV5, dibV5);
                Put(Native.CF_DIB, dib);
                uint cfPng = Native.RegisterClipboardFormat("PNG");
                if (cfPng != 0) Put(cfPng, png);
            }
            finally
            {
                Native.CloseClipboard();
            }
            return true;
        }

        static void Put(uint format, byte[] data)
        {
            IntPtr hMem = Native.GlobalAlloc(Native.GMEM_MOVEABLE, new UIntPtr((uint)data.Length));
            if (hMem == IntPtr.Zero) return;
            IntPtr ptr = Native.GlobalLock(hMem);
            if (ptr == IntPtr.Zero) return;
            Marshal.Copy(data, 0, ptr, data.Length);
            Native.GlobalUnlock(hMem);
            if (Native.SetClipboardData(format, hMem) == IntPtr.Zero)
            {
                Native.GlobalFree(hMem);
            }
        }

        static byte[] GetBgraPixels(Bitmap bmp)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bd = null;
            try
            {
                bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int bytes = bd.Stride * bmp.Height;
                byte[] data = new byte[bytes];
                Marshal.Copy(bd.Scan0, data, 0, bytes);
                return data;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bd != null) bmp.UnlockBits(bd);
            }
        }

        static byte[] BuildDibV5(int w, int h, byte[] pixels)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((uint)124);
                bw.Write(w);
                bw.Write(-h);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)3);
                bw.Write((uint)pixels.Length);
                bw.Write(0);
                bw.Write(0);
                bw.Write((uint)0);
                bw.Write((uint)0);
                bw.Write((uint)0x00FF0000);
                bw.Write((uint)0x0000FF00);
                bw.Write((uint)0x000000FF);
                bw.Write((uint)0xFF000000);
                bw.Write((uint)0x73524742);
                for (int i = 0; i < 12; i++) bw.Write((uint)0);
                bw.Write((uint)2);
                bw.Write((uint)0);
                bw.Write((uint)0);
                bw.Write((uint)0);
                bw.Write(pixels);
                bw.Flush();
                return ms.ToArray();
            }
        }

        static byte[] BuildDib(int w, int h, byte[] pixels)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((uint)40);
                bw.Write(w);
                bw.Write(-h);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)0);
                bw.Write((uint)pixels.Length);
                bw.Write(0);
                bw.Write(0);
                bw.Write((uint)0);
                bw.Write((uint)0);
                bw.Write(pixels);
                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}
