using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkBoard
{
    internal sealed class LockControlsWindow : Window
    {
        internal readonly Slider OpacitySlider;
        internal readonly Button EyeButton, LockButton;
        readonly TextBlock value;
        bool syncing;
        internal event Action<double> OpacityRequested;
        internal event Action TopmostRequested;
        internal event Action UnlockRequested;

        internal LockControlsWindow(MainWindow owner)
        {
            Owner = owner; Width = 294; Height = 34; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false; ShowActivated = false; Background = (Brush)new BrushConverter().ConvertFromString("#1F1F1F");
            Resources = owner.Resources;
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 5, 8, 5) };
            Content = row;
            OpacitySlider = new Slider { Minimum = 5, Maximum = 100, SmallChange = 1, LargeChange = 5, TickFrequency = 1,
                IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Width = 142, Margin = new Thickness(0, 0, 8, 0) };
            OpacitySlider.ValueChanged += delegate { if (!syncing && OpacityRequested != null) OpacityRequested(OpacitySlider.Value); };
            row.Children.Add(OpacitySlider);
            value = new TextBlock { Width = 39, Foreground = (Brush)new BrushConverter().ConvertFromString("#ECECEC"), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(value);
            EyeButton = IconButton("\uE890", "Always on Top"); EyeButton.Click += delegate { if (TopmostRequested != null) TopmostRequested(); }; row.Children.Add(EyeButton);
            LockButton = IconButton("\uE72E", "Unlock ArkBoard"); LockButton.Background = (Brush)new BrushConverter().ConvertFromString("#555555");
            LockButton.Click += delegate { if (UnlockRequested != null) UnlockRequested(); }; row.Children.Add(LockButton);
        }

        static Button IconButton(string glyph, string tooltip)
        {
            return new Button { Content = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), ToolTip = tooltip,
                Width = 30, Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 0, 0) };
        }

        internal void Sync(double percent, bool topmost)
        {
            syncing = true; OpacitySlider.Value = percent; syncing = false;
            value.Text = percent.ToString("0") + "%";
            EyeButton.Background = (Brush)new BrushConverter().ConvertFromString(topmost ? "#555555" : "#323232");
            Topmost = topmost;
            Opacity = Math.Max(.5, Math.Min(1, percent / 100));
        }
    }
}
