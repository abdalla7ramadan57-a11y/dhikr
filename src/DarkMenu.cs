using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Dhikr
{
    /// <summary>Dark, RTL, rounded (Windows 11) context menu used by both the widget and the tray icon.</summary>
    public class DarkMenu : ContextMenuStrip
    {
        static readonly Color Back = Color.FromArgb(32, 32, 35), Hot = Color.FromArgb(50, 50, 55),
                              Line = Color.FromArgb(58, 58, 64), Fore = Color.FromArgb(236, 236, 236);

        public DarkMenu()
        {
            Style(this);
            Font = new Font("Segoe UI", 10f);
        }

        public static void Style(ToolStripDropDown d)
        {
            d.RightToLeft = RightToLeft.Yes;
            d.Renderer = new DarkRenderer();
            d.BackColor = Back;
            d.ForeColor = Fore;
            var menu = d as ToolStripDropDownMenu;
            if (menu != null) { menu.ShowImageMargin = false; menu.ShowCheckMargin = true; }
            d.Opened += (s, e) => Glass.RoundCorners(d.Handle);
        }

        class Colors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Back; } }
            public override Color MenuBorder { get { return Line; } }
            public override Color MenuItemBorder { get { return Hot; } }
            public override Color MenuItemSelected { get { return Hot; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
            public override Color ImageMarginGradientBegin { get { return Back; } }
            public override Color ImageMarginGradientMiddle { get { return Back; } }
            public override Color ImageMarginGradientEnd { get { return Back; } }
            public override Color SeparatorDark { get { return Line; } }
            public override Color SeparatorLight { get { return Back; } }
            public override Color CheckBackground { get { return Back; } }
            public override Color CheckSelectedBackground { get { return Hot; } }
            public override Color CheckPressedBackground { get { return Hot; } }
        }

        class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new Colors()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Fore;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.FromArgb(170, 170, 170);
                base.OnRenderArrow(e);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                // The default check glyph is black: draw a light one instead.
                var r = e.ImageRectangle;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(140, 207, 185), 1.8f))
                {
                    float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
                    g.DrawLines(pen, new[] { new PointF(cx - 4, cy), new PointF(cx - 1, cy + 3), new PointF(cx + 4, cy - 3) });
                }
            }
        }
    }
}
