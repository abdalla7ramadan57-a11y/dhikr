using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Dhikr
{
    /// <summary>A row of colour dots for the reminder glow; the selected one gets a light ring.</summary>
    public class ColorPicker : StackPanel
    {
        readonly Func<string> _get;
        readonly List<Border> _dots = new List<Border>();
        static readonly Brush Ring = Glass.HexBrush("#E6FFFFFF");

        public ColorPicker(Func<string> get, Action<string> set, double size = 18)
        {
            _get = get;
            Orientation = Orientation.Horizontal;
            FlowDirection = FlowDirection.LeftToRight;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;

            for (int i = 0; i < Defaults.GlowColors.GetLength(0); i++)
            {
                string key = Defaults.GlowColors[i, 0];
                var dot = new Border
                {
                    Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Margin = new Thickness(3, 0, 3, 0),
                    Background = new SolidColorBrush(Glass.GlowColor(key)), BorderThickness = new Thickness(2),
                    Cursor = Cursors.Hand, ToolTip = Defaults.GlowColors[i, 1], Tag = key,
                };
                dot.MouseLeftButtonDown += (s, e) => e.Handled = true;
                dot.MouseLeftButtonUp += (s, e) => { e.Handled = true; set(key); Refresh(); };
                _dots.Add(dot);
                Children.Add(dot);
            }
            Refresh();
        }

        public void Refresh()
        {
            string current = _get();
            foreach (var d in _dots)
                d.BorderBrush = (string)d.Tag == current ? Ring : Brushes.Transparent;
        }
    }
}
