using System;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Dhikr
{
    /// <summary>
    /// Windows can't clip blur-behind on a per-pixel-transparent (layered) WPF window, so the blur lives in this
    /// separate plain window: it sits right under the glass panel, takes the panel's rounded shape via a window
    /// region, and lets all mouse input fall through to the panel above it.
    /// </summary>
    public class BlurBackdrop : Forms.Form
    {
        public bool Supported { get; private set; }
        System.Drawing.Rectangle _last;
        int _lastRadius = -1;

        public BlurBackdrop()
        {
            FormBorderStyle = Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = Forms.FormStartPosition.Manual;
            BackColor = System.Drawing.Color.Black; // black GDI pixels = fully see-through to the blur
            TopMost = true;
            Bounds = new System.Drawing.Rectangle(-32000, -32000, 1, 1);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override Forms.CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 /* TOOLWINDOW */ | 0x08000000 /* NOACTIVATE */ | 0x20 /* TRANSPARENT */;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Supported = Glass.EnableBlur(Handle);
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == 0x84 /* WM_NCHITTEST */) { m.Result = (IntPtr)(-1) /* HTTRANSPARENT */; return; }
            base.WndProc(ref m);
        }

        /// <summary>Match a rounded rect (screen DIPs) and sit directly below <paramref name="above"/>.</summary>
        public void Follow(Rect screenDip, double radiusDip, IntPtr above)
        {
            if (!Supported) return;
            double s = Screens.Scale;
            var r = new System.Drawing.Rectangle(
                (int)Math.Round(screenDip.X * s), (int)Math.Round(screenDip.Y * s),
                Math.Max(1, (int)Math.Round(screenDip.Width * s)), Math.Max(1, (int)Math.Round(screenDip.Height * s)));
            int d = Math.Max(0, (int)Math.Round(radiusDip * 2 * s));

            SetWindowPos(Handle, above, r.X, r.Y, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            if (r.Size != _last.Size || d != _lastRadius)
            {
                IntPtr rgn = CreateRoundRectRgn(0, 0, r.Width + 1, r.Height + 1, d, d);
                if (SetWindowRgn(Handle, rgn, true) == 0) DeleteObject(rgn);
            }
            _last = r; _lastRadius = d;
        }

        public void Conceal()
        {
            if (IsHandleCreated) ShowWindow(Handle, 0);
            _last = System.Drawing.Rectangle.Empty;
        }

        const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    }
}
