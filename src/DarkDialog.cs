using System;
using System.Diagnostics;
using System.Globalization;
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

        static Window Create(Window owner, string title, string message, out StackPanel buttons, FrameworkElement content = null)
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
            x.Click += delegate { window.Close(); }; header.Children.Add(x);
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) window.DragMove(); };
            layout.Children.Add(header);
            var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 21,
                Margin = new Thickness(22, 20, 22, 18), Foreground = B("#D8D8D8") };
            FrameworkElement bodyContent = content ?? body;
            Grid.SetRow(bodyContent, 1); layout.Children.Add(bodyContent);
            buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 7, 16, 10) };
            Grid.SetRow(buttons, 2); layout.Children.Add(buttons);
            window.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) window.Close(); };
            return window;
        }

        static Button FlatButton(string text)
        {
            return new Button { Content = text, Background = B("#323232"), Foreground = B("#ECECEC"), BorderBrush = B("#4A4A4A"),
                BorderThickness = new Thickness(1), Padding = new Thickness(13, 5, 13, 5), Margin = new Thickness(6, 0, 0, 0), MinWidth = 78,
                Cursor = Cursors.Hand };
        }

        internal sealed class CanvasExportSettings
        {
            internal int Width, Height;
            internal bool Transparent;
        }
        internal static CanvasExportSettings ConfigureCanvasExport(Window owner, Rect bounds)
        {
            var body = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            body.Children.Add(new TextBlock { Text = Localization.T("Selection size in pixels · proportions locked"), Margin = new Thickness(0, 0, 0, 14) });
            var dimensions = new Grid();
            dimensions.ColumnDefinitions.Add(new ColumnDefinition());
            dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            dimensions.ColumnDefinitions.Add(new ColumnDefinition());
            var widthPanel = new StackPanel(); var heightPanel = new StackPanel();
            widthPanel.Children.Add(new TextBlock { Text = Localization.T("Width (px)"), Margin = new Thickness(0, 0, 0, 6) });
            heightPanel.Children.Add(new TextBlock { Text = Localization.T("Height (px)"), Margin = new Thickness(0, 0, 0, 6) });
            var width = new TextBox { Padding = new Thickness(8), MaxLength = 8 };
            var height = new TextBox { Padding = new Thickness(8), MaxLength = 8 };
            widthPanel.Children.Add(width); heightPanel.Children.Add(height);
            dimensions.Children.Add(widthPanel); Grid.SetColumn(heightPanel, 2); dimensions.Children.Add(heightPanel); body.Children.Add(dimensions);
            var transparent = new CheckBox { Content = Localization.T("Transparent background"), Foreground = B("#ECECEC"), IsChecked = true, Margin = new Thickness(0, 18, 0, 8) };
            body.Children.Add(transparent);
            body.Children.Add(new TextBlock { Text = Localization.T("When disabled, the background is white."), Foreground = B("#AAAAAA"), TextWrapping = TextWrapping.Wrap });
            var hint = new TextBlock { Text = Localization.T("Use 1–16,384 pixels per side, up to 80 megapixels."), Foreground = B("#AAAAAA"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
            body.Children.Add(hint);
            StackPanel buttons; Window window = Create(owner, Localization.T("Export Canvas"), "", out buttons, body);
            window.Width = 500;
            Button export = FlatButton(Localization.T("Export")); export.IsDefault = true;
            Button cancel = FlatButton(Localization.T("Cancel")); cancel.IsCancel = true;
            buttons.Children.Add(export); buttons.Children.Add(cancel);
            double ratio = bounds.Width / bounds.Height; bool syncing = false;
            Action<TextBox, TextBox, bool> update = (edited, other, editingWidth) =>
            {
                if (syncing) return;
                int n;
                syncing = true;
                if (int.TryParse(edited.Text, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n > 0)
                {
                    double linked = Math.Max(1, Math.Round(editingWidth ? n / ratio : n * ratio));
                    other.Text = linked <= int.MaxValue ? linked.ToString("0", CultureInfo.InvariantCulture) : "";
                }
                syncing = false;
                int w, h;
                export.IsEnabled = int.TryParse(width.Text, out w) && int.TryParse(height.Text, out h) && SelectionExport.ValidSize(w, h);
                hint.Foreground = export.IsEnabled ? B("#AAAAAA") : B("#E3AB8B");
            };
            width.TextChanged += delegate { update(width, height, true); };
            height.TextChanged += delegate { update(height, width, false); };
            Size initial = SelectionExport.DefaultSize(bounds, 1);
            width.Text = ((int)initial.Width).ToString(CultureInfo.InvariantCulture);
            if (!export.IsEnabled) { width.Text = Math.Max(1, (int)initial.Width - 1).ToString(CultureInfo.InvariantCulture); }
            export.Click += delegate { if (export.IsEnabled) window.DialogResult = true; };
            window.ContentRendered += delegate { width.Focus(); width.SelectAll(); };
            if (window.ShowDialog() != true) return null;
            return new CanvasExportSettings { Width = int.Parse(width.Text, CultureInfo.InvariantCulture), Height = int.Parse(height.Text, CultureInfo.InvariantCulture), Transparent = transparent.IsChecked == true };
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

        internal static void ShowNotice(Window owner, string title, string message)
        {
            StackPanel buttons; Window window = Create(owner, title, message, out buttons); window.Width = 440;
            Button close = FlatButton(Localization.T("Close")); close.Click += delegate { window.DialogResult = true; }; buttons.Children.Add(close);
            window.ShowDialog();
        }

        internal static void ShowUpdate(Window owner, Version current, UpdateInfo update, Action downloadAction)
        {
            string message = string.Format(Localization.T("ArkBoard {0} is available. You are using {1}."), update.Version, current);
            StackPanel buttons; Window window = Create(owner, Localization.T("Update available"), message, out buttons);
            Button download = FlatButton(Localization.T("Download"));
            download.Click += delegate
            {
                window.Close();
                if (downloadAction != null) downloadAction();
            };
            buttons.Children.Add(download);
            Button close = FlatButton(Localization.T("Close")); close.Click += delegate { window.Close(); }; buttons.Children.Add(close);
            window.Show();
        }
    }
}
