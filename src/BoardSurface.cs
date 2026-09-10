using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArkBoard
{
    public sealed class BoardSurface : FrameworkElement
    {
        public readonly BoardDocument Document;
        public bool ShowGrid = true;
        public bool SpaceDown;
        public event Action ViewChanged;
        internal bool TextToolArmed;
        internal bool TextInputActive;
        internal event Action<Point, double> TextPlacementRequested;
        double placementFontSize = 24;
        readonly Brush background = new SolidColorBrush(Color.FromRgb(25, 25, 25));
        readonly Brush accent = new SolidColorBrush(Color.FromRgb(169, 169, 169));
        readonly Brush muted = new SolidColorBrush(Color.FromRgb(144, 144, 144));
        readonly Brush imageBackground = new SolidColorBrush(Color.FromRgb(37, 37, 37));
        readonly DrawingBrush gridTile;
        readonly DispatcherTimer settleTimer;
        string gesture;
        Point startScreen, startWorld, lastScreen;
        Dictionary<string, ImageItem> originals;
        ImageItem transformStart;
        Point anchor;
        Vector diagonal;
        double startAngle;
        bool checkpoint;
        HashSet<string> selectionBefore;
        Rect marquee = Rect.Empty;

        public BoardSurface(BoardDocument doc)
        {
            Document = doc; Focusable = true; ClipToBounds = true; AllowDrop = true;
            background.Freeze(); accent.Freeze(); muted.Freeze(); imageBackground.Freeze();
            var dot = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(51, 51, 51)), null, new EllipseGeometry(new Point(0, 0), 1, 1));
            dot.Freeze();
            gridTile = new DrawingBrush(dot) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
            settleTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(180) };
            settleTimer.Tick += delegate
            {
                settleTimer.Stop(); RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality); InvalidateVisual();
            };
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            doc.Changed += delegate { BeginInteractiveRendering(); InvalidateVisual(); };
            SizeChanged += delegate { BeginInteractiveRendering(); InvalidateVisual(); };
            Unloaded += delegate { settleTimer.Stop(); };
            LostMouseCapture += delegate { FinishGesture(); };
        }
        internal void BeginInteractiveRendering()
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            settleTimer.Stop(); settleTimer.Start();
        }
        internal void ArmTextTool()
        {
            FinishGesture(); TextToolArmed = true; TextInputActive = true; Cursor = Cursors.Cross; Focus(); InvalidateVisual();
        }
        internal void CancelTextTool()
        {
            TextToolArmed = false; TextInputActive = false; FinishGesture(); InvalidateVisual();
        }
        public Point ToWorld(Point p) { return new Point((p.X - Document.PanX) / Document.Zoom, (p.Y - Document.PanY) / Document.Zoom); }
        public Point ToScreen(Point p) { return new Point(p.X * Document.Zoom + Document.PanX, p.Y * Document.Zoom + Document.PanY); }
        public Point CenterWorld { get { return ToWorld(new Point(ActualWidth / 2, ActualHeight / 2)); } }
        public void ZoomAt(Point screen, double zoom)
        {
            BeginInteractiveRendering();
            Point w = ToWorld(screen);
            Document.Zoom = Math.Max(.01, Math.Min(16, zoom));
            Document.PanX = screen.X - w.X * Document.Zoom; Document.PanY = screen.Y - w.Y * Document.Zoom;
            InvalidateVisual(); if (ViewChanged != null) ViewChanged();
        }
        public void Fit(bool selectedOnly)
        {
            BeginInteractiveRendering();
            Rect bounds = Document.Bounds(selectedOnly);
            if (bounds.IsEmpty) { ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1); return; }
            double w = Math.Max(200, ActualWidth) - 120, h = Math.Max(200, ActualHeight) - 120;
            Document.Zoom = Math.Max(.01, Math.Min(4, Math.Min(w / bounds.Width, h / bounds.Height)));
            Document.PanX = ActualWidth / 2 - (bounds.X + bounds.Width / 2) * Document.Zoom;
            Document.PanY = ActualHeight / 2 - (bounds.Y + bounds.Height / 2) * Document.Zoom;
            InvalidateVisual(); if (ViewChanged != null) ViewChanged();
        }
        public ImageItem Hit(Point screen)
        {
            Point world = ToWorld(screen);
            return Document.Items.LastOrDefault(i => i.Contains(world));
        }
        Point RotationHandle(ImageItem item)
        {
            Point[] corners = item.Corners().Select(ToScreen).ToArray();
            Point top = new Point((corners[0].X + corners[1].X) / 2, (corners[0].Y + corners[1].Y) / 2);
            Vector normal = top - ToScreen(new Point(item.X, item.Y));
            if (normal.Length < .001) normal = new Vector(0, -1);
            normal.Normalize(); return top + normal * 30;
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(background, null, new Rect(RenderSize));
            if (ShowGrid)
            {
                double spacing = 64 * Document.Zoom;
                while (spacing < 24) spacing *= 2;
                while (spacing > 96) spacing /= 2;
                double ox = ((Document.PanX % spacing) + spacing) % spacing;
                double oy = ((Document.PanY % spacing) + spacing) % spacing;
                // One repeated tile instead of recording thousands of individual dots.
                gridTile.Viewbox = new Rect(-2, -2, spacing, spacing);
                gridTile.Viewport = new Rect(ox - 2, oy - 2, spacing, spacing);
                dc.DrawRectangle(gridTile, null, new Rect(RenderSize));
            }
            dc.PushTransform(new MatrixTransform(Document.Zoom, 0, 0, Document.Zoom, Document.PanX, Document.PanY));
            Rect visible = new Rect(ToWorld(new Point(-20, -20)), ToWorld(new Point(ActualWidth + 20, ActualHeight + 20)));
            foreach (ImageItem item in Document.Items)
            {
                if (!visible.IntersectsWith(item.Bounds())) continue;
                AssetData asset = null;
                if (!item.IsText && !Document.Assets.TryGetValue(item.Asset, out asset)) continue;
                dc.PushTransform(new MatrixTransform(item.Matrix));
                Rect rect = new Rect(-item.Width / 2, -item.Height / 2, item.Width, item.Height);
                if (item.IsText) TextLayout.Draw(dc, item);
                else { dc.DrawRectangle(imageBackground, null, rect); dc.DrawImage(asset.Bitmap, rect); }
                dc.Pop();
            }
            dc.Pop();
            foreach (ImageItem item in Document.Selection)
            {
                Point[] corners = item.Corners().Select(ToScreen).ToArray();
                Pen pen = new Pen(accent, 1.5);
                for (int i = 0; i < 4; i++) dc.DrawLine(pen, corners[i], corners[(i + 1) % 4]);
                if (Document.Selected.Count == 1)
                {
                    foreach (Point p in corners) dc.DrawRectangle(background, pen, new Rect(p.X - 4, p.Y - 4, 8, 8));
                    Point top = new Point((corners[0].X + corners[1].X) / 2, (corners[0].Y + corners[1].Y) / 2);
                    Point handle = RotationHandle(item);
                    dc.DrawLine(pen, top, handle); dc.DrawEllipse(background, pen, handle, 5, 5);
                }
            }
            if (!marquee.IsEmpty)
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(32, 169, 169, 169)), new Pen(accent, 1), marquee);
            if (gesture == "textsize")
            {
                var preview = TextLayout.Format("Aa", placementFontSize * Document.Zoom);
                dc.DrawText(preview, startScreen);
                dc.DrawLine(new Pen(accent, 1), startScreen, startScreen + new Vector(0, preview.Height));
            }
            if (Document.Items.Count == 0 && !TextInputActive)
            {
                double cx = ActualWidth / 2, cy = ActualHeight / 2;
                Pen p = new Pen(new SolidColorBrush(Color.FromRgb(88, 88, 88)), 2);
                dc.DrawRoundedRectangle(null, p, new Rect(cx - 27, cy - 100, 54, 43), 6, 6);
                dc.DrawLine(p, new Point(cx - 20, cy - 66), new Point(cx - 7, cy - 81));
                dc.DrawLine(p, new Point(cx - 7, cy - 81), new Point(cx + 2, cy - 72));
                dc.DrawLine(p, new Point(cx + 2, cy - 72), new Point(cx + 11, cy - 83));
                dc.DrawLine(p, new Point(cx + 11, cy - 83), new Point(cx + 21, cy - 66));
                dc.DrawEllipse(null, p, new Point(cx + 13, cy - 91), 3, 3);
                DrawCentered(dc, "A space for your ideas", 24, Brushes.WhiteSmoke, cy - 28);
                DrawCentered(dc, "Drop images here from your computer or browser", 14, muted, cy + 14);
                DrawCentered(dc, "or press Ctrl+I to import and Ctrl+V to paste", 12, muted, cy + 42);
            }
        }
        void DrawCentered(DrawingContext dc, string text, double size, Brush brush, double y)
        {
            FormattedText ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2, y));
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (gesture != null) return;
            ZoomAt(e.GetPosition(this), Document.Zoom * Math.Pow(1.15, e.Delta / 120.0)); e.Handled = true;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Focus();
            if (gesture != null) return;
            startScreen = lastScreen = e.GetPosition(this); startWorld = ToWorld(startScreen); checkpoint = false;
            if (TextToolArmed && e.ChangedButton == MouseButton.Left)
            {
                gesture = "textsize"; placementFontSize = Math.Max(1, Math.Min(8192, 24 / Document.Zoom));
                CaptureMouse(); e.Handled = true; InvalidateVisual(); return;
            }
            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && SpaceDown))
            { gesture = "pan"; Cursor = Cursors.ScrollAll; CaptureMouse(); e.Handled = true; return; }
            if (e.ChangedButton == MouseButton.Right)
            {
                ImageItem hitRight = Hit(startScreen);
                if (hitRight != null && !Document.Selected.Contains(hitRight.Id))
                { Document.Selected.Clear(); Document.Selected.Add(hitRight.Id); Document.Notify(); }
                return;
            }
            if (e.ChangedButton != MouseButton.Left) return;
            ImageItem single = Document.Selected.Count == 1 ? Document.Selection.FirstOrDefault() : null;
            if (single != null)
            {
                if ((RotationHandle(single) - startScreen).Length < 11)
                {
                    gesture = "rotate"; transformStart = single.Copy();
                    startAngle = Math.Atan2(startWorld.Y - single.Y, startWorld.X - single.X) * 180 / Math.PI;
                }
                else
                {
                    Point[] corners = single.Corners();
                    for (int i = 0; i < 4; i++) if ((ToScreen(corners[i]) - startScreen).Length < 11)
                    {
                        gesture = "resize"; transformStart = single.Copy();
                        anchor = corners[(i + 2) % 4]; diagonal = corners[i] - anchor; break;
                    }
                }
            }
            if (gesture == null)
            {
                ImageItem hit = Hit(startScreen);
                bool add = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (hit != null)
                {
                    if (add && Document.Selected.Contains(hit.Id))
                    { Document.Selected.Remove(hit.Id); Document.Notify(); e.Handled = true; return; }
                    if (!Document.Selected.Contains(hit.Id))
                    { if (!add) Document.Selected.Clear(); Document.Selected.Add(hit.Id); }
                    originals = Document.Selection.ToDictionary(i => i.Id, i => i.Copy()); gesture = "move";
                }
                else
                {
                    if (!add) Document.Selected.Clear();
                    selectionBefore = new HashSet<string>(Document.Selected); gesture = "marquee";
                }
                Document.Notify();
            }
            CaptureMouse(); e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point screen = e.GetPosition(this);
            if (gesture == null)
            {
                if (TextToolArmed) { Cursor = Cursors.Cross; return; }
                Cursor = SpaceDown ? Cursors.ScrollAll : Hit(screen) != null ? Cursors.SizeAll : Cursors.Arrow;
                if (Document.Selected.Count == 1)
                {
                    ImageItem i = Document.Selection.FirstOrDefault();
                    if (i != null && i.Corners().Any(p => (ToScreen(p) - screen).Length < 11)) Cursor = Cursors.SizeNWSE;
                    else if (i != null && (RotationHandle(i) - screen).Length < 11) Cursor = Cursors.Hand;
                }
                return;
            }
            if (gesture == "textsize")
            {
                Vector span = screen - startScreen;
                double height = Math.Max(Math.Abs(span.Y), Math.Abs(span.X) / 3);
                placementFontSize = Math.Max(1, Math.Min(8192, Math.Min(512, Math.Max(8, height < 4 ? 24 : height)) / Document.Zoom));
            }
            else if (gesture == "pan")
            {
                BeginInteractiveRendering();
                Vector delta = screen - lastScreen;
                Document.PanX += delta.X; Document.PanY += delta.Y;
                if (ViewChanged != null) ViewChanged();
            }
            else if (gesture == "marquee")
            {
                marquee = new Rect(startScreen, screen);
                Rect worldRect = new Rect(ToWorld(marquee.TopLeft), ToWorld(marquee.BottomRight));
                Document.Selected.Clear(); Document.Selected.UnionWith(selectionBefore);
                foreach (ImageItem i in Document.Items) if (worldRect.IntersectsWith(i.Bounds())) Document.Selected.Add(i.Id);
                Document.Notify();
            }
            else if ((screen - startScreen).Length > 2 || checkpoint)
            {
                if (!checkpoint) { Document.Checkpoint(); checkpoint = true; }
                Point world = ToWorld(screen);
                if (gesture == "move")
                {
                    Vector delta = world - startWorld;
                    foreach (ImageItem i in Document.Selection)
                    { ImageItem old = originals[i.Id]; i.X = old.X + delta.X; i.Y = old.Y + delta.Y; }
                }
                else
                {
                    ImageItem item = Document.Items.FirstOrDefault(i => i.Id == transformStart.Id);
                    if (item == null) { FinishGesture(); return; }
                    if (gesture == "resize")
                    {
                        double factor = Vector.Multiply(world - anchor, diagonal) / diagonal.LengthSquared;
                        factor = Math.Max(1.0 / Math.Min(transformStart.Width, transformStart.Height),
                            Math.Min(1000000.0 / Math.Max(transformStart.Width, transformStart.Height), factor));
                        Point center = anchor + diagonal * factor / 2;
                        item.X = center.X; item.Y = center.Y;
                        item.Width = transformStart.Width * factor; item.Height = transformStart.Height * factor;
                    }
                    else if (gesture == "rotate")
                    {
                        double a = Math.Atan2(world.Y - item.Y, world.X - item.X) * 180 / Math.PI;
                        double result = transformStart.Rotation + a - startAngle;
                        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) result = Math.Round(result / 15) * 15;
                        item.Rotation = NormalizeAngle(result);
                    }
                }
                Document.Notify();
            }
            lastScreen = screen; InvalidateVisual(); e.Handled = true;
        }
        public static double NormalizeAngle(double angle) { return ((angle % 360) + 360) % 360; }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (gesture == "textsize" && e.ChangedButton == MouseButton.Left)
            {
                Point position = startWorld; double size = placementFontSize;
                TextToolArmed = false; FinishGesture();
                if (TextPlacementRequested != null) TextPlacementRequested(position, size);
                e.Handled = true; return;
            }
            if (gesture != null) { FinishGesture(); e.Handled = true; }
        }
        public void FinishGesture()
        {
            gesture = null; marquee = Rect.Empty; originals = null; transformStart = null;
            if (IsMouseCaptured) ReleaseMouseCapture(); Cursor = Cursors.Arrow; InvalidateVisual();
        }
    }
}
