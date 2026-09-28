using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Dhikr
{
    /// <summary>Small dark-glass popup that slides out next to the widget and closes on Esc / click elsewhere.</summary>
    public class GlassPopup : Window
    {
        readonly Point _anchor;
        readonly bool _rightSide;
        readonly Border _card;
        readonly TranslateTransform _slide = new TranslateTransform();
        bool _closing;
        readonly BlurBackdrop _backdrop = new BlurBackdrop();

        protected GlassPopup(Point edgeAnchor, bool rightSide, string title)
        {
            _anchor = edgeAnchor; _rightSide = rightSide;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -10000; Top = -10000;
            Title = title;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = Glass.Font;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Opacity = 0;

            _card = new Border
            {
                Padding = new Thickness(16, 12, 16, 14), CornerRadius = new CornerRadius(16),
                Background = Glass.Fill(false), BorderBrush = Glass.Border, BorderThickness = new Thickness(1),
                RenderTransform = _slide,
            };
            Content = _card;

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { SafeClose(); e.Handled = true; } };
            Deactivated += (s, e) => SafeClose();
            Closing += (s, e) => _closing = true;
            Closed += (s, e) => _backdrop.Close();

            Loaded += (s, e) =>
            {
                Rect wa = Screens.WorkAreaAt(_anchor, out _);
                const double gap = 14;
                double left = _rightSide ? _anchor.X - ActualWidth - gap : _anchor.X + gap;
                double top = _anchor.Y - ActualHeight / 2;
                Left = Math.Max(wa.Left, Math.Min(left, wa.Right - ActualWidth));
                Top = Math.Max(wa.Top + 8, Math.Min(top, wa.Bottom - ActualHeight - 8));

                var hwnd = new WindowInteropHelper(this).Handle;
                if (_backdrop.Handle != IntPtr.Zero && _backdrop.Supported)
                {
                    _card.Background = Glass.Fill(true);
                    _backdrop.Follow(new Rect(Left, Top, ActualWidth, ActualHeight), 16, hwnd);
                }

                // Slides out from the screen edge.
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                _slide.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(_rightSide ? 14 : -14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                Activate();
                OnOpened();
            };
        }

        protected UIElement Body { set { _card.Child = value; } }

        protected virtual void OnOpened() { }

        protected void SafeClose()
        {
            if (_closing) return;
            _closing = true;
            Close();
        }

        protected static TextBlock Label(string text, double size = 13, Brush brush = null)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = brush ?? Glass.TextDim };
        }

        /// <summary>Rounded accent button (a Border, so it can be fully styled).</summary>
        protected static Border AccentButton(string text, Action click)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(10), Background = Glass.Accent, Padding = new Thickness(20, 4, 20, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Glass.HexBrush("#111614") },
            };
            b.MouseEnter += (s, e) => b.Opacity = 0.88;
            b.MouseLeave += (s, e) => b.Opacity = 1;
            b.MouseLeftButtonUp += (s, e) => click();
            return b;
        }
    }
}
