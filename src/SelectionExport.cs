using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    internal sealed class ExportDrawing
    {
        internal DrawingGroup Drawing;
        internal Rect Bounds;
        internal string Name;
        internal double PixelScale = 1;
    }

    internal static class SelectionExport
    {
        internal const int MaxSide = 16384;
        internal const long MaxPixels = 80000000;
        internal static Rect Bounds(IEnumerable<ImageItem> items)
        {
            Rect bounds = Rect.Empty;
            foreach (ImageItem item in items) bounds.Union(new MatrixTransform(item.Matrix).TransformBounds(item.IsText
                ? new Rect(-item.Width / 2, -item.Height / 2, item.Width, item.Height) : item.VisibleRect));
            return bounds;
        }
        internal static ExportDrawing Capture(BoardDocument document, IEnumerable<ImageItem> source)
        {
            List<ImageItem> items = source.Select(i => i.Copy()).ToList();
            if (items.Count == 0) throw new InvalidOperationException(Localization.T("Select objects first."));
            var drawing = new DrawingGroup();
            using (DrawingContext dc = drawing.Open())
            {
                foreach (ImageItem item in items)
                {
                    dc.PushTransform(new MatrixTransform(item.Matrix));
                    if (item.IsText) TextLayout.Draw(dc, item);
                    else
                    {
                        dc.PushOpacity(1 - item.Transparency);
                        dc.PushClip(new RectangleGeometry(item.VisibleRect));
                        dc.DrawImage(document.Assets[item.Asset].BitmapFor(item), new Rect(-item.Width / 2, -item.Height / 2, item.Width, item.Height));
                        dc.Pop(); dc.Pop();
                    }
                    dc.Pop();
                }
            }
            drawing.Freeze();
            var result = new ExportDrawing { Drawing = drawing, Bounds = Bounds(items), Name = items[0].Name };
            if (items.Count == 1 && !items[0].IsText)
            {
                BitmapSource bitmap = document.Assets[items[0].Asset].BitmapFor(items[0]);
                result.PixelScale = Math.Max(bitmap.PixelWidth / items[0].Width, bitmap.PixelHeight / items[0].Height);
            }
            return result;
        }
        internal static bool ValidSize(int width, int height)
        { return width >= 1 && height >= 1 && width <= MaxSide && height <= MaxSide && (long)width * height <= MaxPixels; }
        internal static Size DefaultSize(Rect bounds, double scale)
        {
            double width = Math.Max(1, bounds.Width * scale), height = Math.Max(1, bounds.Height * scale);
            double reduction = Math.Min(1, Math.Min(MaxSide / Math.Max(width, height), Math.Sqrt((double)MaxPixels / (width * height))));
            return new Size(Math.Max(1, Math.Floor(width * reduction)), Math.Max(1, Math.Floor(height * reduction)));
        }
        internal static BitmapSource Render(ExportDrawing snapshot, int width, int height, bool transparent)
        {
            if (!ValidSize(width, height)) throw new InvalidOperationException(Localization.T("Use 1–16,384 pixels per side, up to 80 megapixels."));
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = visual.RenderOpen())
            {
                if (!transparent) dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                Rect bounds = snapshot.Bounds;
                double scale = Math.Min(width / bounds.Width, height / bounds.Height);
                dc.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                    (width - bounds.Width * scale) / 2 - bounds.Left * scale,
                    (height - bounds.Height * scale) / 2 - bounds.Top * scale));
                dc.DrawDrawing(snapshot.Drawing); dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
        internal static void SavePng(string path, BitmapSource bitmap, bool replace)
        {
            string full = Path.GetFullPath(path), temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(file);
                if (replace && File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static string AvailableImagePath(string folder, string name)
        {
            string stem = (name ?? "Image").Replace('\\', '/');
            stem = stem.Substring(stem.LastIndexOf('/') + 1);
            foreach (char c in Path.GetInvalidFileNameChars()) stem = stem.Replace(c, '_');
            int extension = stem.LastIndexOf('.'); if (extension > 0) stem = stem.Substring(0, extension);
            stem = stem.Trim().TrimEnd('.'); if (stem.Length == 0) stem = "Image";
            if (stem.Length > 100) stem = stem.Substring(0, 100);
            // Prefixing reserved device names also makes them safe on Windows.
            string device = stem.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$" }.Contains(device) ||
                (device.Length == 4 && (device.StartsWith("COM") || device.StartsWith("LPT")) && char.IsDigit(device[3]))) stem = "_" + stem;
            string path = Path.Combine(folder, stem + ".png"); int suffix = 2;
            while (File.Exists(path) || Directory.Exists(path)) path = Path.Combine(folder, stem + " (" + suffix++ + ").png");
            return path;
        }
        // WPF rasterization needs an STA; a dedicated worker also keeps the canvas responsive.
        internal static Task RunAsync(Action action)
        {
            var completion = new TaskCompletionSource<bool>();
            var worker = new Thread(() =>
            {
                try { action(); completion.SetResult(true); }
                catch (Exception ex) { completion.SetException(ex); }
            });
            worker.IsBackground = true; worker.SetApartmentState(ApartmentState.STA); worker.Start();
            return completion.Task;
        }
    }
}
