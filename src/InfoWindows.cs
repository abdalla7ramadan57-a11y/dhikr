using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Dhikr
{
    static class InfoContent
    {
        /// <summary>Virtue/evidence text + source, in a scrollable column.</summary>
        public static StackPanel Build(DhikrDef d, double width, double fadlSize)
        {
            var sp = new StackPanel { Width = width };
            sp.Children.Add(new TextBlock
            {
                Text = "الفضل والدليل", FontSize = 13, Foreground = Glass.Accent, Margin = new Thickness(0, 0, 0, 4),
            });
            sp.Children.Add(new TextBlock
            {
                Text = d.Fadl, FontSize = fadlSize, Foreground = Glass.Text, TextWrapping = TextWrapping.Wrap, LineHeight = fadlSize * 1.6,
            });
            sp.Children.Add(new TextBlock
            {
                Text = d.Source, FontSize = 13, Foreground = Glass.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            });
            return sp;
        }

        public static ScrollViewer Scroll(UIElement content, double maxHeight)
        {
            return new ScrollViewer
            {
                Content = content, MaxHeight = maxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly,
            };
        }
    }

    /// <summary>
    /// Hover card for the ⓘ button: never takes focus, scrolls with the mouse wheel, and stays open while the
    /// mouse is over it or over the button.
    /// </summary>
    public class InfoTip : Window
    {
        readonly Rect _card;
        readonly bool _rightSide;

        public InfoTip(DhikrDef d, Rect cardOnScreen, bool rightSide)
        {
            _card = cardOnScreen; _rightSide = rightSide;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -10000; Top = -10000;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = Glass.Font;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var body = InfoContent.Build(d, 290, 15);
            body.Children.Add(new TextBlock
            {
                Text = "اضغط على ⓘ للعرض الكامل", FontSize = 12, Foreground = Glass.TextDim, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0),
            });
            Content = new Border
            {
                Padding = new Thickness(14, 10, 14, 12), CornerRadius = new CornerRadius(14),
                Background = Glass.Fill(false), BorderBrush = Glass.Border, BorderThickness = new Thickness(1),
                Child = InfoContent.Scroll(body, 230),
            };

            Loaded += (s, e) =>
            {
                string _;
                Rect wa = Screens.WorkAreaAt(new Point(_card.X + _card.Width / 2, _card.Y), out _);
                double left = _rightSide ? _card.Left - ActualWidth - 8 : _card.Right + 8;
                double top = _card.Top + _card.Height / 2 - ActualHeight / 2;
                Left = Math.Max(wa.Left + 4, Math.Min(left, wa.Right - ActualWidth - 4));
                Top = Math.Max(wa.Top + 4, Math.Min(top, wa.Bottom - ActualHeight - 4));
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x80 /* TOOLWINDOW */ | 0x08000000 /* NOACTIVATE */);
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }

    /// <summary>Full view in the middle of the screen; closes on Esc or a click anywhere outside it.</summary>
    public class InfoWindow : GlassPopup
    {
        public InfoWindow(DhikrDef d, Point edgeAnchor, bool rightSide) : base(edgeAnchor, rightSide, "فضل الذكر")
        {
            Centered = true;
            var panel = new StackPanel { Width = 480 };
            panel.Children.Add(new TextBlock
            {
                Text = d.Text, FontSize = 24, Foreground = Glass.Text, TextWrapping = TextWrapping.Wrap, LineHeight = 38,
                Margin = new Thickness(0, 4, 0, 12),
            });
            panel.Children.Add(new Border { Height = 1, Background = Glass.HexBrush("#22FFFFFF"), Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(InfoContent.Scroll(InfoContent.Build(d, 470, 17), 320));
            panel.Children.Add(new TextBlock
            {
                Text = "اضغط في أي مكان خارج النافذة للإغلاق", FontSize = 12, Foreground = Glass.TextDim, Opacity = 0.7,
                Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
            });
            Body = panel;
        }
    }
}
