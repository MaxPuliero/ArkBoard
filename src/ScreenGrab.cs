using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;

namespace ArkBoard
{
    // A frozen desktop image lets the selection remain visible while the overlay is on top.
    internal sealed class ScreenGrab : Window
    {
        readonly BitmapSource desktop;
        readonly double pixelsPerDipX, pixelsPerDipY;
        readonly Canvas canvas;
        readonly Rectangle selection;
        readonly Action<BitmapSource> completed;
        Point origin;
        bool dragging, finished;

        internal ScreenGrab(double dipPerPixelX, double dipPerPixelY, Action<BitmapSource> callback)
        {
            completed = callback;
            pixelsPerDipX = 1 / dipPerPixelX;
            pixelsPerDipY = 1 / dipPerPixelY;
            System.Drawing.Rectangle bounds = Forms.SystemInformation.VirtualScreen;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
                IntPtr handle = bitmap.GetHbitmap();
                try { desktop = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); desktop.Freeze(); }
                finally { DeleteObject(handle); }
            }
            Left = bounds.Left * dipPerPixelX; Top = bounds.Top * dipPerPixelY;
            Width = bounds.Width * dipPerPixelX; Height = bounds.Height * dipPerPixelY;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false; Topmost = true; AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Black; Cursor = Cursors.Cross;
            canvas = new Canvas { Width = Width, Height = Height, ClipToBounds = true,
                Background = new ImageBrush(desktop) { Stretch = Stretch.Fill } };
            selection = new Rectangle { Stroke = System.Windows.Media.Brushes.White, StrokeThickness = 1.5,
                Fill = System.Windows.Media.Brushes.Transparent, Visibility = Visibility.Collapsed };
            selection.StrokeDashArray = new DoubleCollection { 5, 3 };
            canvas.Children.Add(selection); Content = canvas;
            KeyDown += OnKeyDown;
            Closed += delegate { if (!finished) { finished = true; completed(null); } };
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            origin = Clamp(e.GetPosition(canvas)); dragging = true;
            Canvas.SetLeft(selection, origin.X); Canvas.SetTop(selection, origin.Y);
            selection.Width = selection.Height = 0; selection.Visibility = Visibility.Visible;
            CaptureMouse(); e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            Point p = Clamp(e.GetPosition(canvas));
            Canvas.SetLeft(selection, Math.Min(origin.X, p.X)); Canvas.SetTop(selection, Math.Min(origin.Y, p.Y));
            selection.Width = Math.Abs(p.X - origin.X); selection.Height = Math.Abs(p.Y - origin.Y);
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!dragging || e.ChangedButton != MouseButton.Left) return;
            Point end = Clamp(e.GetPosition(canvas)); dragging = false; ReleaseMouseCapture();
            int x1 = Math.Max(0, Math.Min(desktop.PixelWidth, (int)Math.Round(Math.Min(origin.X, end.X) * pixelsPerDipX)));
            int y1 = Math.Max(0, Math.Min(desktop.PixelHeight, (int)Math.Round(Math.Min(origin.Y, end.Y) * pixelsPerDipY)));
            int x2 = Math.Max(0, Math.Min(desktop.PixelWidth, (int)Math.Round(Math.Max(origin.X, end.X) * pixelsPerDipX)));
            int y2 = Math.Max(0, Math.Min(desktop.PixelHeight, (int)Math.Round(Math.Max(origin.Y, end.Y) * pixelsPerDipY)));
            BitmapSource crop = null;
            if (x2 - x1 >= 3 && y2 - y1 >= 3)
            {
                crop = new CroppedBitmap(desktop, new Int32Rect(x1, y1, x2 - x1, y2 - y1)); crop.Freeze();
            }
            Complete(crop); e.Handled = true;
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            dragging = false; Complete(null); e.Handled = true;
        }

        Point Clamp(Point p) { return new Point(Math.Max(0, Math.Min(canvas.Width, p.X)), Math.Max(0, Math.Min(canvas.Height, p.Y))); }
        void Complete(BitmapSource crop)
        {
            if (finished) return;
            finished = true; Close(); completed(crop);
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr handle);
    }
}
