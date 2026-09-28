using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Dhikr
{
    /// <summary>Tiny popup: one text box and an "إضافة" button.</summary>
    public class AddDhikrWindow : GlassPopup
    {
        readonly TextBox _box;
        readonly Border _add;
        readonly Action<string> _onAdd;

        public AddDhikrWindow(Point edgeAnchor, bool rightSide, Action<string> onAdd)
            : base(edgeAnchor, rightSide, "إضافة ذكر")
        {
            _onAdd = onAdd;
            _box = new TextBox
            {
                Width = 250, FontSize = 16, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = Glass.Text, CaretBrush = Glass.Text, SelectionBrush = Glass.Accent,
                TextWrapping = TextWrapping.Wrap, MaxLength = 200, Padding = new Thickness(4, 5, 4, 6),
                FlowDirection = FlowDirection.RightToLeft,
            };
            var field = new Border
            {
                Child = _box, CornerRadius = new CornerRadius(10), Background = Glass.HexBrush("#14FFFFFF"),
                BorderBrush = Glass.HexBrush("#22FFFFFF"), BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 8, 0, 12),
            };
            _add = AccentButton("إضافة", Submit);
            _add.HorizontalAlignment = HorizontalAlignment.Left; // mirrored by RTL: sits at the visual end
            _add.Opacity = 0.45;

            var panel = new StackPanel();
            panel.Children.Add(Label("إضافة ذكر"));
            panel.Children.Add(field);
            panel.Children.Add(_add);
            Body = panel;

            _box.TextChanged += (s, e) => _add.Opacity = _box.Text.Trim().Length > 0 ? 1 : 0.45;
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter) { Submit(); e.Handled = true; } };
        }

        protected override void OnOpened()
        {
            _box.Focus();
            Keyboard.Focus(_box);
        }

        void Submit()
        {
            string text = _box.Text.Trim();
            if (text.Length == 0) { _box.Focus(); return; }
            _onAdd(text);
            SafeClose();
        }
    }
}
