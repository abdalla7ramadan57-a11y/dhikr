using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Dhikr
{
    /// <summary>
    /// The edge widget. The window is a transparent strip whose outer side sits exactly on the screen edge;
    /// the glass panel inside it is a thin handle (mostly tucked past the edge) that stretches out into a card.
    /// The window region follows the panel, so blur and mouse input only exist where the panel is.
    /// </summary>
    public class BubbleWindow : Window
    {
        public const double WinW = 420, WinH = 200;

        // Collapsed handle: 14 wide, 6 of it tucked beyond the screen edge.
        const double HandleW = 14, HandleH = 64, TuckedInset = -6;
        const double PeekW = 18, PeekInset = -3;          // hover: peeks out a little
        const double OpenInset = 8, Radius = 16;           // expanded card
        const double MaxTextWidth = 240, Btn = 26, MinCardW = 190;

        enum Mode { Collapsed, Peek, Expanded, Dragging }

        readonly Controller _c;
        readonly Canvas _root = new Canvas();
        readonly Border _panel;
        readonly Grid _inner;
        readonly RectangleGeometry _clip = new RectangleGeometry();
        readonly Border _grip;
        readonly Grid _content;
        readonly TextBlock _text, _count;
        readonly TranslateTransform _textShift = new TranslateTransform();
        readonly ScaleTransform _countPop = new ScaleTransform(1, 1);
        readonly Ellipse _ripple;
        readonly ScaleTransform _rippleScale = new ScaleTransform(1, 1);
        readonly ScaleTransform _stretch = new ScaleTransform(), _squash = new ScaleTransform();
        readonly TranslateTransform _pull = new TranslateTransform();

        readonly Spring _w = new Spring(HandleW), _h = new Spring(HandleH), _inset = new Spring(TuckedInset);
        readonly Spring _s = new Spring(1) { Epsilon = 0.0005 }, _p = new Spring(0);

        // Reminder glow: a soft coloured halo around the panel (colour picked in settings).
        readonly Spring _glow = new Spring(0) { Epsilon = 0.003 };
        readonly DropShadowEffect _glowFx = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 26, Opacity = 0 };
        const double GlowRoom = 22;
        Spring _winX, _winY;
        bool _winMoving;

        Mode _mode = Mode.Collapsed;
        double _cardW = MinCardW, _cardH = 80;
        bool _rendering, _rightSide = true, _blur;
        TimeSpan _lastFrame;
        IntPtr _hwnd;
        Rect _lastRegion, _shape;
        double _shapeRadius;
        readonly BlurBackdrop _backdrop = new BlurBackdrop();
        readonly DispatcherTimer _idle = new DispatcherTimer();
        readonly List<DispatcherTimer> _script = new List<DispatcherTimer>();

        bool _mouseDown, _dragging;
        Point _downCursor;
        double _downLeft, _downTop;

        public BubbleWindow(Controller controller)
        {
            _c = controller;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Width = WinW; Height = WinH;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "Dhikr";
            FontFamily = Glass.Font;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            // Collapsed look: a faint grip line on the visible part of the handle.
            _grip = new Border
            {
                Width = 2, Height = 20, CornerRadius = new CornerRadius(1), Background = Glass.HexBrush("#55FFFFFF"),
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Expanded look:   ‹  الذكر  ›
            //                     〰 24
            // The row is laid out left-to-right on purpose so the arrows never get mirrored by RTL;
            // only the dhikr text itself is RTL.
            _text = new TextBlock
            {
                FontSize = 16, Foreground = Glass.Text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                MaxWidth = MaxTextWidth, FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _textShift, Cursor = Cursors.Hand, Margin = new Thickness(6, 0, 6, 0),
            };
            var prev = IconButton(Chevron(false), Glass.TextDim, () => _c.Move(-1));
            var next = IconButton(Chevron(true), Glass.TextDim, () => _c.Move(1));

            var row = new Grid { FlowDirection = FlowDirection.LeftToRight };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(prev, 0); Grid.SetColumn(_text, 1); Grid.SetColumn(next, 2);
            row.Children.Add(prev); row.Children.Add(_text); row.Children.Add(next);

            _ripple = new Ellipse
            {
                Width = Btn, Height = Btn, Stroke = Glass.Accent, StrokeThickness = 1.2, Opacity = 0, IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = _rippleScale,
            };
            var wave = IconButton(Geometry.Parse("M 5,13 Q 8,8.5 11,13 T 17,13 T 23,13"), Glass.Accent, () => { if (!_deleteMode) _c.Increment(); });
            wave.Background = Glass.Subtle;
            var waveHost = new Grid { Width = Btn, Height = Btn };
            waveHost.Children.Add(_ripple);
            waveHost.Children.Add(wave);
            _count = new TextBlock
            {
                FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Glass.TextDim, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 1), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = _countPop,
            };
            var counter = new StackPanel
            {
                Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
            };
            counter.Children.Add(waveHost);
            counter.Children.Add(_count);

            // "+" and "−" at the two ends of the counter row. "−" switches to delete mode, where the same two
            // spots become "✓" (delete the dhikr on screen) and "✕" (leave without deleting).
            _counter = counter;
            _plus = IconButton(Geometry.Parse("M 13,8.5 L 13,17.5 M 8.5,13 L 17.5,13"), Glass.TextDim, () => _c.ShowAddDialog());
            _plus.HorizontalAlignment = HorizontalAlignment.Right;
            _plus.ToolTip = "إضافة ذكر";
            _minus = IconButton(Geometry.Parse("M 8.5,13 L 17.5,13"), Glass.TextDim, () => SetDeleteMode(true));
            _minus.HorizontalAlignment = HorizontalAlignment.Left;
            _minus.ToolTip = "حذف ذكر";
            _confirm = IconButton(Geometry.Parse("M 8,13.5 L 11.5,17 L 18,9.5"), Glass.HexBrush("#E89A9A"), ConfirmDelete);
            _confirm.HorizontalAlignment = HorizontalAlignment.Right;
            _confirm.ToolTip = "حذف هذا الذكر";
            _cancel = IconButton(Geometry.Parse("M 9.5,9.5 L 16.5,16.5 M 16.5,9.5 L 9.5,16.5"), Glass.TextDim, () => SetDeleteMode(false));
            _cancel.HorizontalAlignment = HorizontalAlignment.Left;
            _cancel.ToolTip = "إلغاء";
            var bottom = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 4, 0, 0) };
            counter.Margin = new Thickness(0);
            bottom.Children.Add(counter);
            bottom.Children.Add(_minus);
            bottom.Children.Add(_plus);
            bottom.Children.Add(_cancel);
            bottom.Children.Add(_confirm);

            // ⚙ next to "+": turns the card into its small settings view.
            _gear = IconButton(GearIcon(), Glass.TextDim, () => SetSettingsMode(true));
            _gear.HorizontalAlignment = HorizontalAlignment.Right;
            _gear.Margin = new Thickness(0, 0, Btn + 2, 0);
            _gear.ToolTip = "الإعدادات";
            bottom.Children.Add(_gear);
            SetDeleteMode(false);

            _content = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
            _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(bottom, 1);
            _content.Children.Add(row);
            _content.Children.Add(bottom);

            _settings = BuildSettingsView();

            _inner = new Grid { Clip = _clip };
            _inner.Children.Add(_grip);
            _inner.Children.Add(_content);
            _inner.Children.Add(_settings);

            _panel = new Border
            {
                Width = HandleW, Height = HandleH,
                Background = Glass.Fill(false),
                BorderBrush = Glass.Border, BorderThickness = new Thickness(1),
                Child = _inner,
                Cursor = Cursors.Hand,
                RenderTransform = new TransformGroup { Children = { _stretch, _squash, _pull } },
            };
            _root.Children.Add(_panel);
            Content = _root;
            SetSide(true);
            LocationChanged += (s, e) => UpdateBackdrop();
            IsVisibleChanged += (s, e) => UpdateBackdrop();

            _idle.Tick += (s, e) => { _idle.Stop(); if (!_panel.IsMouseOver && !_mouseDown) Collapse(); };

            _panel.MouseEnter += (s, e) =>
            {
                if (_mouseDown) return;
                if (_mode == Mode.Collapsed) Peek();
                else if (_mode == Mode.Expanded) _idle.Stop();
            };
            _panel.MouseLeave += (s, e) =>
            {
                if (_mouseDown) return;
                if (_mode == Mode.Peek && _script.Count == 0) Collapse();
                else if (_mode == Mode.Expanded) CollapseAfter(3);
            };
            _panel.MouseLeftButtonDown += OnDown;
            _panel.MouseMove += OnMove;
            _panel.MouseLeftButtonUp += OnUp;
            _panel.LostMouseCapture += (s, e) => { if (_dragging) SnapToEdge(); _mouseDown = false; };
            _panel.MouseWheel += (s, e) => { if (_mode == Mode.Expanded && !_settingsMode) _c.Move(e.Delta < 0 ? 1 : -1); e.Handled = true; };
            _panel.MouseRightButtonUp += (s, e) => { _c.OpenMenu(); e.Handled = true; };

            Refresh(0);
            Apply();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            // Tool window (no Alt+Tab) that never steals focus from what the user is doing.
            SetWindowLong(_hwnd, GWL_EXSTYLE, GetWindowLong(_hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            _blur = _backdrop.Handle != IntPtr.Zero && _backdrop.Supported;
            _panel.Background = Glass.Fill(_blur);
            _lastRegion = Rect.Empty;
            Apply();
        }

        // ---------- building blocks ----------

        static Geometry Chevron(bool right)
        {
            return Geometry.Parse(right ? "M 11,8 L 16,13 L 11,18" : "M 15,8 L 10,13 L 15,18");
        }

        static Geometry GearIcon()
        {
            var g = new GeometryGroup();
            g.Children.Add(new EllipseGeometry(new Point(13, 13), 4.6, 4.6));
            g.Children.Add(new EllipseGeometry(new Point(13, 13), 1.4, 1.4));
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                g.Children.Add(new LineGeometry(
                    new Point(13 + 5.2 * Math.Cos(a), 13 + 5.2 * Math.Sin(a)),
                    new Point(13 + 7.4 * Math.Cos(a), 13 + 7.4 * Math.Sin(a))));
            }
            return g;
        }

        Border IconButton(Geometry icon, Brush stroke, Action click)
        {
            var path = new Path
            {
                Data = icon, Stroke = stroke, StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = Btn, Height = Btn,
            };
            var scale = new ScaleTransform(1, 1);
            var b = new Border
            {
                Width = Btn, Height = Btn, CornerRadius = new CornerRadius(Btn / 2), Background = Brushes.Transparent,
                Child = path, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = scale,
            };
            Brush idle = null;
            b.MouseEnter += (s, e) => { idle = b.Background; b.Background = Glass.Hover; };
            b.MouseLeave += (s, e) => { if (idle != null) b.Background = idle; scale.ScaleX = scale.ScaleY = 1; };
            b.MouseLeftButtonDown += (s, e) => { scale.ScaleX = scale.ScaleY = 0.88; e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) =>
            {
                scale.ScaleX = scale.ScaleY = 1;
                e.Handled = true;
                if (_mode == Mode.Expanded) click();
            };
            return b;
        }

        // ---------- content ----------

        /// <summary>Show the current dhikr + its count. direction: +1 next, -1 previous (slide), 0 none.</summary>
        public void Refresh(int direction)
        {
            var item = _c.State.Current;
            _count.Text = item.Count.ToString();

            var probe = new TextBlock
            {
                Text = item.Text, FontFamily = Glass.Font, FontSize = _text.FontSize,
                TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft,
            };
            TextOptions.SetTextFormattingMode(probe, TextFormattingMode.Display); // measure exactly like the real text
            probe.Measure(new Size(MaxTextWidth, double.PositiveInfinity));
            var ts = probe.DesiredSize;
            _cardW = Math.Max(MinCardW, Math.Ceiling(ts.Width) + 4 + 2 * Btn + 12 + 20 + 2);
            _cardH = Math.Ceiling(Math.Max(ts.Height, Btn)) + 4 + Btn + 16 + 2;
            _content.Width = _cardW - 2 - 20;
            _settings.Width = SettingsW - 2 - 20;
            _intervalText.Text = "كل " + Controller.IntervalShortLabel(_c.State.ReminderMinutes);
            _colors.Refresh();

            if (direction != 0 && _mode == Mode.Expanded && _text.Text != item.Text) SlideText(item.Text, direction);
            else _text.Text = item.Text;

            if (_mode == Mode.Expanded) { _w.Target = TargetW; _h.Target = TargetH; Kick(); }
            UpdateConfirm();
        }

        void SlideText(string newText, int direction)
        {
            double d = 18 * direction;
            var outX = new DoubleAnimation(0, -d, TimeSpan.FromMilliseconds(110)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            outX.Completed += (s, e) =>
            {
                _text.Text = newText;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                _textShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(d, 0, TimeSpan.FromMilliseconds(230)) { EasingFunction = ease });
                _text.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            };
            _textShift.BeginAnimation(TranslateTransform.XProperty, outX);
            _text.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110)));
        }

        /// <summary>After a +1: ripple out of the wave button, bounce the number.</summary>
        public void Counted()
        {
            _count.Text = _c.State.Current.Count.ToString();
            var dur = TimeSpan.FromMilliseconds(520);
            var grow = new DoubleAnimation(1, 2.3, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            _rippleScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            _rippleScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            _ripple.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8, 0, dur));
            var pop = new DoubleAnimation(1.3, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut },
            };
            _countPop.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            _countPop.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            Touched();
        }

        /// <summary>Any interaction keeps the card open a little longer.</summary>
        public void Touched()
        {
            if (_mode == Mode.Expanded && !_panel.IsMouseOver) CollapseAfter(Defaults.CollapseAfterSeconds);
        }

        // ---------- modes ----------

        void Peek()
        {
            _mode = Mode.Peek;
            _w.Tune(260, 0.55); _h.Tune(260, 0.6); _inset.Tune(260, 0.55);
            _w.Target = PeekW; _h.Target = HandleH + 6; _inset.Target = PeekInset;
            Kick();
        }

        public void Expand(bool jelly)
        {
            if (!IsVisible) return;
            _attention = false; // opening it answers any pending "until clicked" reminder
            CancelScript();
            _mode = Mode.Expanded;
            Refresh(0);
            if (jelly) { _w.Tune(190, 0.48); _h.Tune(210, 0.55); _inset.Tune(200, 0.5); }
            else { _w.Tune(240, 0.8); _h.Tune(240, 0.85); _inset.Tune(240, 0.85); }
            _w.Target = TargetW; _h.Target = TargetH; _inset.Target = OpenInset;
            Kick();
            if (!_panel.IsMouseOver) CollapseAfter(Defaults.CollapseAfterSeconds);
        }

        // ---------- delete mode ----------

        Border _plus, _minus, _confirm, _cancel;
        UIElement _counter;
        bool _deleteMode;

        void SetDeleteMode(bool on)
        {
            _deleteMode = on;
            _plus.Visibility = _minus.Visibility = _gear.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            _confirm.Visibility = _cancel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            _counter.Opacity = on ? 0.35 : 1; // counting is paused while choosing what to delete
            UpdateConfirm();
            Touched();
        }

        /// <summary>Only user-added adhkar can be deleted; ✓ is dimmed on the built-in ones.</summary>
        void UpdateConfirm()
        {
            if (_confirm == null) return;
            bool deletable = _c.State.Current.Custom;
            _confirm.Opacity = deletable ? 1 : 0.3;
            _confirm.ToolTip = deletable ? "حذف هذا الذكر" : "الأذكار الأساسية لا تُحذف";
        }

        void ConfirmDelete()
        {
            if (!_c.State.Current.Custom) return;
            SetDeleteMode(false);
            _c.DeleteCurrent();
        }

        void Collapse()
        {
            if (_deleteMode) SetDeleteMode(false);
            if (_settingsMode) SetSettingsMode(false);
            _glow.Tune(40, 1.0); _glow.Target = 0;
            CancelScript();
            _idle.Stop();
            _mode = Mode.Collapsed;
            _w.Tune(200, 0.8); _h.Tune(200, 0.85); _inset.Tune(170, 0.75);
            _w.Target = HandleW; _h.Target = HandleH; _inset.Target = TuckedInset;
            _p.Target = 0;
            Kick();
        }

        void CollapseAfter(double seconds)
        {
            _idle.Stop();
            _idle.Interval = TimeSpan.FromSeconds(seconds);
            _idle.Start();
        }

        /// <summary>
        /// Reminder: the handle slides out of the edge to catch the eye, gets a gentle tug back toward the edge
        /// (as if tied to it), then is released and stretches open with a soft jelly wobble.
        /// </summary>
        public void Remind()
        {
            if (!IsVisible || _mouseDown || _attention) return;
            PrepareReminder();

            // Three soft glowing peeks, then the tug and the jelly opening.
            Pulse(0); Pulse(800); Pulse(1600);
            Later(2500, () => { PeekOut(); _glow.Tune(90, 1.0); _glow.Target = 0.8; Kick(); });
            Later(3100, () => { _p.Tune(55, 1.0); _p.Target = 9; Kick(); });
            Later(3700, () =>
            {
                _p.Tune(260, 0.4); _p.Target = 0;
                Expand(true);
                _w.Velocity += 260;
                _glow.Tune(6, 1.0); _glow.Target = 0; // the halo fades slowly as it opens
                if (!_panel.IsMouseOver) CollapseAfter(Defaults.ReminderVisibleSeconds);
            });
        }

        /// <summary>
        /// Reminder while the user is away: keep peeking out and glowing (three pulses, a pause, again...)
        /// until the widget is clicked, so the dhikr isn't missed.
        /// </summary>
        public void RemindUntilClicked()
        {
            if (!IsVisible || _mouseDown || _attention) return;
            PrepareReminder();
            _attention = true;
            AttentionRound(0);
        }

        public bool InAttention { get { return _attention; } }
        bool _attention;

        void AttentionRound(int delay)
        {
            Pulse(delay); Pulse(delay + 800); Pulse(delay + 1600);
            Later(delay + 1600 + 7000, () => { if (_attention) AttentionRound(0); });
        }

        /// <summary>The user clicked the calling widget: open it on the new dhikr.</summary>
        void Acknowledge()
        {
            Expand(true);
            _glow.Tune(6, 1.0); _glow.Target = 0;
            if (!_panel.IsMouseOver) CollapseAfter(Defaults.ReminderVisibleSeconds);
        }

        void PrepareReminder()
        {
            Topmost = false; Topmost = true; // re-assert z-order
            UpdateBackdrop();
            if (_mode == Mode.Expanded) Collapse();
            CancelScript();
            _idle.Stop();
            _glowFx.Color = Glass.GlowColor(_c.State.GlowColor);
        }

        void PeekOut()
        {
            _mode = Mode.Peek;
            _w.Tune(120, 0.5); _h.Tune(120, 0.55); _inset.Tune(110, 0.45);
            _w.Target = PeekW + 2; _h.Target = HandleH + 10; _inset.Target = 3;
        }

        /// <summary>One gentle "out and back in" of the handle with a glow.</summary>
        void Pulse(int delay)
        {
            Later(delay, () =>
            {
                if (!IsVisible || _mouseDown || _mode == Mode.Expanded || _panel.IsMouseOver) return;
                _mode = Mode.Peek;
                _w.Tune(160, 0.55); _h.Tune(160, 0.6); _inset.Tune(150, 0.5); _glow.Tune(90, 1.0);
                _w.Target = PeekW + 4; _h.Target = HandleH + 8; _inset.Target = 4; _glow.Target = 1;
                Kick();
            });
            Later(delay + 420, () =>
            {
                if (!IsVisible || _mouseDown || _mode == Mode.Expanded || _panel.IsMouseOver) return;
                _w.Tune(140, 0.6); _h.Tune(140, 0.65); _inset.Tune(130, 0.55); _glow.Tune(40, 1.0);
                _w.Target = HandleW; _h.Target = HandleH; _inset.Target = TuckedInset; _glow.Target = 0.25;
                Kick();
            });
        }

        /// <summary>Flash the halo once in the chosen colour (when picking a colour).</summary>
        public void PreviewGlow()
        {
            _glowFx.Color = Glass.GlowColor(_c.State.GlowColor);
            _glow.Tune(90, 1.0); _glow.Target = 1; Kick();
            Later(700, () => { _glow.Tune(40, 1.0); _glow.Target = 0; Kick(); });
            Touched();
        }

        // ---------- settings view inside the card ----------
        //   ‹   كل 30 دقيقة   ›      (5-minute steps)
        //     ● ● ● ● ● ●      ✓     (glow colour)

        Border _gear;
        Grid _settings;
        TextBlock _intervalText;
        ColorPicker _colors;
        bool _settingsMode;
        const double SettingsH = 2 + 8 + Btn + 6 + Btn + 8 + 2;

        double SettingsW { get { return Math.Max(_cardW, 250); } }
        double TargetW { get { return _settingsMode ? SettingsW : _cardW; } }
        double TargetH { get { return _settingsMode ? SettingsH : _cardH; } }

        Grid BuildSettingsView()
        {
            _intervalText = new TextBlock
            {
                FontSize = 15, Foreground = Glass.Text, FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            var less = IconButton(Chevron(false), Glass.TextDim, () => _c.StepInterval(-1));
            var more = IconButton(Chevron(true), Glass.TextDim, () => _c.StepInterval(1));
            less.ToolTip = "أقل 5 دقائق";
            more.ToolTip = "أكثر 5 دقائق";
            var top = new Grid { FlowDirection = FlowDirection.LeftToRight };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(less, 0); Grid.SetColumn(_intervalText, 1); Grid.SetColumn(more, 2);
            top.Children.Add(less); top.Children.Add(_intervalText); top.Children.Add(more);

            _colors = new ColorPicker(() => _c.State.GlowColor, key => _c.SetGlowColor(key), 16);
            var done = IconButton(Geometry.Parse("M 8,13.5 L 11.5,17 L 18,9.5"), Glass.Accent, () => SetSettingsMode(false));
            done.HorizontalAlignment = HorizontalAlignment.Right;
            done.ToolTip = "تم";
            var bottom = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 6, 0, 0) };
            bottom.Children.Add(_colors);
            bottom.Children.Add(done);

            var view = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0, Visibility = Visibility.Collapsed,
            };
            view.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            view.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(bottom, 1);
            view.Children.Add(top);
            view.Children.Add(bottom);
            return view;
        }

        void SetSettingsMode(bool on)
        {
            _settingsMode = on;
            _content.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            _settings.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            Refresh(0);
            Touched();
        }

        void Later(int ms, Action a)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (s, e) => { t.Stop(); _script.Remove(t); a(); };
            _script.Add(t);
            t.Start();
        }

        void CancelScript()
        {
            foreach (var t in _script) t.Stop();
            _script.Clear();
        }

        // ---------- frame loop (runs only while something moves) ----------

        void Kick()
        {
            if (_rendering) return;
            _rendering = true;
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnFrame;
        }

        void OnFrame(object sender, EventArgs e)
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == _lastFrame) return;
            double dt = _lastFrame == TimeSpan.Zero ? 1 / 60.0 : Math.Min((now - _lastFrame).TotalSeconds, 1 / 30.0);
            _lastFrame = now;

            _w.Step(dt); _h.Step(dt); _inset.Step(dt); _s.Step(dt); _p.Step(dt); _glow.Step(dt);
            if (_winMoving)
            {
                _winX.Step(dt); _winY.Step(dt);
                Left = _winX.Value; Top = _winY.Value;
                if (_winX.Settled && _winY.Settled) _winMoving = false;
            }
            Apply();

            if (!_winMoving && _w.Settled && _h.Settled && _inset.Settled && _s.Settled && _p.Settled && _glow.Settled)
            {
                CompositionTarget.Rendering -= OnFrame;
                _rendering = false;
            }
        }

        void Apply()
        {
            double w = Math.Max(4, _w.Value), h = Math.Max(4, _h.Value), inset = _inset.Value;
            double x = _rightSide ? WinW - inset - w : inset, y = (WinH - h) / 2;
            Canvas.SetLeft(_panel, x);
            Canvas.SetTop(_panel, y);
            _panel.Width = w;
            _panel.Height = h;
            double r = Math.Min(Math.Min(w, h) / 2, Radius);
            _panel.CornerRadius = new CornerRadius(r);
            _clip.Rect = new Rect(0, 0, Math.Max(0, w - 2), Math.Max(0, h - 2));
            _clip.RadiusX = _clip.RadiusY = Math.Max(0, r - 1);

            double p = Clamp((w - PeekW) / (TargetW - PeekW), 0, 1);
            _grip.Opacity = Clamp(1 - p * 4, 0, 1);
            _content.Opacity = _settings.Opacity = Clamp((p - 0.55) / 0.45, 0, 1);
            _content.IsHitTestVisible = _settings.IsHitTestVisible = _mode == Mode.Expanded;

            // Glow halo (only attached while visible, so it costs nothing at rest).
            double glow = _glow.Value;
            if (glow > 0.01)
            {
                if (_panel.Effect != _glowFx) _panel.Effect = _glowFx;
                _glowFx.Opacity = Math.Min(1, glow);
            }
            else if (_panel.Effect != null) _panel.Effect = null;

            // Jelly: a tug stretches the panel along X toward the edge; fast width changes squash Y a little.
            double pull = _p.Value;
            double vel = Clamp(_w.Velocity / 3600, -0.1, 0.1);
            _stretch.ScaleX = 1 + pull / 70;
            _stretch.ScaleY = 1 - pull / 160 - vel;
            _stretch.CenterX = _rightSide ? w : 0;
            _stretch.CenterY = h / 2;
            _squash.ScaleX = _squash.ScaleY = _s.Value;
            _squash.CenterX = w / 2;
            _squash.CenterY = h / 2;
            _pull.X = _rightSide ? pull : -pull;

            // Window region = the panel's on-screen shape: blur + hit-testing stay inside it.
            if (_hwnd != IntPtr.Zero)
            {
                Rect b = _panel.RenderTransform.TransformBounds(new Rect(0, 0, w, h));
                b.Offset(x, y);
                b.Intersect(new Rect(0, 0, WinW, WinH));
                if (b.IsEmpty) b = new Rect(0, 0, 1, 1);
                _shape = b;
                _shapeRadius = r * Math.Min(_stretch.ScaleY, 1) * _s.Value;

                // While glowing, the region grows so the halo isn't cut off (the blur window keeps the exact shape).
                Rect region = b;
                double regionRadius = _shapeRadius;
                if (glow > 0.01)
                {
                    region.Inflate(GlowRoom, GlowRoom);
                    region.Intersect(new Rect(0, 0, WinW, WinH));
                    regionRadius += GlowRoom;
                }
                if (!AlmostSame(region, _lastRegion))
                {
                    _lastRegion = region;
                    Glass.SetRoundRegion(_hwnd, region, regionRadius);
                }
                UpdateBackdrop();
            }
        }

        /// <summary>Keep the blur window glued under the panel (also called while the window is dragged).</summary>
        void UpdateBackdrop()
        {
            if (_hwnd == IntPtr.Zero || _shape.IsEmpty) return;
            if (!IsVisible) { _backdrop.Conceal(); return; }
            _backdrop.Follow(new Rect(Left + _shape.X, Top + _shape.Y, _shape.Width, _shape.Height), _shapeRadius, _hwnd);
        }

        static bool AlmostSame(Rect a, Rect b)
        {
            return !b.IsEmpty && Math.Abs(a.X - b.X) < 0.3 && Math.Abs(a.Y - b.Y) < 0.3
                && Math.Abs(a.Width - b.Width) < 0.3 && Math.Abs(a.Height - b.Height) < 0.3;
        }

        // ---------- mouse ----------

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = true;
            _dragging = false;
            _downCursor = CursorDip();
            _downLeft = Left; _downTop = Top;
            _panel.CaptureMouse();
            _s.Tune(500, 0.7); _s.Target = 0.96; Kick();
            e.Handled = true;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (!_mouseDown) return;
            var p = CursorDip();
            double dx = p.X - _downCursor.X, dy = p.Y - _downCursor.Y;
            if (!_dragging && dx * dx + dy * dy > 36)
            {
                _dragging = true;
                _winMoving = false;
                CancelScript();
                _idle.Stop();
                _mode = Mode.Dragging;
                _s.Tune(400, 0.6); _s.Target = 1;
                _w.Tune(260, 0.7); _h.Tune(260, 0.7); _inset.Tune(260, 0.7);
                _w.Target = HandleW; _h.Target = HandleH; _inset.Target = 6; // fully visible while carried
                Kick();
            }
            if (_dragging) { Left = _downLeft + dx; Top = _downTop + dy; }
        }

        void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (!_mouseDown) return;
            bool wasDrag = _dragging;
            _mouseDown = false;
            _dragging = false;
            _panel.ReleaseMouseCapture();
            _s.Tune(380, 0.35); _s.Target = 1; Kick();
            if (wasDrag) SnapToEdge();
            else if (_attention) Acknowledge();                // it was calling for attention: show the dhikr
            else if (_mode != Mode.Expanded) Expand(false);    // tap on the handle opens it
            else if (!_deleteMode && !_settingsMode) _c.Increment(); // tap on the open card counts
            e.Handled = true;
        }

        // ---------- placement ----------

        double PanelCenterX { get { return _rightSide ? WinW - _inset.Value - _w.Value / 2 : _inset.Value + _w.Value / 2; } }

        void SetSide(bool right)
        {
            _rightSide = right;
            _grip.HorizontalAlignment = right ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            _grip.Margin = right ? new Thickness(4, 0, 0, 0) : new Thickness(0, 0, 4, 0);
            _lastRegion = Rect.Empty;
        }

        /// <summary>Snap to the nearest left/right edge of whichever screen the widget was dropped on.</summary>
        void SnapToEdge()
        {
            _dragging = false;
            var center = new Point(Left + PanelCenterX, Top + WinH / 2);
            string screen;
            Rect wa = Screens.WorkAreaAt(center, out screen);
            bool right = center.X > wa.Left + wa.Width / 2;
            if (right != _rightSide)
            {
                double old = PanelCenterX;
                SetSide(right);
                Left += old - PanelCenterX; // keep it visually in place, then glide
            }

            var st = _c.State;
            st.Side = right ? "Right" : "Left";
            st.VerticalRatio = Clamp((center.Y - wa.Top) / wa.Height, 0, 1);
            st.Screen = screen;
            _c.SaveSoon();

            _winX = new Spring(Left).Tune(170, 0.7);
            _winY = new Spring(Top).Tune(170, 0.8);
            _winX.Epsilon = _winY.Epsilon = 0.3;
            _winX.Target = TargetLeft(wa);
            _winY.Target = TargetTop(wa, st.VerticalRatio);
            _winMoving = true;
            Collapse();
            if (_attention) AttentionRound(1200); // dragging doesn't dismiss a pending reminder
        }

        /// <summary>Place from saved state (startup, resolution / taskbar changes, side change in Settings).</summary>
        public void Place()
        {
            var st = _c.State;
            Rect wa = Screens.WorkAreaByName(st.Screen);
            _winMoving = false;
            SetSide(st.Side != "Left");
            Left = TargetLeft(wa);
            Top = TargetTop(wa, st.VerticalRatio);
            Apply();
        }

        double TargetLeft(Rect wa) { return _rightSide ? wa.Right - WinW : wa.Left; }

        static double TargetTop(Rect wa, double ratio)
        {
            const double half = 70; // keep the expanded card fully inside the work area
            double cy = wa.Top + ratio * wa.Height;
            cy = Clamp(cy, wa.Top + half, Math.Max(wa.Top + half, wa.Bottom - half));
            return cy - WinH / 2;
        }

        /// <summary>Where popups should appear: the screen-edge point level with the widget.</summary>
        public Point EdgeAnchor { get { return new Point(_rightSide ? Left + WinW : Left, Top + WinH / 2); } }
        public bool OnRightSide { get { return _rightSide; } }

        static Point CursorDip()
        {
            var p = System.Windows.Forms.Cursor.Position;
            return new Point(p.X / Screens.Scale, p.Y / Screens.Scale);
        }

        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
