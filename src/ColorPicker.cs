using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ArkBoard
{
    public sealed partial class MainWindow
    {
        const int ColorSlotCount = 11;
        internal readonly Color?[] pickedColors = new Color?[ColorSlotCount];
        readonly Button[] colorSlots = new Button[ColorSlotCount];
        readonly Grid[] colorTracks = new Grid[4];
        readonly Border[] colorIndicators = new Border[4];
        readonly double[] colorFractions = new double[4];
        readonly TextBlock[] colorLabels = new TextBlock[4];
        readonly TextBlock[] colorValues = new TextBlock[4];
        internal Button colorMode;
        internal TextBox colorCode;
        internal StackPanel colorPickerSection;
        int nextColorSlot, selectedColorSlot = -1;
        bool rgbaMode;

        void BuildColorPicker(StackPanel parent)
        {
            var section = new StackPanel { Margin = new Thickness(0, 28, 0, 0) };
            colorPickerSection = section;
            parent.Children.Add(section);
            section.Children.Add(new Border { Height = 1, Background = Brush("#393939"), Margin = new Thickness(0, 0, 0, 12) });
            TextBlock title = Label("Color Picker", 15, secondary);
            title.Margin = new Thickness(0, 0, 0, 10);
            section.Children.Add(title);

            var swatches = new UniformGrid { Columns = ColorSlotCount, Margin = new Thickness(0, 0, 0, 8) };
            section.Children.Add(swatches);
            for (int i = 0; i < ColorSlotCount; i++)
            {
                int slot = i;
                var button = new Button { Height = 24, Margin = new Thickness(1), Padding = new Thickness(0),
                    Background = Brushes.Black, BorderBrush = Brush("#777777"), BorderThickness = new Thickness(1),
                    ToolTip = "Color " + (i + 1).ToString(CultureInfo.InvariantCulture) };
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
                border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
                border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
                template.VisualTree = border; button.Template = template;
                button.Click += delegate { SelectColorSlot(slot); };
                colorSlots[i] = button; swatches.Children.Add(button);
            }

            colorMode = new Button { Content = "HSL", Width = 82, Height = 25,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8),
                ToolTip = "HSL / RGBA" };
            colorMode.Click += delegate { rgbaMode = !rgbaMode; RefreshColorControls(); };
            section.Children.Add(colorMode);
            for (int i = 0; i < 4; i++)
            {
                int component = i;
                var row = new Grid { Height = 29 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(23) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                var label = Label("", 11, secondary); label.VerticalAlignment = VerticalAlignment.Center;
                var track = new Grid { Height = 10, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 6, 0), IsHitTestVisible = false, Background = Brush("#777777") };
                var indicator = new Border { Width = 2, Height = 16, Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                track.Children.Add(indicator);
                track.SizeChanged += delegate { PositionColorIndicator(component); };
                var value = Label("", 11, text); value.TextAlignment = TextAlignment.Right;
                value.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(label, 0); Grid.SetColumn(track, 1); Grid.SetColumn(value, 2);
                row.Children.Add(label); row.Children.Add(track); row.Children.Add(value);
                colorLabels[i] = label; colorTracks[i] = track; colorIndicators[i] = indicator; colorValues[i] = value;
                section.Children.Add(row);
            }
            colorCode = new TextBox { IsReadOnly = true, Width = 145, Height = 29,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0),
                FontSize = 15, Foreground = text, Background = Brush("#191919"),
                BorderBrush = Brush("#454545"), Padding = new Thickness(6, 3, 3, 3) };
            colorCode.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                colorCode.Focus(); colorCode.SelectAll(); e.Handled = true;
            };
            colorCode.GotKeyboardFocus += delegate { colorCode.SelectAll(); };
            section.Children.Add(colorCode);
            RefreshColorControls();
        }

        internal void AddPickedColor(Color color)
        {
            pickedColors[nextColorSlot] = color;
            selectedColorSlot = nextColorSlot;
            nextColorSlot = (nextColorSlot + 1) % ColorSlotCount;
            RefreshColorControls();
        }

        void SelectColorSlot(int slot)
        {
            if (!pickedColors[slot].HasValue) return;
            selectedColorSlot = slot;
            RefreshColorControls();
        }

        void RefreshColorControls()
        {
            if (colorMode == null || colorCode == null) return;
            bool hsl = !rgbaMode;
            colorMode.Content = hsl ? "HSL" : "RGBA";
            string[] labels = hsl ? new[] { "H", "S", "L", "A" } : new[] { "R", "G", "B", "A" };
            Color? selected = selectedColorSlot < 0 ? null : pickedColors[selectedColorSlot];
            double[] values = new double[4];
            double selectedHue = 0;
            if (selected.HasValue)
            {
                Color c = selected.Value;
                if (hsl)
                {
                    double hue, saturation, lightness;
                    ToHsl(c, out hue, out saturation, out lightness);
                    selectedHue = hue;
                    values = new[] { hue, saturation, lightness, c.A * 100.0 / 255 };
                }
                else values = new double[] { c.R, c.G, c.B, c.A };
            }
            for (int i = 0; i < 4; i++)
            {
                colorLabels[i].Text = labels[i];
                colorTracks[i].Background = ColorTrackBrush(hsl, i, selectedHue);
                colorFractions[i] = selected.HasValue ? Math.Max(0, Math.Min(1, values[i] / (hsl ? (i == 0 ? 360 : 100) : 255))) : 0;
                colorIndicators[i].Visibility = selected.HasValue ? Visibility.Visible : Visibility.Collapsed;
                PositionColorIndicator(i);
                colorValues[i].Text = selected.HasValue ? Math.Round(values[i]).ToString("0", CultureInfo.InvariantCulture) : "—";
            }
            for (int i = 0; i < ColorSlotCount; i++)
            {
                Color? c = pickedColors[i];
                colorSlots[i].Background = c.HasValue ? new SolidColorBrush(c.Value) : Brushes.Black;
                colorSlots[i].BorderBrush = i == selectedColorSlot ? Brushes.White : Brush("#777777");
                colorSlots[i].BorderThickness = new Thickness(i == selectedColorSlot ? 3 : 1);
            }
            colorCode.Text = selected.HasValue ? ColorHex(selected.Value) : "";
        }

        void PositionColorIndicator(int component)
        {
            if (colorTracks[component] == null) return;
            double available = Math.Max(0, colorTracks[component].ActualWidth - colorIndicators[component].Width);
            colorIndicators[component].Margin = new Thickness(colorFractions[component] * available, 0, 0, 0);
        }

        static Brush HueSpectrum()
        {
            return new LinearGradientBrush(new GradientStopCollection {
                new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, 1.0 / 6),
                new GradientStop(Colors.Lime, 2.0 / 6), new GradientStop(Colors.Cyan, 3.0 / 6),
                new GradientStop(Colors.Blue, 4.0 / 6), new GradientStop(Colors.Magenta, 5.0 / 6),
                new GradientStop(Colors.Red, 1) }, new Point(0, 0), new Point(1, 0));
        }

        static Brush ColorTrackBrush(bool hsl, int component, double hue)
        {
            if (hsl && component == 0) return HueSpectrum();
            if (hsl && component == 1)
                return Gradient(Colors.Gray, FromHsl(hue, 1, .5));
            if ((hsl && component >= 2) || (!hsl && component == 3))
                return Gradient(Colors.Black, Colors.White);
            return Gradient(Colors.Black, component == 0 ? Colors.Red : component == 1 ? Colors.Lime : Colors.Blue);
        }

        static Brush Gradient(Color start, Color end)
        {
            return new LinearGradientBrush(start, end, new Point(0, 0), new Point(1, 0));
        }

        static Color FromHsl(double hue, double saturation, double lightness)
        {
            double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double sector = (hue % 360) / 60;
            double second = chroma * (1 - Math.Abs(sector % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (sector < 1) { r = chroma; g = second; }
            else if (sector < 2) { r = second; g = chroma; }
            else if (sector < 3) { g = chroma; b = second; }
            else if (sector < 4) { g = second; b = chroma; }
            else if (sector < 5) { r = second; b = chroma; }
            else { r = chroma; b = second; }
            double match = lightness - chroma / 2;
            return Color.FromRgb((byte)Math.Round((r + match) * 255),
                (byte)Math.Round((g + match) * 255), (byte)Math.Round((b + match) * 255));
        }

        static string ColorHex(Color c)
        {
            return c.A == 255 ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
                : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.R, c.G, c.B, c.A);
        }

        static void ToHsl(Color c, out double hue, out double saturation, out double lightness)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            lightness = (max + min) / 2;
            saturation = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * lightness - 1));
            hue = 0;
            if (delta > 0)
            {
                if (max == r) hue = ((g - b) / delta) % 6;
                else if (max == g) hue = (b - r) / delta + 2;
                else hue = (r - g) / delta + 4;
                hue = (hue * 60 + 360) % 360;
            }
            saturation *= 100; lightness *= 100;
        }

    }
}
