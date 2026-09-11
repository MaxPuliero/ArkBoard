using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkBoard
{
    internal static class DarkDialog
    {
        static Brush B(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }

        static Window Create(Window owner, string title, string message, out StackPanel buttons)
        {
            var window = new Window { Owner = owner, Title = title, Width = 570, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = B("#232323"), Foreground = B("#ECECEC"),
                FontFamily = new FontFamily("Segoe UI"), FontSize = 13 };
            window.Resources = owner.Resources;
            var frame = new Border { BorderBrush = B("#505050"), BorderThickness = new Thickness(1), Background = B("#232323") };
            var layout = new Grid(); frame.Child = layout; window.Content = frame;
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

        internal static void ShowAbout(Window owner, string message)
        {
            StackPanel buttons; Window window = Create(owner, Localization.T("About ArkBoard"), message, out buttons);
            Button github = FlatButton("GitHub");
            github.Click += delegate { Process.Start(new ProcessStartInfo("https://github.com/MaxPuliero/ArkBoard") { UseShellExecute = true }); };
            buttons.Children.Add(github);
            Button close = FlatButton(Localization.T("Close")); close.Click += delegate { window.DialogResult = true; }; buttons.Children.Add(close);
            window.ShowDialog();
        }
    }
}
