using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ArkBoard
{
    public sealed partial class MainWindow
    {
        internal Button screenGrabButton;
        AssetData pendingScreenGrab;
        bool screenGrabRunning;

        void BuildScreenGrabButton(Grid area)
        {
            // WPF geometries correspond to the four strokes in assets/camera.svg.
            var drawing = new Canvas { Width = 33, Height = 24 };
            Geometry body = Geometry.Parse("M56,183 L65.85,183 C66.485,183 67,183.515 67,184.15 L67,204.85 C67,205.485 66.485,206 65.85,206 L31.15,206 C30.515,206 30,205.485 30,204.85 L30,184.15 C30,183.515 30.515,183 31.15,183 L41,183 L41,180.25 C41,179.836 41.336,179.5 41.75,179.5 L55.25,179.5 C55.664,179.5 56,179.836 56,180.25 Z").Clone();
            body.Transform = new MatrixTransform(.84492, 0, 0, .84492, -24.478597, -151.10824);
            Geometry lens = new EllipseGeometry(new Point(48.5, 194.5), 7.112, 7.112);
            lens.Transform = new MatrixTransform(.84492, 0, 0, .84492, -24.478597, -149.32783);
            drawing.Children.Add(new Path { Data = body, Stroke = Brush("#D9D9D9"), StrokeThickness = 1.18, StrokeLineJoin = PenLineJoin.Round });
            drawing.Children.Add(new Path { Data = lens, Stroke = Brush("#D9D9D9"), StrokeThickness = 1.26 });
            drawing.Children.Add(new Path { Data = Geometry.Parse("M0.869,6.892 L32.131,6.892"), Stroke = Brush("#D9D9D9"), StrokeThickness = .67 });
            var icon = new Viewbox { Width = 24.3, Height = 18, Child = drawing };
            screenGrabButton = new Button { ToolTip = "Capture screen region · Click canvas to place · Esc to cancel" };
            // The shared Button helper refocuses Board after Click, which deactivates the capture overlay.
            screenGrabButton.Click += delegate { if (!busy) StartScreenGrab(); };
            screenGrabButton.Content = icon; screenGrabButton.Padding = new Thickness(0);
            screenGrabButton.Width = 38; screenGrabButton.Height = 28;
            screenGrabButton.HorizontalAlignment = HorizontalAlignment.Left;
            screenGrabButton.VerticalAlignment = VerticalAlignment.Top;
            screenGrabButton.Margin = new Thickness(12, 46, 0, 0);
            screenGrabButton.Background = Brush("#323232");
            System.Windows.Automation.AutomationProperties.SetName(screenGrabButton, "Screen capture tool");
            Panel.SetZIndex(screenGrabButton, 20); area.Children.Add(screenGrabButton);
            Board.PreviewMouseDown += PlaceScreenGrab;
        }

        void StartScreenGrab()
        {
            if (screenGrabRunning || busy || locked) return;
            CancelScreenGrabPlacement(); CommitText();
            screenGrabRunning = true;
            var source = PresentationSource.FromVisual(this) as HwndSource;
            Matrix fromDevice = source == null || source.CompositionTarget == null
                ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            try
            {
                SetStatus("Drag a screen region · Esc to cancel");
                var overlay = new ScreenGrab(fromDevice.M11, fromDevice.M22, OnScreenGrabComplete);
                overlay.Show(); overlay.Activate(); overlay.Focus();
            }
            catch (Exception ex)
            {
                screenGrabRunning = false; Activate(); Error("Unable to capture screen", ex);
            }
        }

        void OnScreenGrabComplete(BitmapSource crop)
        {
            screenGrabRunning = false;
            Activate(); Board.Focus();
            if (crop == null) { SetStatus("Screen capture cancelled"); return; }
            try
            {
                pendingScreenGrab = AssetData.FromBitmap(crop);
                Board.ScreenGrabPlacementArmed = true; Board.Cursor = Cursors.Cross;
                screenGrabButton.Background = Brush("#505050");
                SetStatus("Screen captured · Click the canvas to place it · Esc to cancel");
            }
            catch (Exception ex) { Error("Unable to capture screen", ex); }
        }

        void PlaceScreenGrab(object sender, MouseButtonEventArgs e)
        {
            if (pendingScreenGrab == null || e.ChangedButton != MouseButton.Left) return;
            Point at = Board.ToWorld(e.GetPosition(Board));
            AssetData asset = pendingScreenGrab;
            CancelScreenGrabPlacement();
            Document.Change(() =>
            {
                ImageItem item = Document.Add(asset, "Screen capture.png", at);
                Document.Selected.Clear(); Document.Selected.Add(item.Id);
            });
            SetStatus("Screen capture placed"); Board.Focus(); e.Handled = true;
        }

        void CancelScreenGrabPlacement()
        {
            pendingScreenGrab = null; Board.ScreenGrabPlacementArmed = false;
            Board.Cursor = Cursors.Arrow;
            if (screenGrabButton != null) screenGrabButton.Background = Brush("#323232");
        }
    }
}
