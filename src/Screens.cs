using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Dhikr
{
    /// <summary>Monitor work areas converted to WPF device-independent units.</summary>
    public static class Screens
    {
        public static double Scale
        {
            get
            {
                using (var g = System.Drawing.Graphics.FromHwnd(System.IntPtr.Zero))
                    return g.DpiX / 96.0;
            }
        }

        static Rect ToDip(System.Drawing.Rectangle r)
        {
            double s = Scale;
            return new Rect(r.X / s, r.Y / s, r.Width / s, r.Height / s);
        }

        public static Rect WorkAreaAt(Point dip, out string screenName)
        {
            double s = Scale;
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)(dip.X * s), (int)(dip.Y * s)));
            screenName = screen.DeviceName;
            return ToDip(screen.WorkingArea);
        }

        public static Rect WorkAreaByName(string name)
        {
            var screen = Forms.Screen.AllScreens.FirstOrDefault(x => x.DeviceName == name) ?? Forms.Screen.PrimaryScreen;
            return ToDip(screen.WorkingArea);
        }
    }
}
