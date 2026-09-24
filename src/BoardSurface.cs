using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        public bool AutoSorting = true;
        public bool Snapping;
        public double ImagePadding = 4;
        public bool InvertDragZoom;
        public bool SpaceDown;
        internal bool ShiftPreview;
        internal string HoveredImageId;
        public event Action ViewChanged;
        internal bool TextToolArmed;
        internal bool ScreenGrabPlacementArmed;
        internal bool TextInputActive;
        internal string EditingTextId;
        internal string MaskEditingId;
        internal event Action<Point, double> TextPlacementRequested;
        internal event Action<ImageItem> TextEditRequested;
        double placementFontSize = 24;
        readonly Brush background = new SolidColorBrush(Color.FromRgb(25, 25, 25));
        readonly Brush accent = new SolidColorBrush(Color.FromRgb(169, 169, 169));
        readonly Brush muted = new SolidColorBrush(Color.FromRgb(144, 144, 144));
        readonly Brush imageBackground = new SolidColorBrush(Color.FromRgb(37, 37, 37));
        readonly DrawingBrush gridTile;
        readonly DispatcherTimer settleTimer;
        static readonly Cursor rotateCursor = LoadRotateCursor();
        static readonly Geometry rotationAnchorGeometry = CreateRotationAnchorGeometry();
        static readonly Geometry distantRotationAnchorGeometry = CreateDistantRotationAnchorGeometry();
        static readonly Brush rotationAnchorBrush = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        string gesture;
        Point startScreen, startWorld, lastScreen;
        Dictionary<string, ImageItem> originals;
        ImageItem transformStart;
        Point anchor;
        Vector diagonal;
        Rect groupStartBounds = Rect.Empty;
        Point groupCenter;
        double groupRotationDelta;
        double startAngle, startZoom;
        int maskEdge = -1;
        string draggedItemId;
        bool checkpoint;
        HashSet<string> selectionBefore;
        Rect marquee = Rect.Empty;
        readonly List<Tuple<Point, Point>> snapGuides = new List<Tuple<Point, Point>>();
        internal int SnapGuideCount { get { return snapGuides.Count; } }

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
            doc.Changed += delegate
            {
                if (MaskEditingId != null && !doc.Selected.Contains(MaskEditingId)) MaskEditingId = null;
                BeginInteractiveRendering(); InvalidateVisual();
            };
            SizeChanged += delegate { BeginInteractiveRendering(); InvalidateVisual(); };
            Unloaded += delegate { settleTimer.Stop(); };
            LostMouseCapture += delegate { FinishGesture(); };
        }
        internal void BeginInteractiveRendering()
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            settleTimer.Stop(); settleTimer.Start();
        }
        static Cursor LoadRotateCursor()
        {
            Stream stream = typeof(BoardSurface).Assembly.GetManifestResourceStream("ArkBoard.RotateCursor");
            if (stream == null) return Cursors.Hand;
            using (stream) return new Cursor(stream);
        }
        static Geometry CreateRotationAnchorGeometry()
        {
            // The three filled paths and matrices from assets/rotate.svg (viewBox 624 x 624).
            var group = new GeometryGroup();
            Geometry arc = Geometry.Parse("M512,976.489 L512,957.91 C758.269,957.91 957.91,758.269 957.91,512 L976.489,512 C976.489,768.53 768.53,976.489 512,976.489 Z").Clone();
            arc.Transform = new MatrixTransform(0, -.996509, .996509, 0, -374.834144, 998.834144);
            Geometry firstArrow = Geometry.Parse("M426.5,306 L495,443 L358,443 Z").Clone();
            firstArrow.Transform = new MatrixTransform(0, -.547445, 1, 0, -306, 270.985401);
            Geometry secondArrow = Geometry.Parse("M426.5,306 L495,443 L358,443 Z").Clone();
            secondArrow.Transform = new MatrixTransform(-.547445, 0, 0, -1, 819.985401, 930);
            group.Children.Add(arc); group.Children.Add(firstArrow); group.Children.Add(secondArrow);
            group.Freeze(); return group;
        }
        static Geometry CreateDistantRotationAnchorGeometry()
        {
            // Top-side artwork from assets/rotate_distant.svg (viewBox 790 x 310).
            var group = new GeometryGroup();
            Geometry arc = Geometry.Parse("M835.602,307.27 L803.445,325.836 C736.935,210.638 614.02,139.673 481,139.673 C347.98,139.673 225.065,210.638 158.555,325.836 L126.398,307.27 C199.541,180.583 334.714,102.541 481,102.541 C627.286,102.541 762.459,180.583 835.602,307.27 Z").Clone();
            arc.Transform = new MatrixTransform(1, 0, 0, 1, -86, -102);
            Geometry leftArrow = Geometry.Parse("M140.617,256.96 L189,374 L92.234,374 Z").Clone();
            leftArrow.Transform = new MatrixTransform(-.866025, -.5, .5, -.866025, -5.912436, 602.182933);
            Geometry rightArrow = Geometry.Parse("M140.617,256.96 L189,374 L92.234,374 Z").Clone();
            rightArrow.Transform = new MatrixTransform(.866025, -.5, -.5, -.866025, 795.912443, 602.182933);
            group.Children.Add(arc); group.Children.Add(leftArrow); group.Children.Add(rightArrow);
            group.Freeze(); return group;
        }
        internal void ArmTextTool()
        {
            FinishGesture(); TextToolArmed = true; TextInputActive = true; Cursor = Cursors.Cross; Focus(); InvalidateVisual();
        }
        internal void CancelTextTool()
        {
            TextToolArmed = false; TextInputActive = false; FinishGesture(); InvalidateVisual();
        }
        internal void CancelMaskEditing() { MaskEditingId = null; InvalidateVisual(); }
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
            return Document.Items.LastOrDefault(i => i.Id != EditingTextId && i.Contains(world));
        }
        internal bool TryEditTextAt(Point screen)
        {
            ImageItem item = Hit(screen);
            if (item == null || !item.IsText) return false;
            FinishGesture();
            Document.Selected.Clear(); Document.Selected.Add(item.Id); Document.Notify();
            if (TextEditRequested != null) TextEditRequested(item);
            return true;
        }
        internal bool FitOnEmptyDoubleClick(Point screen, int clickCount)
        {
            if (clickCount != 2 || Hit(screen) != null) return false;
            Fit(false); return true;
        }
        internal bool FitOnImageDoubleClick(Point screen, int clickCount)
        {
            if (clickCount != 2) return false;
            ImageItem item = Hit(screen);
            if (item == null || item.IsText) return false;
            if (Document.Selected.Count != 1 || !Document.Selected.Contains(item.Id))
            { Document.Selected.Clear(); Document.Selected.Add(item.Id); Document.Notify(); }
            Fit(true); return true;
        }
        internal Point[] RotationHandles(ImageItem item)
        {
            return RotationHandles(item.Corners().Select(ToScreen).ToArray());
        }
        internal static bool UsesOuterRotationHandles(Point[] corners)
        {
            if (corners == null || corners.Length != 4) return false;
            double shortest = double.MaxValue;
            for (int index = 0; index < 4; index++)
                shortest = Math.Min(shortest, (corners[(index + 1) % 4] - corners[index]).Length);
            return shortest < 90;
        }
        internal static double RotationAnchorRadius(Point[] corners)
        { return UsesOuterRotationHandles(corners) ? 4 : 8; }
        static Point[] RotationHandles(Point[] corners)
        {
            var handles = new Point[4];
            if (UsesOuterRotationHandles(corners))
            {
                Point center = new Point(corners.Average(p => p.X), corners.Average(p => p.Y));
                for (int index = 0; index < 4; index++)
                {
                    Point nextCorner = corners[(index + 1) % 4];
                    Point midpoint = new Point((corners[index].X + nextCorner.X) / 2, (corners[index].Y + nextCorner.Y) / 2);
                    Vector side = nextCorner - corners[index];
                    Vector outward = new Vector(side.Y, -side.X);
                    if (outward.Length > .001) outward.Normalize();
                    if (Vector.Multiply(outward, midpoint - center) < 0) outward *= -1;
                    handles[index] = midpoint + outward * 14;
                }
                return handles;
            }
            for (int index = 0; index < 4; index++)
            {
                Vector next = corners[(index + 1) % 4] - corners[index];
                Vector previous = corners[(index + 3) % 4] - corners[index];
                if (next.Length > .001) next.Normalize(); if (previous.Length > .001) previous.Normalize();
                handles[index] = corners[index] + next * 27 + previous * 27;
            }
            return handles;
        }
        internal bool HasGroupTransformSelection
        { get { return Document.Selection.Count() > 1 && Document.Selection.All(i => !i.IsText); } }
        internal Rect GroupBounds()
        { return HasGroupTransformSelection ? Document.Bounds(true) : Rect.Empty; }
        static Point[] RectCorners(Rect bounds)
        { return new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft }; }
        internal Point[] GroupCorners()
        {
            Rect bounds = GroupBounds();
            return bounds.IsEmpty ? new Point[0] : RectCorners(bounds).Select(ToScreen).ToArray();
        }
        Point[] GroupControlCorners()
        {
            if (gesture != "grouprotate" || groupStartBounds.IsEmpty) return GroupCorners();
            Matrix rotation = Matrix.Identity; rotation.RotateAt(groupRotationDelta, groupCenter.X, groupCenter.Y);
            return RectCorners(groupStartBounds).Select(p => ToScreen(rotation.Transform(p))).ToArray();
        }
        internal Point[] GroupRotationHandles()
        {
            Point[] corners = GroupControlCorners();
            return corners.Length == 4 ? RotationHandles(corners) : new Point[0];
        }
        internal int GroupCornerAt(Point screen)
        {
            Point[] corners = GroupCorners();
            for (int index = 0; index < corners.Length; index++) if ((corners[index] - screen).Length < 11) return index;
            return -1;
        }
        internal int GroupRotationHandleAt(Point screen)
        {
            Point[] handles = GroupRotationHandles();
            for (int index = 0; index < handles.Length; index++) if ((handles[index] - screen).Length <= 14) return index;
            return -1;
        }
        internal int RotationHandleAt(ImageItem item, Point screen)
        {
            if (item == null || item.IsText) return -1;
            Point[] handles = RotationHandles(item);
            for (int index = 0; index < handles.Length; index++) if ((handles[index] - screen).Length <= 14) return index;
            return -1;
        }
        internal static Matrix RotationAnchorTransform(Point[] corners, Point center, double radius, int index)
        {
            Vector horizontal = corners[1] - corners[0];
            Vector vertical = corners[3] - corners[0];
            if (horizontal.Length < .001) horizontal = new Vector(1, 0); else horizontal.Normalize();
            if (vertical.Length < .001) vertical = new Vector(0, 1); else vertical.Normalize();
            // rotate.svg is authored for the top-right corner.
            double xSign = index == 0 || index == 3 ? -1 : 1;
            double ySign = index == 2 || index == 3 ? -1 : 1;
            double scale = radius / 312;
            double m11 = scale * xSign * horizontal.X, m12 = scale * xSign * horizontal.Y;
            double m21 = scale * ySign * vertical.X, m22 = scale * ySign * vertical.Y;
            return new Matrix(m11, m12, m21, m22,
                center.X - 312 * (m11 + m21), center.Y - 312 * (m12 + m22));
        }
        internal static Matrix DistantRotationAnchorTransform(Point[] corners, Point center, int index)
        {
            Vector horizontal = corners[1] - corners[0];
            Vector vertical = corners[3] - corners[0];
            if (horizontal.Length < .001) horizontal = new Vector(1, 0); else horizontal.Normalize();
            if (vertical.Length < .001) vertical = new Vector(0, 1); else vertical.Normalize();
            Vector tangent = index == 0 || index == 2 ? horizontal : vertical;
            Vector inward = index == 0 ? vertical : index == 1 ? -horizontal : index == 2 ? -vertical : horizontal;
            const double width = 20;
            double height = width * 310 / 790;
            double m11 = tangent.X * width / 790, m12 = tangent.Y * width / 790;
            double m21 = inward.X * height / 310, m22 = inward.Y * height / 310;
            return new Matrix(m11, m12, m21, m22,
                center.X - 395 * m11 - 155 * m21, center.Y - 395 * m12 - 155 * m22);
        }
        void DrawRotationAnchor(DrawingContext dc, Point center, Point[] corners, int index, double radius)
        {
            bool distant = UsesOuterRotationHandles(corners);
            Geometry geometry = (distant ? distantRotationAnchorGeometry : rotationAnchorGeometry).Clone();
            geometry.Transform = new MatrixTransform(distant
                ? DistantRotationAnchorTransform(corners, center, index)
                : RotationAnchorTransform(corners, center, radius, index));
            dc.DrawGeometry(null, new Pen(background, 1), geometry);
            dc.DrawGeometry(rotationAnchorBrush, null, geometry);
        }
        internal bool ShowsMoveCursor(ImageItem item)
        { return item != null && Document.Selected.Contains(item.Id); }
        internal void SetShiftPreview(bool active)
        {
            ShiftPreview = active;
            ImageItem hovered = active && IsMouseOver ? Hit(Mouse.GetPosition(this)) : null;
            HoveredImageId = hovered != null && !hovered.IsText ? hovered.Id : null;
            InvalidateVisual();
        }
        internal int MaskEdgeAt(ImageItem item, Point screen)
        {
            if (item == null || item.IsText) return -1;
            Matrix inverse = item.Matrix; if (!inverse.HasInverse) return -1; inverse.Invert();
            Point local = inverse.Transform(ToWorld(screen));
            double threshold = 11 / Math.Max(.01, Document.Zoom);
            bool useVisibleBounds = item.HasMask && (MaskEditingId == item.Id || (ShiftPreview && HoveredImageId == item.Id));
            Rect bounds = useVisibleBounds ? item.VisibleRect :
                new Rect(-item.Width / 2, -item.Height / 2, item.Width, item.Height);
            double marginX = Math.Min(bounds.Width * .2, 14 / Math.Max(.01, Document.Zoom));
            double marginY = Math.Min(bounds.Height * .2, 14 / Math.Max(.01, Document.Zoom));
            int edge = -1; double distance = double.MaxValue;
            if (local.Y >= bounds.Top + marginY && local.Y <= bounds.Bottom - marginY)
            {
                double left = Math.Abs(local.X - bounds.Left), right = Math.Abs(local.X - bounds.Right);
                if (left <= threshold && left < distance) { edge = 0; distance = left; }
                if (right <= threshold && right < distance) { edge = 2; distance = right; }
            }
            if (local.X >= bounds.Left + marginX && local.X <= bounds.Right - marginX)
            {
                double top = Math.Abs(local.Y - bounds.Top), bottom = Math.Abs(local.Y - bounds.Bottom);
                if (top <= threshold && top < distance) { edge = 1; distance = top; }
                if (bottom <= threshold && bottom < distance) { edge = 3; }
            }
            return edge;
        }
        internal static void ApplyMask(ImageItem item, ImageItem basis, int edge, Point world)
        {
            Matrix inverse = basis.Matrix; if (!inverse.HasInverse) return; inverse.Invert();
            Point local = inverse.Transform(world); const double minimumVisible = .001;
            if (edge == 0) item.MaskLeft = Limit((local.X + basis.Width / 2) / basis.Width, 0, 1 - basis.MaskRight - minimumVisible);
            else if (edge == 2) item.MaskRight = Limit((basis.Width / 2 - local.X) / basis.Width, 0, 1 - basis.MaskLeft - minimumVisible);
            else if (edge == 1) item.MaskTop = Limit((local.Y + basis.Height / 2) / basis.Height, 0, 1 - basis.MaskBottom - minimumVisible);
            else if (edge == 3) item.MaskBottom = Limit((basis.Height / 2 - local.Y) / basis.Height, 0, 1 - basis.MaskTop - minimumVisible);
        }
        internal static void ApplyMaskOffset(ImageItem item, ImageItem basis, Point start, Point current)
        {
            Matrix inverse = basis.Matrix; if (!inverse.HasInverse) return; inverse.Invert();
            Vector delta = inverse.Transform(current) - inverse.Transform(start);
            double horizontal = basis.MaskLeft + basis.MaskRight;
            double vertical = basis.MaskTop + basis.MaskBottom;
            item.MaskLeft = Limit(basis.MaskLeft + delta.X / basis.Width, 0, horizontal);
            item.MaskRight = horizontal - item.MaskLeft;
            item.MaskTop = Limit(basis.MaskTop + delta.Y / basis.Height, 0, vertical);
            item.MaskBottom = vertical - item.MaskTop;
        }
        Point[] VisibleCorners(ImageItem item)
        {
            Rect r = item.VisibleRect; Matrix m = item.Matrix;
            return new[] { new Point(r.Left, r.Top), new Point(r.Right, r.Top), new Point(r.Right, r.Bottom), new Point(r.Left, r.Bottom) }
                .Select(p => ToScreen(m.Transform(p))).ToArray();
        }
        internal static Rect VisibleWorldBounds(ImageItem item)
        {
            Rect r = item.VisibleRect; Matrix m = item.Matrix;
            Point[] points = new[] { r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft }
                .Select(m.Transform).ToArray();
            return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)),
                new Point(points.Max(p => p.X), points.Max(p => p.Y)));
        }
        static Rect VisibleBounds(IEnumerable<ImageItem> items)
        {
            Rect result = Rect.Empty;
            foreach (ImageItem item in items.Where(i => !i.IsText))
            {
                Rect bounds = VisibleWorldBounds(item);
                if (result.IsEmpty) result = bounds; else result.Union(bounds);
            }
            return result;
        }
        internal static Vector CalculateMoveSnap(IEnumerable<ImageItem> movingBases, IEnumerable<ImageItem> targets,
            Vector rawDelta, double threshold, double padding, out List<Tuple<Point, Point>> guides)
        {
            guides = new List<Tuple<Point, Point>>();
            Rect source = VisibleBounds(movingBases);
            if (source.IsEmpty) return rawDelta;
            source.Offset(rawDelta);
            double bestX = threshold + 1, bestY = threshold + 1, correctionX = 0, correctionY = 0;
            Tuple<Point, Point> guideX = null, guideY = null;
            foreach (ImageItem target in targets.Where(i => !i.IsText))
            {
                Rect bounds = VisibleWorldBounds(target);
                foreach (double[] edges in new[] { new[] { source.Left, bounds.Left }, new[] { source.Right, bounds.Right },
                    new[] { source.Right, bounds.Left - padding }, new[] { source.Left, bounds.Right + padding } })
                    {
                        double sourceEdge = edges[0], destination = edges[1];
                        double distance = destination - sourceEdge;
                        if (Math.Abs(distance) <= threshold && Math.Abs(distance) < bestX)
                        {
                            bestX = Math.Abs(distance); correctionX = distance;
                            guideX = Tuple.Create(new Point(destination, source.Top), new Point(destination, source.Bottom));
                        }
                    }
                foreach (double[] edges in new[] { new[] { source.Top, bounds.Top }, new[] { source.Bottom, bounds.Bottom },
                    new[] { source.Bottom, bounds.Top - padding }, new[] { source.Top, bounds.Bottom + padding } })
                    {
                        double sourceEdge = edges[0], destination = edges[1];
                        double distance = destination - sourceEdge;
                        if (Math.Abs(distance) <= threshold && Math.Abs(distance) < bestY)
                        {
                            bestY = Math.Abs(distance); correctionY = distance;
                            guideY = Tuple.Create(new Point(source.Left, destination), new Point(source.Right, destination));
                        }
                    }
            }
            if (guideX != null) guides.Add(Tuple.Create(guideX.Item1 + new Vector(0, correctionY), guideX.Item2 + new Vector(0, correctionY)));
            if (guideY != null) guides.Add(Tuple.Create(guideY.Item1 + new Vector(correctionX, 0), guideY.Item2 + new Vector(correctionX, 0)));
            return rawDelta + new Vector(correctionX, correctionY);
        }
        internal static double CalculateScaleSnap(Rect source, IEnumerable<ImageItem> targets, Point fixedAnchor,
            double rawFactor, double minimum, double maximum, double threshold, double padding,
            out List<Tuple<Point, Point>> guides)
        {
            guides = new List<Tuple<Point, Point>>();
            double best = threshold + 1, result = rawFactor;
            Tuple<Point, Point> bestGuide = null;
            foreach (ImageItem target in targets.Where(i => !i.IsText))
            {
                Rect bounds = VisibleWorldBounds(target);
                foreach (double[] edges in new[] { new[] { source.Left, bounds.Left }, new[] { source.Right, bounds.Right },
                    new[] { source.Right, bounds.Left - padding }, new[] { source.Left, bounds.Right + padding } })
                {
                    double sourceEdge = edges[0], destination = edges[1];
                    double basis = sourceEdge - fixedAnchor.X;
                    if (Math.Abs(basis) < .000001) continue;
                    double current = fixedAnchor.X + basis * rawFactor;
                    double distance = Math.Abs(destination - current);
                    double candidate = (destination - fixedAnchor.X) / basis;
                    if (distance <= threshold && distance < best && candidate >= minimum && candidate <= maximum)
                    {
                        best = distance; result = candidate;
                        double scaledTop = fixedAnchor.Y + (source.Top - fixedAnchor.Y) * candidate;
                        double scaledBottom = fixedAnchor.Y + (source.Bottom - fixedAnchor.Y) * candidate;
                        bestGuide = Tuple.Create(new Point(destination, Math.Min(scaledTop, scaledBottom)),
                            new Point(destination, Math.Max(scaledTop, scaledBottom)));
                    }
                }
                foreach (double[] edges in new[] { new[] { source.Top, bounds.Top }, new[] { source.Bottom, bounds.Bottom },
                    new[] { source.Bottom, bounds.Top - padding }, new[] { source.Top, bounds.Bottom + padding } })
                {
                    double sourceEdge = edges[0], destination = edges[1];
                    double basis = sourceEdge - fixedAnchor.Y;
                    if (Math.Abs(basis) < .000001) continue;
                    double current = fixedAnchor.Y + basis * rawFactor;
                    double distance = Math.Abs(destination - current);
                    double candidate = (destination - fixedAnchor.Y) / basis;
                    if (distance <= threshold && distance < best && candidate >= minimum && candidate <= maximum)
                    {
                        best = distance; result = candidate;
                        double scaledLeft = fixedAnchor.X + (source.Left - fixedAnchor.X) * candidate;
                        double scaledRight = fixedAnchor.X + (source.Right - fixedAnchor.X) * candidate;
                        bestGuide = Tuple.Create(new Point(Math.Min(scaledLeft, scaledRight), destination),
                            new Point(Math.Max(scaledLeft, scaledRight), destination));
                    }
                }
            }
            if (bestGuide != null) guides.Add(bestGuide);
            return result;
        }
        static double Limit(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
        internal bool SnapEnabledForModifiers(ModifierKeys modifiers)
        { return Snapping != ((modifiers & ModifierKeys.Control) != 0); }
        internal bool AutoSortSelection(ImageItem dragged)
        {
            if (!AutoSorting || dragged == null || dragged.IsText || !Document.Selected.Contains(dragged.Id)) return false;
            List<ImageItem> selected = Document.Items.Where(i => Document.Selected.Contains(i.Id)).ToList();
            if (selected.Count == 0 || Document.Items.Skip(Document.Items.Count - selected.Count).SequenceEqual(selected)) return false;
            Document.Items.RemoveAll(i => Document.Selected.Contains(i.Id)); Document.Items.AddRange(selected); return true;
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
                if (item.Id == EditingTextId) continue;
                if (!visible.IntersectsWith(item.Bounds())) continue;
                AssetData asset = null;
                if (!item.IsText && !Document.Assets.TryGetValue(item.Asset, out asset)) continue;
                dc.PushTransform(new MatrixTransform(item.Matrix));
                Rect rect = new Rect(-item.Width / 2, -item.Height / 2, item.Width, item.Height);
                if (item.IsText) TextLayout.Draw(dc, item);
                else
                {
                    dc.PushClip(new RectangleGeometry(item.VisibleRect));
                    dc.DrawRectangle(imageBackground, null, rect); dc.DrawImage(asset.BitmapFor(item), rect); dc.Pop();
                }
                dc.Pop();
            }
            dc.Pop();
            List<ImageItem> controlItems = Document.Selection.ToList();
            if (ShiftPreview && HoveredImageId != null && !Document.Selected.Contains(HoveredImageId))
            {
                ImageItem hovered = Document.Items.FirstOrDefault(i => i.Id == HoveredImageId && !i.IsText);
                if (hovered != null) controlItems.Add(hovered);
            }
            foreach (ImageItem item in controlItems)
            {
                if (item.Id == EditingTextId) continue;
                Point[] corners = item.Corners().Select(ToScreen).ToArray();
                Pen pen = new Pen(accent, 1.5);
                bool selected = Document.Selected.Contains(item.Id);
                bool maskControls = !item.IsText && (item.Id == MaskEditingId || (ShiftPreview && item.Id == HoveredImageId));
                bool maskEditing = maskControls && item.HasMask;
                Pen outerPen = maskEditing ? new Pen(accent, 1.5) { DashStyle = DashStyles.Dash } : pen;
                for (int i = 0; i < 4; i++) dc.DrawLine(outerPen, corners[i], corners[(i + 1) % 4]);
                AssetData selectedAsset;
                bool layeredSelection = selected && !item.IsText && Document.Assets.TryGetValue(item.Asset, out selectedAsset) && selectedAsset.IsPsd;
                if (layeredSelection)
                {
                    double inset = 4 / Math.Max(.01, Document.Zoom);
                    if (item.Width > inset * 2 && item.Height > inset * 2)
                    {
                        Rect inner = new Rect(-item.Width / 2 + inset, -item.Height / 2 + inset, item.Width - inset * 2, item.Height - inset * 2);
                        Matrix matrix = item.Matrix;
                        Point[] innerCorners = new[] { inner.TopLeft, inner.TopRight, inner.BottomRight, inner.BottomLeft }
                            .Select(p => ToScreen(matrix.Transform(p))).ToArray();
                        for (int side = 0; side < 4; side++) dc.DrawLine(pen, innerCorners[side], innerCorners[(side + 1) % 4]);
                    }
                }
                if ((selected && Document.Selected.Count == 1) || maskControls)
                {
                    if (selected && Document.Selected.Count == 1)
                        foreach (Point p in corners) dc.DrawRectangle(background, outerPen, new Rect(p.X - 4, p.Y - 4, 8, 8));
                    if (!item.IsText)
                    {
                        Point[] maskCorners = maskEditing ? VisibleCorners(item) : corners;
                        if (maskEditing) for (int side = 0; side < 4; side++)
                            dc.DrawLine(pen, maskCorners[side], maskCorners[(side + 1) % 4]);
                        if (maskControls) for (int side = 0; side < 4; side++)
                        {
                            Point midpoint = new Point((maskCorners[side].X + maskCorners[(side + 1) % 4].X) / 2,
                                (maskCorners[side].Y + maskCorners[(side + 1) % 4].Y) / 2);
                            dc.DrawRectangle(background, pen, new Rect(midpoint.X - 3, midpoint.Y - 3, 6, 6));
                        }
                        if (selected && Document.Selected.Count == 1 && !maskControls)
                        {
                            Point[] handles = RotationHandles(item);
                            for (int index = 0; index < handles.Length; index++)
                                DrawRotationAnchor(dc, handles[index], corners, index, RotationAnchorRadius(corners));
                        }
                    }
                }
            }
            if (HasGroupTransformSelection)
            {
                Point[] corners = GroupControlCorners(); Pen pen = new Pen(accent, 1.5);
                for (int side = 0; side < 4; side++) dc.DrawLine(pen, corners[side], corners[(side + 1) % 4]);
                foreach (Point corner in corners) dc.DrawRectangle(background, pen, new Rect(corner.X - 4, corner.Y - 4, 8, 8));
                Point[] handles = GroupRotationHandles();
                for (int index = 0; index < handles.Length; index++)
                    DrawRotationAnchor(dc, handles[index], corners, index, RotationAnchorRadius(corners));
            }
            if (snapGuides.Count > 0)
            {
                Pen snapPen = new Pen(Brushes.White, 3);
                foreach (Tuple<Point, Point> guide in snapGuides)
                    dc.DrawLine(snapPen, ToScreen(guide.Item1), ToScreen(guide.Item2));
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
                DrawCentered(dc, Localization.T("A space for your ideas"), 24, accent, cy - 28);
                DrawCentered(dc, Localization.T("Drop images here from your computer or browser"), 14, muted, cy + 14);
                DrawCentered(dc, Localization.T("or press Ctrl+I to import and Ctrl+V to paste"), 12, muted, cy + 42);
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
            if (e.ChangedButton == MouseButton.Middle && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            { gesture = "dragzoom"; startZoom = Document.Zoom; Cursor = Cursors.SizeNS; CaptureMouse(); e.Handled = true; return; }
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
            if (e.ClickCount == 2 && TryEditTextAt(startScreen)) { e.Handled = true; return; }
            if (FitOnImageDoubleClick(startScreen, e.ClickCount)) { e.Handled = true; return; }
            if (FitOnEmptyDoubleClick(startScreen, e.ClickCount)) { e.Handled = true; return; }
            bool shiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (!shiftDown) MaskEditingId = null;
            if (HasGroupTransformSelection)
            {
                int rotationHandle = shiftDown ? -1 : GroupRotationHandleAt(startScreen);
                int corner = GroupCornerAt(startScreen);
                if (rotationHandle >= 0 || corner >= 0)
                {
                    originals = Document.Selection.ToDictionary(i => i.Id, i => i.Copy());
                    groupStartBounds = Document.Bounds(true);
                    groupCenter = new Point(groupStartBounds.X + groupStartBounds.Width / 2, groupStartBounds.Y + groupStartBounds.Height / 2);
                    if (rotationHandle >= 0)
                    {
                        gesture = "grouprotate"; groupRotationDelta = 0;
                        startAngle = Math.Atan2(startWorld.Y - groupCenter.Y, startWorld.X - groupCenter.X) * 180 / Math.PI;
                    }
                    else
                    {
                        Point[] corners = RectCorners(groupStartBounds); gesture = "groupresize";
                        anchor = corners[(corner + 2) % 4]; diagonal = corners[corner] - anchor;
                    }
                }
            }
            ImageItem single = Document.Selected.Count == 1 ? Document.Selection.FirstOrDefault() : null;
            if (gesture == null && single != null)
            {
                if (!shiftDown && !single.IsText && RotationHandleAt(single, startScreen) >= 0)
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
            if (gesture == null && shiftDown)
            {
                ImageItem maskedHit = Hit(startScreen);
                if (maskedHit != null && !maskedHit.IsText)
                {
                    maskEdge = MaskEdgeAt(maskedHit, startScreen);
                    if (maskEdge >= 0)
                    {
                        Document.Selected.Clear(); Document.Selected.Add(maskedHit.Id); MaskEditingId = maskedHit.Id;
                        transformStart = maskedHit.Copy(); gesture = "mask"; Document.Notify();
                    }
                    else if (maskedHit.HasMask && maskedHit.ContainsVisible(startWorld))
                    {
                        Document.Selected.Clear(); Document.Selected.Add(maskedHit.Id); MaskEditingId = maskedHit.Id;
                        transformStart = maskedHit.Copy(); gesture = "maskmove"; Document.Notify();
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
                    originals = Document.Selection.ToDictionary(i => i.Id, i => i.Copy());
                    draggedItemId = hit.Id; gesture = "move"; Cursor = Cursors.SizeAll;
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
                bool shiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                ImageItem hovered = Hit(screen);
                string hoveredId = hovered != null && !hovered.IsText ? hovered.Id : null;
                bool previewChanged = ShiftPreview != shiftDown || HoveredImageId != hoveredId;
                ShiftPreview = shiftDown; HoveredImageId = hoveredId;
                if (previewChanged) InvalidateVisual();
                if (TextToolArmed || ScreenGrabPlacementArmed) { Cursor = Cursors.Cross; return; }
                Cursor = SpaceDown ? Cursors.ScrollAll : ShowsMoveCursor(hovered) ? Cursors.SizeAll : Cursors.Arrow;
                if (HasGroupTransformSelection)
                {
                    if (GroupCornerAt(screen) >= 0) Cursor = Cursors.SizeNWSE;
                    else if (!shiftDown && GroupRotationHandleAt(screen) >= 0) Cursor = rotateCursor;
                }
                else if (Document.Selected.Count == 1)
                {
                    ImageItem i = Document.Selection.FirstOrDefault();
                    if (i != null && i.Corners().Any(p => (ToScreen(p) - screen).Length < 11)) Cursor = Cursors.SizeNWSE;
                    else if (i != null && !i.IsText && shiftDown && MaskEdgeAt(i, screen) >= 0)
                    { int edge = MaskEdgeAt(i, screen); Cursor = edge == 0 || edge == 2 ? Cursors.SizeWE : Cursors.SizeNS; }
                    else if (i != null && !i.IsText && !shiftDown && RotationHandleAt(i, screen) >= 0) Cursor = rotateCursor;
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
            else if (gesture == "dragzoom")
            {
                ZoomAt(startScreen, DragZoomTarget(startZoom, screen.Y - startScreen.Y, InvertDragZoom));
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
                if (!checkpoint)
                {
                    Document.Checkpoint(); checkpoint = true;
                    if (gesture == "move") AutoSortSelection(Document.Items.FirstOrDefault(i => i.Id == draggedItemId));
                }
                Point world = ToWorld(screen);
                if (gesture == "move")
                {
                    Vector delta = world - startWorld;
                    snapGuides.Clear();
                    if (SnapEnabledForModifiers(Keyboard.Modifiers) && originals.Values.All(i => !i.IsText))
                    {
                        List<Tuple<Point, Point>> guides;
                        delta = CalculateMoveSnap(originals.Values,
                            Document.Items.Where(i => !Document.Selected.Contains(i.Id)), delta,
                            8 / Math.Max(.01, Document.Zoom), ImagePadding, out guides);
                        snapGuides.AddRange(guides);
                    }
                    foreach (ImageItem i in Document.Selection)
                    { ImageItem old = originals[i.Id]; i.X = old.X + delta.X; i.Y = old.Y + delta.Y; }
                }
                else if (gesture == "groupresize")
                {
                    snapGuides.Clear();
                    double factor = Vector.Multiply(world - anchor, diagonal) / diagonal.LengthSquared;
                    double minimum = originals.Values.Max(i => 1.0 / Math.Min(i.Width, i.Height));
                    double maximum = originals.Values.Min(i => 1000000.0 / Math.Max(i.Width, i.Height));
                    factor = Math.Max(minimum, Math.Min(maximum, factor));
                    if (SnapEnabledForModifiers(Keyboard.Modifiers))
                    {
                        List<Tuple<Point, Point>> guides;
                        factor = CalculateScaleSnap(VisibleBounds(originals.Values),
                            Document.Items.Where(i => !Document.Selected.Contains(i.Id)), anchor, factor,
                            minimum, maximum, 8 / Math.Max(.01, Document.Zoom), ImagePadding, out guides);
                        snapGuides.AddRange(guides);
                    }
                    ApplyGroupScale(Document.Selection, originals, anchor, diagonal, anchor + diagonal * factor);
                }
                else if (gesture == "grouprotate")
                {
                    double angle = Math.Atan2(world.Y - groupCenter.Y, world.X - groupCenter.X) * 180 / Math.PI;
                    groupRotationDelta = angle - startAngle;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) groupRotationDelta = Math.Round(groupRotationDelta / 15) * 15;
                    ApplyGroupRotation(Document.Selection, originals, groupCenter, groupRotationDelta);
                }
                else
                {
                    ImageItem item = Document.Items.FirstOrDefault(i => i.Id == transformStart.Id);
                    if (item == null) { FinishGesture(); return; }
                    if (gesture == "resize")
                    {
                        double factor = Vector.Multiply(world - anchor, diagonal) / diagonal.LengthSquared;
                        double minimum = 1.0 / Math.Min(transformStart.Width, transformStart.Height);
                        double maximum = 1000000.0 / Math.Max(transformStart.Width, transformStart.Height);
                        factor = Math.Max(minimum, Math.Min(maximum, factor));
                        snapGuides.Clear();
                        if (SnapEnabledForModifiers(Keyboard.Modifiers) && !transformStart.IsText)
                        {
                            List<Tuple<Point, Point>> guides;
                            factor = CalculateScaleSnap(VisibleWorldBounds(transformStart),
                                Document.Items.Where(i => i.Id != transformStart.Id), anchor, factor,
                                minimum, maximum, 8 / Math.Max(.01, Document.Zoom), ImagePadding, out guides);
                            snapGuides.AddRange(guides);
                        }
                        Point center = anchor + diagonal * factor / 2;
                        item.X = center.X; item.Y = center.Y;
                        item.Width = transformStart.Width * factor; item.Height = transformStart.Height * factor;
                    }
                    else if (gesture == "mask") ApplyMask(item, transformStart, maskEdge, world);
                    else if (gesture == "maskmove") ApplyMaskOffset(item, transformStart, startWorld, world);
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
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (HoveredImageId != null) { HoveredImageId = null; InvalidateVisual(); }
        }
        public static double NormalizeAngle(double angle) { return ((angle % 360) + 360) % 360; }
        internal static double ApplyGroupScale(IEnumerable<ImageItem> items, IDictionary<string, ImageItem> bases,
            Point fixedAnchor, Vector startDiagonal, Point current)
        {
            if (bases == null || bases.Count == 0 || startDiagonal.LengthSquared < .000001) return 1;
            double factor = Vector.Multiply(current - fixedAnchor, startDiagonal) / startDiagonal.LengthSquared;
            double minimum = bases.Values.Max(i => 1.0 / Math.Min(i.Width, i.Height));
            double maximum = bases.Values.Min(i => 1000000.0 / Math.Max(i.Width, i.Height));
            factor = Math.Max(minimum, Math.Min(maximum, factor));
            foreach (ImageItem item in items)
            {
                ImageItem basis;
                if (!bases.TryGetValue(item.Id, out basis)) continue;
                item.X = fixedAnchor.X + (basis.X - fixedAnchor.X) * factor;
                item.Y = fixedAnchor.Y + (basis.Y - fixedAnchor.Y) * factor;
                item.Width = basis.Width * factor; item.Height = basis.Height * factor;
            }
            return factor;
        }
        internal static void ApplyGroupRotation(IEnumerable<ImageItem> items, IDictionary<string, ImageItem> bases,
            Point center, double angle)
        {
            Matrix rotation = Matrix.Identity; rotation.Rotate(angle);
            foreach (ImageItem item in items)
            {
                ImageItem basis;
                if (!bases.TryGetValue(item.Id, out basis)) continue;
                Vector offset = rotation.Transform(new Vector(basis.X - center.X, basis.Y - center.Y));
                item.X = center.X + offset.X; item.Y = center.Y + offset.Y;
                item.Rotation = NormalizeAngle(basis.Rotation + angle);
            }
        }
        internal static double DragZoomTarget(double initialZoom, double verticalDelta, bool inverted)
        { return Math.Max(.01, Math.Min(16, initialZoom * Math.Exp(verticalDelta * (inverted ? -1 : 1) / 180.0))); }
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
            snapGuides.Clear();
            groupStartBounds = Rect.Empty; groupRotationDelta = 0;
            maskEdge = -1; draggedItemId = null;
            if (IsMouseCaptured) ReleaseMouseCapture(); Cursor = Cursors.Arrow; InvalidateVisual();
        }
    }
}
