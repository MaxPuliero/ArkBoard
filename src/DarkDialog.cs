using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkBoard
{
    internal static class DarkDialog
    {
        internal const string DownloadUrl = "https://maxpuliero.gumroad.com/l/ArkBoard";
        static Brush B(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }

        static Window Create(Window owner, string title, string message, out StackPanel buttons)
        {
            Grid layout; return Create(owner, title, message, out buttons, out layout);
        }

        static Window Create(Window owner, string title, string message, out StackPanel buttons, out Grid layout)
        {
            var window = new Window { Owner = owner, Title = title, Width = 570, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = B("#232323"), Foreground = B("#ECECEC"),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 13 };
            window.Resources = owner.Resources;
            var frame = new Border { BorderBrush = B("#505050"), BorderThickness = new Thickness(1), Background = B("#232323") };
            layout = new Grid(); frame.Child = layout; window.Content = frame;
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            var header = new Grid { Background = B("#1D1D1D") };
            var heading = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 45, 0) };
            header.Children.Add(heading);
            var x = FlatButton("×"); x.Width = 42; x.HorizontalAlignment = HorizontalAlignment.Right;
            x.Click += delegate { window.DialogResult = null; window.Close(); }; header.Children.Add(x);
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) window.DragMove(); };
            layout.Children.Add(header);
            var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 21,
                Margin = new Thickness(22, 20, 22, 18), Foreground = B("#D8D8D8") };
            Grid.SetRow(body, 1); layout.Children.Add(body);
            buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 7, 16, 10) };
            Grid.SetRow(buttons, 2); layout.Children.Add(buttons);
            window.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { window.DialogResult = null; window.Close(); } };
            return window;
        }

        static Button FlatButton(string text)
        {
            return new Button { Content = text, Background = B("#323232"), Foreground = B("#ECECEC"), BorderBrush = B("#4A4A4A"),
                BorderThickness = new Thickness(1), Padding = new Thickness(13, 5, 13, 5), Margin = new Thickness(6, 0, 0, 0), MinWidth = 78,
                Cursor = Cursors.Hand };
        }

        internal static bool? ConfirmSave(Window owner)
        {
            StackPanel buttons; Window window = Create(owner, "ArkBoard", Localization.T("Save changes to this project?"), out buttons);
            Button save = FlatButton(Localization.T("Save")); save.Click += delegate { window.DialogResult = true; }; buttons.Children.Add(save);
            Button discard = FlatButton(Localization.T("Don't Save")); discard.Click += delegate { window.DialogResult = false; }; buttons.Children.Add(discard);
            Button cancel = FlatButton(Localization.T("Cancel")); cancel.Click += delegate { window.DialogResult = null; window.Close(); }; buttons.Children.Add(cancel);
            return window.ShowDialog();
        }

        internal static double? PromptNumber(Window owner, string title, string message, double value, double minimum, double maximum)
        {
            StackPanel buttons; Grid layout;
            Window window = Create(owner, title, message, out buttons, out layout);
            TextBlock originalBody = layout.Children.OfType<TextBlock>().FirstOrDefault(block => Grid.GetRow(block) == 1);
            if (originalBody != null) layout.Children.Remove(originalBody);
            var body = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
            body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap,
                Foreground = B("#D8D8D8"), Margin = new Thickness(0, 0, 0, 12) });
            var input = new TextBox { Text = value.ToString("0.##", CultureInfo.InvariantCulture), Width = 180,
                HorizontalAlignment = HorizontalAlignment.Left };
            body.Children.Add(input);
            var error = new TextBlock { Foreground = B("#C8A0A0"), Margin = new Thickness(0, 8, 0, 0) };
            body.Children.Add(error); Grid.SetRow(body, 1); layout.Children.Add(body);
            double result = value;
            Action accept = delegate
            {
                double parsed;
                bool valid = double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
                    double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
                if (!valid || double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed < minimum || parsed > maximum)
                { error.Text = Localization.T("Enter a value from 0 to 10,000 pixels."); input.Focus(); input.SelectAll(); return; }
                result = parsed; window.DialogResult = true;
            };
            Button apply = FlatButton(Localization.T("Apply")); apply.Click += delegate { accept(); }; buttons.Children.Add(apply);
            Button cancel = FlatButton(Localization.T("Cancel")); cancel.Click += delegate { window.DialogResult = null; window.Close(); }; buttons.Children.Add(cancel);
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { accept(); e.Handled = true; } };
            window.ContentRendered += delegate { input.Focus(); input.SelectAll(); };
            return window.ShowDialog() == true ? (double?)result : null;
        }

        internal static void ShowAbout(Window owner, string message)
        {
            StackPanel buttons; Window window = Create(owner, Localization.T("About ArkBoard"), message, out buttons);
            Button gumroad = FlatButton("Gumroad");
            gumroad.ToolTip = "Download ArkBoard";
            gumroad.Click += delegate { Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true }); };
            buttons.Children.Add(gumroad);
            Button github = FlatButton("GitHub");
            github.Click += delegate { Process.Start(new ProcessStartInfo("https://github.com/MaxPuliero/ArkBoard") { UseShellExecute = true }); };
            buttons.Children.Add(github);
            Button close = FlatButton(Localization.T("Close")); close.Click += delegate { window.DialogResult = true; }; buttons.Children.Add(close);
            window.ShowDialog();
        }
    }
}
