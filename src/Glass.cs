using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Dhikr
{
    /// <summary>Dark-glass palette + Win32 helpers (blur-behind, shaped regions, rounded menus).</summary>
    public static class Glass
    {
        public static readonly FontFamily Font = new FontFamily(Defaults.FontFamily);

        public static readonly Brush Text = HexBrush("#EDEDED");
        public static readonly Brush TextDim = HexBrush("#9EA3A1");
        public static readonly Brush Accent = HexBrush("#8CCFB9");
        public static readonly Brush Hover = HexBrush("#1FFFFFFF");
        public static readonly Brush Subtle = HexBrush("#12FFFFFF");

        /// <summary>Glass fill. More opaque when Windows can't blur behind us.</summary>
        public static Brush Fill(bool blurred)
        {
            var b = blurred
                ? new LinearGradientBrush(Hex("#C82A2A2E"), Hex("#C01B1B1E"), 90)
                : new LinearGradientBrush(Hex("#F02A2A2E"), Hex("#EE1B1B1E"), 90);
            b.Freeze();
            return b;
        }

        /// <summary>Hairline border, a touch brighter on top like light catching glass.</summary>
        public static readonly Brush Border = Freeze(new LinearGradientBrush(Hex("#38FFFFFF"), Hex("#14FFFFFF"), 90));

        public static int GlowIndex(string key)
        {
            for (int i = 0; i < Defaults.GlowColors.GetLength(0); i++)
                if (Defaults.GlowColors[i, 0] == key) return i;
            return -1;
        }

        public static Color GlowColor(string key)
        {
            int i = Math.Max(0, GlowIndex(key));
            return Hex(Defaults.GlowColors[i, 2]);
        }

        public static Color Hex(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static Brush HexBrush(string hex) { return Freeze(new SolidColorBrush(Hex(hex))); }
        static Brush Freeze(Brush b) { b.Freeze(); return b; }

        // ---------- blur behind ----------

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public int AccentState, AccentFlags; public uint GradientColor; public int AnimationId; }

        [StructLayout(LayoutKind.Sequential)]
        struct CompositionData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);

        /// <summary>Ask DWM to blur what's behind the window (Windows 10/11). The window region limits the blur to our shape.</summary>
        public static bool EnableBlur(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy { AccentState = 3 /* ACCENT_ENABLE_BLURBEHIND */ };
                int size = Marshal.SizeOf(accent);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new CompositionData { Attribute = 19 /* WCA_ACCENT_POLICY */, Data = ptr, SizeOfData = size };
                    return SetWindowCompositionAttribute(hwnd, ref data) != 0;
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
            catch { return false; }
        }

        // ---------- shaped window region ----------

        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);

        /// <summary>Clip the window (and its blur + hit-testing) to a rounded rect given in DIPs.</summary>
        public static void SetRoundRegion(IntPtr hwnd, Rect dip, double radiusDip)
        {
            double s = Screens.Scale;
            int l = (int)Math.Floor(dip.Left * s), t = (int)Math.Floor(dip.Top * s);
            int r = (int)Math.Ceiling(dip.Right * s) + 1, b = (int)Math.Ceiling(dip.Bottom * s) + 1;
            int d = Math.Max(0, (int)Math.Round(radiusDip * 2 * s));
            IntPtr rgn = CreateRoundRectRgn(l, t, r, b, d, d);
            if (SetWindowRgn(hwnd, rgn, true) == 0) DeleteObject(rgn); // on success the system owns it
        }

        // ---------- Windows 11 rounded corners for menus ----------

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, 4); }
            catch { } // Windows 10: no-op
        }
    }
}
