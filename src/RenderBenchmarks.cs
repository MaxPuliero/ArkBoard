using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ArkBoard
{
    internal static class RenderBenchmarks
    {
        internal static async Task Run(string output)
        {
            Directory.CreateDirectory(output);
            var window = new MainWindow(true) { ShowActivated = false, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            window.Show();
            var pixels = new DrawingVisual();
            using (var dc = pixels.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Colors.SteelBlue, Colors.DarkSlateGray, 45), null, new Rect(0, 0, 1200, 800));
                for (int i = 0; i < 60; i++) dc.DrawEllipse(Brushes.LightGray, null, new Point(20 * i, 400), 7, 120);
            }
            var bitmap = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32); bitmap.Render(pixels);
            AssetData asset = AssetData.FromBitmap(bitmap);
            for (int i = 0; i < 24; i++)
            {
                ImageItem item = window.Document.Add(asset, "Benchmark", new Point(i % 6 * 550, i / 6 * 400));
                item.Rotation = (i % 5 - 2) * 7;
            }
            window.Document.Notify();
            using (var report = new StreamWriter(Path.Combine(output, "render-benchmark.txt")))
            {
                report.WriteLine("WPF rendering tier: " + (RenderCapability.Tier >> 16));
                report.WriteLine("Synthetic 24-image scene. Off-screen software snapshot timings; these are NOT desktop FPS.");
                foreach (int width in new[] { 1280, 1920, 2560 })
                {
                    window.Width = width; window.Height = width * .64;
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    window.Board.Fit(false);
                    var target = new RenderTargetBitmap((int)window.Root.ActualWidth, (int)window.Root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    double total = 0;
                    for (int frame = 0; frame < 4; frame++)
                    {
                        var timer = Stopwatch.StartNew();
                        window.Board.ZoomAt(new Point(0, 0), window.Document.Zoom * 1.0001); window.Root.UpdateLayout();
                        target.Clear(); target.Render(window.Root); timer.Stop();
                        if (frame > 0) total += timer.Elapsed.TotalMilliseconds;
                    }
                    report.WriteLine(width + " px: " + (total / 3).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " ms/snapshot");
                }
            }
            window.Close();
        }
    }
}
