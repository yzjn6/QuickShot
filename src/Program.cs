using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QuickShot
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var boot = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!Native.SetProcessDpiAwarenessContext(-2))
                {
                    Native.SetProcessDPIAware();
                }
            }
            catch
            {
                try { Native.SetProcessDPIAware(); }
                catch { }
            }

            foreach (string a in args)
            {
                if (a == "--selftest") { RunSelfTest(boot); return; }
            }

            using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero))
            {
                IconFont.Scale = g0.DpiY / 96f;
            }
            IconFont.Load();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs ex)
            {
                string detail = ex.Exception.ToString();
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "quickshot-crash.log"), detail);
                }
                catch { }
                MessageBox.Show(detail, "QuickShot 异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Rectangle vs = SystemInformation.VirtualScreen;
            Bitmap screen = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(screen))
            {
                g.CopyFromScreen(vs.Left, vs.Top, 0, 0, screen.Size);
            }

            using (CaptureForm form = new CaptureForm(screen, vs))
            {
                Application.Run(form);
            }
        }

        static string RunHitAssertions()
        {
            ShapeEllipse el = new ShapeEllipse { Start = new Point(-45, -50), End = new Point(55, 50), StrokeWidth = 10 };
            if (!el.HitTest(new Point(55, 0))) return "fail:ellipse_ring_hit";
            if (el.HitTest(new Point(0, 0))) return "fail:ellipse_center_miss";
            if (el.HitTest(new Point(48, 0))) return "fail:ellipse_inner_miss";
            if (el.HitTest(new Point(60, 60))) return "fail:ellipse_corner_miss";

            ShapeRect rc = new ShapeRect { Start = new Point(0, 0), End = new Point(100, 100), StrokeWidth = 8, Filled = true };
            if (rc.HitTest(new Point(50, 50))) return "fail:rect_fill_center_miss";
            if (!rc.HitTest(new Point(50, 3))) return "fail:rect_edge_hit";

            ShapeLine dot = new ShapeLine { BrushWidth = 20 };
            dot.AddPoint(new Point(10, 10));
            using (GraphicsPath dp = dot.MakePath())
            {
                RectangleF db = dp.GetBounds();
                if ((int)Math.Round(db.Width) != 20 || (int)Math.Round(db.Height) != 20) return "fail:dot_diameter";
            }

            ShapeArrow ar = new ShapeArrow { Start = new Point(0, 0), End = new Point(100, 0), ArrowSize = 18 };
            if (!ar.HitTest(new Point(50, 0))) return "fail:arrow_shaft_hit";
            if (ar.HitTest(new Point(50, 6))) return "fail:arrow_outside_miss";

            return "ok";
        }

        static string RunControlSmoke()
        {
            string stage = "panel";
            try
            {
                stage = "iconfont_gc";
                IconFont.Load();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Font probe = IconFont.Make(14f))
                {
                    if (probe == null) return "fail:iconfont_null";
                }

                using (ToolbarPanel panel = new ToolbarPanel())
                using (Bitmap bmp = new Bitmap(160, 48))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    stage = "iconbutton";
                    IconButton ib = new IconButton('\uE8E8', "R", "矩形");
                    ib.BackColor = Color.Transparent;
                    panel.Controls.Add(ib);

                    stage = "slider";
                    ToolSlider sl = new ToolSlider(60f, 8f, 18f);
                    sl.BackColor = Color.Transparent;
                    panel.Controls.Add(sl);

                    stage = "colorcell";
                    ColorCell cc = new ColorCell(Color.Red);
                    cc.BackColor = Color.Transparent;
                    panel.Controls.Add(cc);

                    stage = "brushpreview";
                    BrushPreview bp = new BrushPreview(12f);
                    bp.BackColor = Color.Transparent;
                    bp.SetValue(20f);
                    panel.Controls.Add(bp);

                    stage = "fillbutton";
                    FillButton fb = new FillButton(true, "填充", '\uE602', "■");
                    fb.BackColor = Color.Transparent;
                    panel.Controls.Add(fb);

                    stage = "texttoggle";
                    TextToggleButton tb = new TextToggleButton(false, '\uE634', "B", "粗体");
                    tb.BackColor = Color.Transparent;
                    panel.Controls.Add(tb);

                    stage = "draw";
                    foreach (Control c in panel.Controls)
                    {
                        c.DrawToBitmap(bmp, new Rectangle(0, 0, Math.Min(c.Width, 160), Math.Min(c.Height, 48)));
                    }
                }
                return "ok";
            }
            catch (Exception ex)
            {
                return "fail:ui_" + stage + ":" + ex.Message;
            }
        }

        static string RunGeometryAssertions()
        {
            Point zero = new Point(0, 0);

            if (ArrowGeometry.ConstrainToEightDirections(zero, new Point(20, 8)) != new Point(20, 0)) return "fail:constrain_horizontal";
            if (ArrowGeometry.ConstrainToEightDirections(zero, new Point(8, 20)) != new Point(0, 20)) return "fail:constrain_vertical";
            if (ArrowGeometry.ConstrainToEightDirections(zero, new Point(30, 30)) != new Point(30, 30)) return "fail:constrain_diagonal";
            if (ArrowGeometry.ConstrainToEightDirections(zero, new Point(-20, -8)) != new Point(-20, 0)) return "fail:constrain_horizontal_neg";

            if (MosaicUtil.BlockSize(32) != 11) return "fail:mosaic_block_32";
            if (MosaicUtil.BlockSize(0) != 6) return "fail:mosaic_block_0";

            if (CaptureForm.CmykText(Color.FromArgb(255, 0, 0)) != "0,100,100,0") return "fail:cmyk_red";
            if (CaptureForm.CmykText(Color.FromArgb(0, 0, 0)) != "0,0,0,100") return "fail:cmyk_black";
            if (CaptureForm.CmykText(Color.FromArgb(255, 255, 255)) != "0,0,0,0") return "fail:cmyk_white";
            if (CaptureForm.CmykText(Color.FromArgb(128, 128, 128)) != "0,0,0,50") return "fail:cmyk_gray";

            return "ok";
        }

        static void RunSelfTest(System.Diagnostics.Stopwatch boot)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int w = Math.Min(160, vs.Width - 20);
            int h = Math.Min(90, vs.Height - 20);
            Bitmap screen = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            int code;
            try
            {
                using (Graphics g = Graphics.FromImage(screen))
                {
                    g.CopyFromScreen(vs.Left, vs.Top, 0, 0, screen.Size);
                }

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(screen, new Rectangle(0, 0, w, h),
                        new Rectangle(10, 10, w, h), GraphicsUnit.Pixel);
                    using (Pen p = new Pen(Color.Red, 3))
                    {
                        g.DrawRectangle(p, 2, 2, w - 5, h - 5);
                    }
                }

                bool ok = ClipboardImage.Set(bmp);

                string render;
                try
                {
                    PaintContext ctx = new PaintContext { Screen = screen, Vs = vs, Scale = 1f };

                    ShapeLine line = new ShapeLine { Color = Color.Blue, BrushWidth = 4, SemiTransparent = true };
                    line.AddPoint(new Point(90, 60));
                    line.AddPoint(new Point(120, 80));
                    line.AddPoint(new Point(150, 70));

                    ShapeMosaic mosaicRect = new ShapeMosaic
                    {
                        RectMode = true,
                        RectStart = new Point(20, 60),
                        RectEnd = new Point(70, 90)
                    };

                    ShapeMosaic mosaicBrush = new ShapeMosaic { RectMode = false, BrushWidth = 32 };
                    mosaicBrush.AddPoint(new Point(30, 20));
                    mosaicBrush.AddPoint(new Point(60, 40));

                    ShapeEraser eraser = new ShapeEraser
                    {
                        RectMode = true,
                        RectStart = new Point(100, 20),
                        RectEnd = new Point(140, 40)
                    };

                    ShapeBase[] arr = new ShapeBase[]
                    {
                        new ShapeRect { Start = new Point(5, 5), End = new Point(60, 40), Color = Color.Red, StrokeWidth = 3, Filled = false },
                        new ShapeEllipse { Start = new Point(70, 5), End = new Point(120, 40), Color = Color.Green, StrokeWidth = 8, Filled = true },
                        new ShapeArrow { Start = new Point(5, 50), End = new Point(80, 100), Color = Color.Blue, ArrowSize = 18, Filled = true },
                        new ShapeNumber { Center = new Point(150, 20), Radius = 16, Value = 3, Color = Color.Orange, Filled = true },
                        new ShapeText { Position = new Point(5, 95), Text = "测试", FontSize = 26, Bold = true, Color = Color.Purple },
                        line, mosaicRect, mosaicBrush, eraser
                    };

                    using (Bitmap t = new Bitmap(200, 130))
                    using (Graphics tg = Graphics.FromImage(t))
                    {
                        foreach (ShapeBase s in arr)
                        {
                            s.Commit(ctx);
                            s.Paint(tg, ctx);
                        }
                        new ShapeEraser { RectMode = true, RectStart = new Point(10, 10), RectEnd = new Point(50, 30) }.Paint(tg, ctx);
                        ShapeEraser eraserBrushPreview = new ShapeEraser { RectMode = false, BrushWidth = 32 };
                        eraserBrushPreview.AddPoint(new Point(60, 60));
                        eraserBrushPreview.AddPoint(new Point(90, 70));
                        eraserBrushPreview.Paint(tg, ctx);
                    }
                    render = "ok";
                }
                catch (Exception ex)
                {
                    render = "fail:" + ex.Message;
                }

                if (render == "ok")
                {
                    try { render = RunHitAssertions(); }
                    catch (Exception ex) { render = "fail:" + ex.Message; }
                }

                if (render == "ok")
                {
                    try { render = RunGeometryAssertions(); }
                    catch (Exception ex) { render = "fail:" + ex.Message; }
                }

                if (render == "ok") render = RunControlSmoke();

                Console.WriteLine("selftest size=" + w + "x" + h + " clipboard=" + ok + " render=" + render +
                                  " boot=" + boot.ElapsedMilliseconds + "ms");
                code = ok ? 0 : 2;
                if (render != "ok") code = 3;
            }
            finally
            {
                bmp.Dispose();
                screen.Dispose();
            }
            Environment.Exit(code);
        }
    }
}
