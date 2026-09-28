using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Dhikr
{
    /// <summary>Settings: reminder interval, start with Windows, screen side. Changes apply immediately.</summary>
    public class SettingsWindow : GlassPopup
    {
        readonly Controller _c;
        readonly List<Action> _refreshers = new List<Action>();

        public SettingsWindow(Controller c, Point edgeAnchor, bool rightSide) : base(edgeAnchor, rightSide, "الإعدادات")
        {
            _c = c;
            var panel = new StackPanel { Width = 270 };
            panel.Children.Add(Label("الإعدادات", 15, Glass.Text));

            // Adhkar: tap one to show it now, × removes your own additions, + adds a new one.
            panel.Children.Add(Section("الأذكار"));
            panel.Children.Add(new ScrollViewer
            {
                Content = _list, MaxHeight = 230, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });
            var add = AccentButton("+  إضافة ذكر", () => _c.ShowAddDialog());
            add.HorizontalAlignment = HorizontalAlignment.Stretch;
            ((TextBlock)add.Child).HorizontalAlignment = HorizontalAlignment.Center;
            add.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(add);
            _refreshers.Add(BuildList);
            BuildList();

            panel.Children.Add(Section("مدة التذكير"));
            var options = new List<KeyValuePair<string, Action>>();
            var selected = new List<Func<bool>>();
            foreach (int m in Defaults.ReminderOptions)
            {
                int minutes = m;
                options.Add(new KeyValuePair<string, Action>(Controller.IntervalShortLabel(m), () => _c.SetInterval(minutes)));
                selected.Add(() => _c.State.ReminderMinutes == minutes);
            }
            panel.Children.Add(Segmented(options, selected));

            var startupRow = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
            var toggle = Toggle(() => _c.State.StartWithWindows == true, on => _c.SetStartWithWindows(on));
            var startupLabel = Label("التشغيل مع Windows", 14, Glass.Text);
            DockPanel.SetDock(startupLabel, Dock.Left); // RTL mirrors: label at the visual right,
            DockPanel.SetDock(toggle, Dock.Right);      // switch at the visual left end
            startupRow.Children.Add(startupLabel);
            startupRow.Children.Add(toggle);
            panel.Children.Add(startupRow);

            panel.Children.Add(Section("مكان الـWidget"));
            panel.Children.Add(Segmented(
                new List<KeyValuePair<string, Action>>
                {
                    new KeyValuePair<string, Action>("يمين الشاشة", () => _c.SetSide("Right")),
                    new KeyValuePair<string, Action>("يسار الشاشة", () => _c.SetSide("Left")),
                },
                new List<Func<bool>> { () => _c.State.Side != "Left", () => _c.State.Side == "Left" }));

            Body = panel;
        }

        readonly StackPanel _list = new StackPanel();

        void BuildList()
        {
            _list.Children.Clear();
            var adhkar = _c.State.Adhkar;
            for (int i = 0; i < adhkar.Count; i++)
            {
                int index = i;
                var item = adhkar[i];
                bool current = i == _c.State.CurrentIndex;

                var row = new DockPanel { LastChildFill = true };
                if (item.Custom)
                {
                    var del = new TextBlock
                    {
                        Text = "✕", FontSize = 12, Foreground = Glass.TextDim, Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "حذف",
                    };
                    del.MouseEnter += (s, e) => del.Foreground = Glass.Text;
                    del.MouseLeave += (s, e) => del.Foreground = Glass.TextDim;
                    del.MouseLeftButtonUp += (s, e) => { e.Handled = true; _c.DeleteAt(index); RefreshAll(); };
                    DockPanel.SetDock(del, Dock.Right);
                    row.Children.Add(del);
                }
                var count = new TextBlock
                {
                    Text = item.Count.ToString(), FontSize = 12, Foreground = Glass.TextDim,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
                };
                DockPanel.SetDock(count, Dock.Right);
                row.Children.Add(count);
                row.Children.Add(new TextBlock
                {
                    Text = item.Text, FontSize = 13.5, TextWrapping = TextWrapping.Wrap,
                    Foreground = current ? Glass.Text : Glass.TextDim, VerticalAlignment = VerticalAlignment.Center,
                });

                var cell = new Border
                {
                    Child = row, CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 4, 8, 5),
                    Margin = new Thickness(0, 0, 0, 2), Cursor = Cursors.Hand,
                    Background = current ? Glass.HexBrush("#26FFFFFF") : Brushes.Transparent,
                };
                cell.MouseEnter += (s, e) => { if (!current) cell.Background = Glass.Subtle; };
                cell.MouseLeave += (s, e) => { if (!current) cell.Background = Brushes.Transparent; };
                cell.MouseLeftButtonUp += (s, e) => { _c.Select(index); RefreshAll(); };
                _list.Children.Add(cell);
            }
        }

        static TextBlock Section(string text)
        {
            var t = Label(text);
            t.Margin = new Thickness(0, 14, 0, 6);
            return t;
        }

        UIElement Segmented(List<KeyValuePair<string, Action>> items, List<Func<bool>> isSelected)
        {
            var grid = new UniformGrid { Rows = 1 };
            var host = new Border { CornerRadius = new CornerRadius(10), Background = Glass.Subtle, Padding = new Thickness(3), Child = grid };
            var cells = new List<Border>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var cell = new Border
                {
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3, 6, 5), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = item.Key, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center },
                };
                cell.MouseLeftButtonUp += (s, e) => { item.Value(); RefreshAll(); };
                cells.Add(cell);
                grid.Children.Add(cell);
            }
            Action refresh = () =>
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    bool on = isSelected[i]();
                    cells[i].Background = on ? Glass.HexBrush("#30FFFFFF") : Brushes.Transparent;
                    ((TextBlock)cells[i].Child).Foreground = on ? Glass.Text : Glass.TextDim;
                }
            };
            _refreshers.Add(refresh);
            refresh();
            return host;
        }

        UIElement Toggle(Func<bool> get, Action<bool> set)
        {
            var knobMove = new TranslateTransform();
            var knob = new Ellipse { Width = 14, Height = 14, Fill = Glass.Text, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = knobMove };
            var track = new Border
            {
                Width = 38, Height = 20, CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Cursor = Cursors.Hand,
                FlowDirection = FlowDirection.LeftToRight, Child = knob, VerticalAlignment = VerticalAlignment.Center,
            };
            Action refresh = () =>
            {
                bool on = get();
                track.Background = on ? Glass.Accent : Glass.HexBrush("#30FFFFFF");
                knob.Fill = on ? Glass.HexBrush("#111614") : Glass.Text;
                knobMove.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(on ? 18 : 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            };
            track.MouseLeftButtonUp += (s, e) => { set(!get()); RefreshAll(); };
            _refreshers.Add(refresh);
            refresh();
            return track;
        }

        void RefreshAll() { foreach (var r in _refreshers) r(); }
    }
}
