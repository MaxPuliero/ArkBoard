using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArkBoard
{
    public static class SelfTests
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); checks.Add("PASS: " + message); }
        static bool Near(double a, double b) { return Math.Abs(a - b) < .00001; }
        static AssetData Sample(int width, int height, Color color, string title)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(color, Color.FromRgb(24, 35, 58), 55), null, new Rect(0, 0, width, height));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), null, new Point(width * .68, height * .36), height * .22, height * .22);
                StreamGeometry mountain = new StreamGeometry();
                using (var g = mountain.Open())
                { g.BeginFigure(new Point(0, height), true, true); g.LineTo(new Point(width * .37, height * .35), true, false); g.LineTo(new Point(width * .72, height), true, false); }
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(160, 15, 28, 46)), null, mountain);
                dc.DrawText(new FormattedText(title, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 24, Brushes.White, 1), new Point(24, height - 54));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
            return AssetData.FromBitmap(bitmap);
        }
        static void Be16(BinaryWriter writer, int value) { writer.Write((byte)(value >> 8)); writer.Write((byte)value); }
        static void Be32(BinaryWriter writer, int value)
        { writer.Write((byte)(value >> 24)); writer.Write((byte)(value >> 16)); writer.Write((byte)(value >> 8)); writer.Write((byte)value); }
        static byte[] PsdChannel(byte[] pixels, bool rle)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                Be16(writer, rle ? 1 : 0);
                if (!rle) writer.Write(pixels);
                else
                {
                    Be16(writer, 3); Be16(writer, 3);
                    writer.Write((byte)1); writer.Write(pixels, 0, 2);
                    writer.Write((byte)1); writer.Write(pixels, 2, 2);
                }
                return stream.ToArray();
            }
        }
        static byte[] SamplePsd()
        {
            byte[][] top = { PsdChannel(new byte[] { 255, 255, 255, 255 }, false), PsdChannel(new byte[4], false),
                PsdChannel(new byte[4], false), PsdChannel(new byte[] { 255, 255, 255, 255 }, false) };
            byte[][] bottom = { PsdChannel(new byte[4], true), PsdChannel(new byte[4], true),
                PsdChannel(new byte[] { 255, 255, 255, 255 }, true), PsdChannel(new byte[] { 255, 255, 255, 255 }, true) };
            using (var layerInfoStream = new MemoryStream()) using (var layerWriter = new BinaryWriter(layerInfoStream))
            {
                Be16(layerWriter, 2);
                Action<string, byte[][]> record = (name, channels) =>
                {
                    Be32(layerWriter, 0); Be32(layerWriter, 0); Be32(layerWriter, 2); Be32(layerWriter, 2);
                    Be16(layerWriter, 4);
                    int[] ids = { 0, 1, 2, -1 };
                    for (int c = 0; c < 4; c++) { Be16(layerWriter, ids[c]); Be32(layerWriter, channels[c].Length); }
                    layerWriter.Write(Encoding.ASCII.GetBytes("8BIMnorm"));
                    layerWriter.Write((byte)255); layerWriter.Write((byte)0); layerWriter.Write((byte)0); layerWriter.Write((byte)0);
                    byte[] label = Encoding.ASCII.GetBytes(name); int paddedName = ((label.Length + 1 + 3) / 4) * 4;
                    Be32(layerWriter, 8 + paddedName); Be32(layerWriter, 0); Be32(layerWriter, 0);
                    layerWriter.Write((byte)label.Length); layerWriter.Write(label);
                    for (int p = label.Length + 1; p < paddedName; p++) layerWriter.Write((byte)0);
                };
                // PSD layer records are ordered bottom-to-top.
                record("Bottom Blue", bottom); record("Top Red", top);
                foreach (byte[] channel in bottom.Concat(top)) layerWriter.Write(channel);
                byte[] layerInfo = layerInfoStream.ToArray();
                using (var maskStream = new MemoryStream()) using (var maskWriter = new BinaryWriter(maskStream))
                {
                    Be32(maskWriter, layerInfo.Length); maskWriter.Write(layerInfo); if ((layerInfo.Length & 1) != 0) maskWriter.Write((byte)0); Be32(maskWriter, 0);
                    byte[] layerMask = maskStream.ToArray();
                    using (var file = new MemoryStream()) using (var writer = new BinaryWriter(file))
                    {
                        writer.Write(Encoding.ASCII.GetBytes("8BPS")); Be16(writer, 1); writer.Write(new byte[6]); Be16(writer, 4);
                        Be32(writer, 2); Be32(writer, 2); Be16(writer, 8); Be16(writer, 3);
                        Be32(writer, 0); Be32(writer, 0); Be32(writer, layerMask.Length); writer.Write(layerMask);
                        Be16(writer, 0); writer.Write(new byte[16]);
                        return file.ToArray();
                    }
                }
            }
        }
        static Color Pixel(BitmapSource bitmap, int x, int y)
        {
            BitmapSource source = bitmap.Format == PixelFormats.Bgra32 ? bitmap : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            byte[] pixel = new byte[4]; source.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
        static void Capture(MainWindow window, string path)
        {
            window.Root.UpdateLayout();
            int width = (int)Math.Ceiling(window.Root.ActualWidth), height = (int)Math.Ceiling(window.Root.ActualHeight);
            Check(width > 800 && height > 500, "WPF window measured and arranged");
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window.Root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (Stream s = File.Create(path)) png.Save(s);
        }
        static void CaptureContextMenu(MainWindow window, string path)
        {
            var menu = window.Board.ContextMenu;
            menu.ApplyTemplate();
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            menu.Arrange(new Rect(menu.DesiredSize)); menu.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(menu);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (Stream s = File.Create(path)) png.Save(s);
        }
        public static async Task Run(string folder)
        {
            Directory.CreateDirectory(folder);
            checks.Clear();
            foreach (string resultFile in new[] { "FAILED.txt", "results.txt" })
            { string oldResult = Path.Combine(folder, resultFile); if (File.Exists(oldResult)) File.Delete(oldResult); }
            AssetData blue = Sample(600, 400, Color.FromRgb(53, 112, 174), "01 / SHAPES");
            AssetData pink = Sample(360, 500, Color.FromRgb(171, 89, 113), "02 / COLOR");
            AssetData green = Sample(640, 360, Color.FromRgb(55, 139, 129), "03 / ATMOSPHERE");
            Check(blue.Bitmap.PixelWidth == 600 && blue.Bitmap.PixelHeight == 400, "PNG decode preserves source dimensions");
            Check(AssetData.Create(blue.Bytes).Key == blue.Key, "Identical images have identical content hashes");
            AssetData psd = AssetData.Create(SamplePsd());
            Check(psd.IsPsd && psd.Psd.Layers.Count == 2 && psd.Psd.Layers[0].Name == "Bottom Blue" && psd.Psd.Layers[1].Name == "Top Red",
                "Basic PSD import reads named raster layers with raw and PackBits channel data");
            var psdDoc = new BoardDocument(); ImageItem psdItem = psdDoc.Add(psd, "Layers.psd", new Point(40, 40));
            Check(psdItem.LayerVisibility.SequenceEqual(new[] { true, true }) && Pixel(psd.BitmapFor(psdItem), 0, 0).R > 240,
                "PSD import initializes Photoshop layer visibility and composites the top layer");
            psdDoc.Change(() => psdItem.LayerVisibility[1] = false);
            Check(Pixel(psd.BitmapFor(psdItem), 0, 0).B > 240, "Turning off a PSD layer reveals the layer below it");
            psdDoc.Undo(); psdItem = psdDoc.Items.Single();
            Check(psdItem.LayerVisibility[1] && Pixel(psd.BitmapFor(psdItem), 0, 0).R > 240,
                "PSD layer visibility changes can be undone without sharing mutable state");
            psdDoc.Redo(); psdItem = psdDoc.Items.Single();
            string psdProject = Path.Combine(folder, "psd-layers.arkboard"); psdDoc.Save(psdProject);
            using (var file = File.OpenRead(psdProject)) using (var zip = new ZipArchive(file, ZipArchiveMode.Read)) using (var entry = zip.GetEntry("manifest.json").Open())
                Check(((Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(entry)).Version == 4,
                    "Projects containing PSD layer state use manifest version 4");
            var psdRead = new BoardDocument(); psdRead.Load(psdProject);
            Check(psdRead.Items.Single().LayerVisibility.SequenceEqual(new[] { true, false }) && psdRead.Assets.Single().Value.IsPsd &&
                Pixel(psdRead.Assets.Single().Value.BitmapFor(psdRead.Items.Single()), 0, 0).B > 240,
                "ArkBoard projects embed PSD source data and preserve per-item layer visibility");
            ImageItem psdClipboardItem; AssetData psdClipboardAsset;
            DataObject psdClipboard = ArkBoardClipboard.Create(psdRead.Items.Single(), psdRead.Assets.Single().Value);
            Check(ArkBoardClipboard.TryRead(psdClipboard, out psdClipboardItem, out psdClipboardAsset) && !psdClipboardItem.LayerVisibility[1] &&
                Pixel(psdClipboard.GetImage(), 0, 0).B > 240,
                "ArkBoard and standard bitmap clipboard data preserve the current PSD layer composite");
            var doc = new BoardDocument(); doc.Checkpoint();
            ImageItem first = doc.Add(blue, "Test blu.png", new Point(100, 200)); doc.Selected.Add(first.Id);
            Check(Near(first.Width / first.Height, 1.5), "Import preserves aspect ratio");
            doc.Change(() => { first.Rotation = 37; first.FlipX = true; first.FlipY = true; first.Width *= 1.8; first.Height *= 1.8; });
            Check(first.Contains(new Point(first.X, first.Y)) && !first.Contains(new Point(10000, 10000)), "Hit testing handles rotation and both flips");
            Matrix inv = first.Matrix; inv.Invert();
            Point p = new Point(23, -47); Point back = inv.Transform(first.Matrix.Transform(p));
            Check(Near(p.X, back.X) && Near(p.Y, back.Y), "Image transform has a stable inverse");
            doc.Undo(); Check(Near(doc.Items[0].Rotation, 0) && !doc.Items[0].FlipX, "Undo restores image transformations");
            doc.Redo(); Check(Near(doc.Items[0].Rotation, 37) && doc.Items[0].FlipX && doc.Items[0].FlipY, "Redo restores image transformations");
            doc.Change(() => doc.Add(blue, "Duplicato.png", new Point(900, 200)));
            doc.Zoom = .73; doc.PanX = 71; doc.PanY = -23;
            string project = Path.Combine(folder, "roundtrip.refcanvas"); doc.Save(project);
            using (var file = File.OpenRead(project)) using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                Check(zip.Entries.Count(e => e.FullName.StartsWith("assets/")) == 1, "Project packs duplicate images only once");
            var read = new BoardDocument(); read.Load(project);
            Check(read.Items.Count == 2 && read.Assets.Count == 1 && read.Assets[blue.Key].Bytes.SequenceEqual(blue.Bytes), "ZIP round trip preserves exact original bytes without external paths");
            Check(read.Items[0].FlipX && read.Items[0].FlipY && Near(read.Items[0].Rotation, 37) && Near(read.Zoom, .73) && Near(read.PanY, -23), "ZIP round trip preserves transforms and viewport");
            doc.Change(() => doc.Items.RemoveAt(1)); doc.Save(project); read.Load(project);
            Check(read.Items.Count == 1 && !doc.Dirty, "Atomic overwrite replaces the saved project");
            string legacy = Path.Combine(folder, "legacy.refcanvas"); File.Copy(project, legacy, true);
            using (var file = new FileStream(legacy, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            {
                zip.GetEntry("manifest.json").Delete();
                using (var entry = zip.CreateEntry("manifest.json").Open())
                    new DataContractJsonSerializer(typeof(Manifest)).WriteObject(entry, new Manifest { Format = "RefCanvas", Images = read.Items });
            }
            var legacyRead = new BoardDocument(); legacyRead.Load(legacy);
            Check(legacyRead.Items.Count == read.Items.Count && legacyRead.Assets[blue.Key].Bytes.SequenceEqual(blue.Bytes), "ArkBoard opens legacy RefCanvas projects without changing image data");
            legacyRead.Save(Path.Combine(folder, "migrated.arkboard"));
            using (var file = File.OpenRead(legacyRead.Path)) using (var zip = new ZipArchive(file, ZipArchiveMode.Read)) using (var entry = zip.GetEntry("manifest.json").Open())
                Check(((Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(entry)).Format == "ArkBoard", "New projects identify themselves as ArkBoard");
            var maskDoc = new BoardDocument();
            ImageItem masked = maskDoc.Add(blue, "Masked.png", new Point(300, 200));
            ImageItem maskBasis = masked.Copy(); Rect fullBounds = masked.Bounds();
            BoardSurface.ApplyMask(masked, maskBasis, 0, maskBasis.Matrix.Transform(new Point(-maskBasis.Width * .2, 0)));
            Check(Near(masked.MaskLeft, .3) && Near(masked.VisibleRect.Width, masked.Width * .7) && masked.Bounds() == fullBounds,
                "Masking changes the visible image area while preserving the original bounding box");
            string maskProject = Path.Combine(folder, "masked.arkboard"); maskDoc.Save(maskProject);
            using (var file = File.OpenRead(maskProject)) using (var zip = new ZipArchive(file, ZipArchiveMode.Read)) using (var entry = zip.GetEntry("manifest.json").Open())
                Check(((Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(entry)).Version == 3,
                    "Masked projects use manifest version 3");
            var maskRead = new BoardDocument(); maskRead.Load(maskProject);
            Check(Near(maskRead.Items.Single().MaskLeft, .3) && maskRead.Items.Single().HasMask,
                "Project round trip preserves non-destructive image masks");
            maskRead.Items.Single().Rotation = 27; maskRead.Items.Single().FlipX = true;
            maskRead.Items.Single().Width *= .8; maskRead.Items.Single().Height *= .8;
            ImageItem clipboardItem; AssetData clipboardAsset;
            DataObject clipboardData = ArkBoardClipboard.Create(maskRead.Items.Single(), maskRead.Assets[blue.Key]);
            Check(ArkBoardClipboard.TryRead(clipboardData, out clipboardItem, out clipboardAsset) && clipboardItem.HasMask &&
                Near(clipboardItem.MaskLeft, .3) && Near(clipboardItem.Rotation, 27) && clipboardItem.FlipX &&
                Near(clipboardItem.Width, maskRead.Items.Single().Width) && clipboardAsset.Bytes.SequenceEqual(blue.Bytes),
                "ArkBoard clipboard data preserves mask, transforms and exact embedded asset bytes");
            ImageItem movingMask = maskRead.Items.Single().Copy(), movingBasis = maskRead.Items.Single().Copy();
            movingBasis.MaskTop = movingMask.MaskTop = .2; movingBasis.MaskBottom = movingMask.MaskBottom = .1;
            Point maskMoveStart = movingBasis.Matrix.Transform(new Point(0, 0));
            Point maskMoveEnd = movingBasis.Matrix.Transform(new Point(-movingBasis.Width * .1, movingBasis.Height * .05));
            BoardSurface.ApplyMaskOffset(movingMask, movingBasis, maskMoveStart, maskMoveEnd);
            Check(Near(movingMask.MaskLeft, .2) && Near(movingMask.MaskRight, .1) && Near(movingMask.MaskTop, .25) &&
                Near(movingMask.MaskBottom, .05) && movingMask.Bounds() == movingBasis.Bounds(),
                "Moving a mask preserves its size and the image bounding box while clamping it inside the image");
            masked.MaskRight = .8; string invalidMaskProject = Path.Combine(folder, "invalid-mask.arkboard");
            maskDoc.Save(invalidMaskProject); bool invalidMaskFailed = false;
            try { maskRead.Load(invalidMaskProject); } catch (InvalidDataException) { invalidMaskFailed = true; }
            Check(invalidMaskFailed && Near(maskRead.Items.Single().MaskLeft, .3),
                "Invalid mask geometry is rejected without replacing the open board");
            var sortDoc = new BoardDocument();
            ImageItem sortFirst = sortDoc.Add(blue, "First.png", new Point());
            ImageItem sortSecond = sortDoc.Add(pink, "Second.png", new Point());
            sortDoc.Selected.Add(sortFirst.Id);
            var sortBoard = new BoardSurface(sortDoc);
            Check(sortBoard.AutoSorting && sortBoard.AutoSortSelection(sortFirst) && sortDoc.Items.Last() == sortFirst,
                "Auto-sorting is enabled by default and brings a dragged image selection to the top");
            Point[] rotationHandles = sortBoard.RotationHandles(sortFirst);
            Point[] rotationCorners = sortFirst.Corners().Select(sortBoard.ToScreen).ToArray();
            Check(rotationHandles.Length == 4 && Enumerable.Range(0, 4).All(k =>
                    Math.Abs((rotationHandles[k] - rotationCorners[k]).Length - Math.Sqrt(27 * 27 * 2)) < .001) &&
                    sortBoard.RotationHandleAt(sortFirst, rotationHandles[2]) == 2,
                "Four inset rotation anchors stay clear of the corner scale handles and have generous hit targets");
            using (Stream cursorStream = typeof(BoardSurface).Assembly.GetManifestResourceStream("ArkBoard.RotateCursor"))
            {
                Check(cursorStream != null, "The rotation cursor is embedded in the portable executable");
                using (var cursor = new Cursor(cursorStream)) Check(cursor != null, "The embedded rotation cursor is a valid Windows cursor");
            }
            sortBoard.AutoSorting = false; sortDoc.Selected.Clear(); sortDoc.Selected.Add(sortSecond.Id);
            Check(!sortBoard.AutoSortSelection(sortSecond) && sortDoc.Items.Last() == sortFirst,
                "Disabling auto-sorting preserves the existing stacking order");
            string broken = Path.Combine(folder, "broken.refcanvas"); File.WriteAllText(broken, "invalid zip");
            bool failed = false; try { read.Load(broken); } catch { failed = true; }
            Check(failed && read.Items.Count == 1 && read.Assets.Count == 1, "Invalid archive leaves the existing board intact");
            string missing = Path.Combine(folder, "missing.refcanvas");
            using (var stream = File.Create(missing)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                using (var entry = zip.CreateEntry("manifest.json").Open())
                    new DataContractJsonSerializer(typeof(Manifest)).WriteObject(entry, new Manifest { Images = read.Items });
            failed = false; try { read.Load(missing); } catch (InvalidDataException) { failed = true; }
            Check(failed && read.Items.Count == 1, "Missing packed asset is rejected without replacing the board");
            string badNumbers = Path.Combine(folder, "bad-numbers.refcanvas");
            var invalidItem = read.Items[0].Copy(); invalidItem.Width = -2;
            using (var stream = File.Create(badNumbers)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                using (var entry = zip.CreateEntry("manifest.json").Open())
                    new DataContractJsonSerializer(typeof(Manifest)).WriteObject(entry, new Manifest { Images = new List<ImageItem> { invalidItem } });
            failed = false; try { read.Load(badNumbers); } catch (InvalidDataException) { failed = true; }
            Check(failed, "Invalid geometry is rejected");
            var urls = Importer.ExtractHtmlImages("SourceURL:https://example.com/gallery/page\r\n<img src='../images/a.png?x=1&amp;y=2'><img src=\"https://example.com/b.jpg\">");
            Check(urls.Count == 2 && urls[0] == "https://example.com/images/a.png?x=1&y=2", "Browser HTML resolves relative image URLs and entities");
            var data = new DataObject(); data.SetData(DataFormats.Html, "<img src='https://example.com/image.png'>"); data.SetData(DataFormats.UnicodeText, "https://example.com/page");
            Check(Importer.Extract(data)[0].Location == "https://example.com/image.png", "Browser image source takes precedence over page URL");
            string local = Path.Combine(folder, "temporary-source.png"); File.WriteAllBytes(local, pink.Bytes);
            AssetData imported = await Importer.LoadAsync(new ImportSource { Location = local, Name = "temp.png" });
            File.Delete(local);
            var portable = new BoardDocument(); portable.Add(imported, "temp.png", new Point()); portable.Save(Path.Combine(folder, "portable.refcanvas"));
            var portableRead = new BoardDocument(); portableRead.Load(portable.Path);
            Check(portableRead.Assets[imported.Key].Bitmap.PixelHeight == 500, "Project reopens after original file is deleted");
            AssetData fromData = await Importer.LoadAsync(new ImportSource { Location = "data:image/png;base64," + Convert.ToBase64String(green.Bytes), Name = "data.png" });
            Check(fromData.Key == green.Key, "Data image URLs import correctly");
            doc.Change(() => { doc.Items.Clear(); doc.Selected.Clear(); }); doc.Undo();
            Check(doc.Items.Count == 1 && doc.Assets.ContainsKey(doc.Items[0].Asset), "Undo of deletion retains the packed image data");

            var window = new MainWindow(true) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(!WindowTransparency.IsLayered(new System.Windows.Interop.WindowInteropHelper(window).Handle), "Opaque windows start without the layered transparency path");
            window.Board.ZoomAt(new Point(100, 100), 1);
            Check(RenderOptions.GetBitmapScalingMode(window.Board) == BitmapScalingMode.LowQuality, "Interaction uses fast bitmap sampling");
            await Task.Delay(300);
            Check(RenderOptions.GetBitmapScalingMode(window.Board) == BitmapScalingMode.HighQuality, "Full-quality bitmap sampling returns after interaction settles");
            double fullCanvasWidth = window.Board.ActualWidth;
            Check(window.quickControlsExpander.IsExpanded && window.quickControlsExpander.VerticalAlignment == VerticalAlignment.Bottom &&
                window.quickControlsExpander.HorizontalAlignment == HorizontalAlignment.Left &&
                ((SolidColorBrush)window.quickControlsExpander.Background).Color.A == 217,
                "Quick Controls starts expanded at the bottom-left with an 85 percent opaque backplate");
            ScrollViewer quickScroll = window.quickControlsExpander.Content as ScrollViewer;
            Check(quickScroll != null && ((StackPanel)quickScroll.Content).Children.Count == 32 && quickScroll.MaxHeight == 455,
                "Quick Controls lists every implemented shortcut in a bounded scrollable panel");
            Check(window.Resources[typeof(Expander)] is Style && ((Style)window.Resources[typeof(Expander)]).Setters.Count > 0,
                "Quick Controls and Layers use the square outline expander style");
            Check(window.topmostButton != null && window.lockButton != null && window.opacityControls.Children.Contains(window.lockButton),
                "Eye and lock controls sit beside the opacity controls");
            window.SetLocked(true);
            Check(window.Locked && window.IsLockInteractive(window.opacitySlider) && window.IsLockInteractive(window.lockButton) &&
                !window.IsLockInteractive(window.topmostButton) && !window.IsLockInteractive(window.Board),
                "Locked mode passes canvas and eye input through while opacity and unlock remain interactive");
            Capture(window, Path.Combine(folder, "ui-locked.png"));
            window.SetLocked(false); Check(!window.Locked, "Board lock can be released from its persistent control");
            Check(BoardSurface.DragZoomTarget(1, 180, false) > 2.7 && BoardSurface.DragZoomTarget(1, 180, true) < .38,
                "Alt plus middle-button vertical drag zooms down by default and supports inversion");
            window.SetLanguage(UiLanguage.Italian);
            Check((string)window.quickControlsExpander.Header == "COMANDI RAPIDI", "Italian UI can be selected at runtime");
            window.SetLanguage(UiLanguage.Japanese);
            Check((string)window.layersExpander.Header == "レイヤー", "Japanese UI can be selected at runtime");
            Check(window.languageItems.Select(item => (string)item.Header).SequenceEqual(new[] { "English", "Italiano", "日本語" }),
                "Language choices keep their native labels independently of the active UI language");
            Capture(window, Path.Combine(folder, "ui-japanese.png"));
            window.SetLanguage(UiLanguage.English);
            window.quickControlsExpander.IsExpanded = false;
            Check(!window.quickControlsExpander.IsExpanded, "Quick Controls can be collapsed to its header");
            Capture(window, Path.Combine(folder, "ui-quick-controls-collapsed.png"));
            window.quickControlsExpander.IsExpanded = true;
            Capture(window, Path.Combine(folder, "ui-empty.png"));
            await window.ImportSources(new List<ImportSource> { new ImportSource { Bytes = blue.Bytes, Name = "Shapes.png" }, new ImportSource { Bytes = pink.Bytes, Name = "Color.png" }, new ImportSource { Bytes = green.Bytes, Name = "Atmosphere.png" } }, new Point(0, 0));
            Check(window.Document.Items.Count == 3, "UI import pipeline adds and selects multiple images");
            Check(Near(fullCanvasWidth - window.Board.ActualWidth, 360), "Selecting images reveals the wider readable inspector and reserves its width");
            var wi = window.Document.Items;
            wi[0].X = 270; wi[0].Y = 200; wi[0].Rotation = -8;
            wi[1].X = 760; wi[1].Y = 300; wi[1].FlipX = true;
            wi[2].X = 280; wi[2].Y = 630; wi[2].Rotation = 5;
            window.Document.Selected.Clear(); window.Document.Selected.Add(wi[0].Id);
            window.Document.Notify(); window.Board.Fit(false);
            Point world = new Point(213, 347); Point screen = window.Board.ToScreen(world); Point round = window.Board.ToWorld(screen);
            Check(Near(world.X, round.X) && Near(world.Y, round.Y), "Canvas world/screen coordinate round trip");
            Point pointer = new Point(325, 222); Point before = window.Board.ToWorld(pointer);
            window.Board.ZoomAt(pointer, window.Document.Zoom * 1.7); Point after = window.Board.ToWorld(pointer);
            Check(Near(before.X, after.X) && Near(before.Y, after.Y), "Zoom remains anchored under the cursor");
            window.Board.Fit(false);
            Check(window.Board.Hit(window.Board.ToScreen(new Point(wi[1].X, wi[1].Y))) == wi[1], "Canvas hits transformed image at its visible center");
            Rect selectionBounds = wi[0].Bounds(); ImageItem uiMaskBasis = wi[0].Copy();
            BoardSurface.ApplyMask(wi[0], uiMaskBasis, 2, uiMaskBasis.Matrix.Transform(new Point(uiMaskBasis.Width * .1, 0)));
            window.Document.Notify();
            Check(wi[0].HasMask && wi[0].Bounds() == selectionBounds && wi[0].VisibleRect.Width < wi[0].Width,
                "A selected masked image keeps its full selection geometry");
            Capture(window, Path.Combine(folder, "ui-masked.png"));
            window.Board.MaskEditingId = null; window.Board.ShiftPreview = true; window.Board.HoveredImageId = wi[0].Id; window.Board.InvalidateVisual();
            Point[] previewMaskCorners = new[] { wi[0].Matrix.Transform(wi[0].VisibleRect.TopLeft), wi[0].Matrix.Transform(wi[0].VisibleRect.TopRight) };
            Point previewMaskTopMiddle = window.Board.ToScreen(new Point((previewMaskCorners[0].X + previewMaskCorners[1].X) / 2, (previewMaskCorners[0].Y + previewMaskCorners[1].Y) / 2));
            Check(window.Board.MaskEdgeAt(wi[0], previewMaskTopMiddle) == 1,
                "Holding Shift over an image previews actionable controls on the visible mask edges before clicking");
            Capture(window, Path.Combine(folder, "ui-mask-shift-preview.png"));
            window.Board.ShiftPreview = false; window.Board.HoveredImageId = null;
            window.Board.MaskEditingId = wi[0].Id; window.Board.InvalidateVisual();
            Point[] maskCorners = new[] { wi[0].Matrix.Transform(wi[0].VisibleRect.TopLeft), wi[0].Matrix.Transform(wi[0].VisibleRect.TopRight) };
            Point maskTopMiddle = window.Board.ToScreen(new Point((maskCorners[0].X + maskCorners[1].X) / 2, (maskCorners[0].Y + maskCorners[1].Y) / 2));
            Check(window.Board.MaskEdgeAt(wi[0], maskTopMiddle) == 1,
                "Mask adjustment mode targets the visible mask edge instead of the original outer edge");
            Capture(window, Path.Combine(folder, "ui-mask-adjustment.png"));
            window.RemoveMask();
            Check(!wi[0].HasMask, "Remove Mask restores selected masked images");
            window.Document.Undo();
            Check(window.Document.Items.Single(i => i.Id == wi[0].Id).HasMask, "Removing a mask can be undone");
            window.Document.Redo(); wi = window.Document.Items;
            Check(!wi.Single(i => i.Id == uiMaskBasis.Id).HasMask, "Removing a mask can be redone");
            window.Document.Save(Path.Combine(folder, "Example.arkboard"));
            window.SetStatus("Sample project · All images are embedded");
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Capture(window, Path.Combine(folder, "ui-board.png"));
            CaptureContextMenu(window, Path.Combine(folder, "ui-context-menu.png"));
            Point stableScreen = window.Board.ToScreen(new Point(wi[0].X, wi[0].Y));
            window.Document.Selected.Clear(); window.Document.Notify();
            Check(Near(window.Board.ActualWidth, fullCanvasWidth), "Deselecting hides the inspector and gives the canvas all available width");
            Point stableAfter = window.Board.ToScreen(new Point(wi[0].X, wi[0].Y));
            Check(Near(stableScreen.X, stableAfter.X) && Near(stableScreen.Y, stableAfter.Y), "Inspector visibility does not shift image coordinates");
            Capture(window, Path.Combine(folder, "ui-unselected.png"));
            window.Document.Selected.Add(wi[0].Id); window.Document.Selected.Add(wi[1].Id); window.Document.Notify();
            double savedWidth = wi[0].Width, savedX = wi[0].X;
            window.ResetRotation();
            Check(Near(wi[0].Rotation, 0) && Near(wi[1].Rotation, 0) && Near(wi[2].Rotation, 5) &&
                Near(wi[0].Width, savedWidth) && Near(wi[0].X, savedX) && wi[1].FlipX,
                "Reset rotation affects only the selection and preserves position, scale and flips");
            window.Document.Undo();
            Check(Near(window.Document.Items[0].Rotation, -8), "Reset rotation is undoable");
            var originalLayout = window.Document.Items.Select(i => i.Copy()).ToArray();
            double expectedSide = originalLayout.Take(2).Average(i => Math.Max(i.Width, i.Height));
            window.NormalizeSelected();
            Check(window.Document.Selection.All(i => Near(Math.Max(i.Width, i.Height), expectedSide)), "Normalize uses the selection's average longest side");
            Check(window.Document.Items.Select((i, k) => Near(i.Width / i.Height, originalLayout[k].Width / originalLayout[k].Height) &&
                Near(i.X, originalLayout[k].X) && Near(i.Y, originalLayout[k].Y) && Near(i.Rotation, originalLayout[k].Rotation) && i.FlipX == originalLayout[k].FlipX).All(v => v) &&
                Near(window.Document.Items[2].Width, originalLayout[2].Width), "Normalize preserves proportions, positions, flips and unselected images");
            window.Document.Undo();
            Check(window.Document.Items.Select((i, k) => Near(i.Width, originalLayout[k].Width)).All(v => v), "Normalize can be undone");
            window.Document.Redo();
            Check(window.Document.Selection.All(i => Near(Math.Max(i.Width, i.Height), expectedSide)), "Normalize can be redone");
            window.Document.Undo(); window.PackImages();
            Check(!window.Document.Items[0].Bounds().IntersectsWith(window.Document.Items[1].Bounds()) &&
                Near(window.Document.Items[2].X, originalLayout[2].X) && Near(window.Document.Items[2].Y, originalLayout[2].Y), "Packing a selection leaves other images untouched");
            window.Document.Undo();
            Check(window.Document.Items.Select((i, k) => Near(i.X, originalLayout[k].X) && Near(i.Y, originalLayout[k].Y)).All(v => v), "Packing can be undone");
            window.Document.Selected.Clear(); window.Document.Notify(); window.PackImages();
            Check(window.Document.Items.Select((i, k) => Near(i.Width, originalLayout[k].Width) && Near(i.Height, originalLayout[k].Height) &&
                Near(i.Rotation, originalLayout[k].Rotation) && i.FlipX == originalLayout[k].FlipX).All(v => v), "Packing all images preserves sizes, rotations, flips and stacking order");
            var random = new Random(712);
            var mixed = Enumerable.Range(0, 150).Select(k => new ImageItem { Width = 10 + random.Next(1500), Height = 10 + random.Next(1500),
                X = k * 2000, Y = -k * 1000, Rotation = random.Next(360), FlipX = k % 2 == 0 }).ToList();
            ImageLayout.Pack(mixed, 16);
            bool overlaps = false;
            for (int a = 0; a < mixed.Count; a++) for (int b = a + 1; b < mixed.Count; b++)
            { Rect ra = mixed[a].Bounds(); ra.Inflate(7.999, 7.999); Rect rb = mixed[b].Bounds(); rb.Inflate(7.999, 7.999); overlaps |= ra.IntersectsWith(rb); }
            Check(!overlaps, "Packing 150 mixed rotated rectangles maintains a 16-unit gap without overlaps");
            var packed = mixed.Select(i => i.Copy()).ToArray(); ImageLayout.Pack(mixed, 16);
            Check(mixed.Select((i, k) => Near(i.X, packed[k].X) && Near(i.Y, packed[k].Y)).All(v => v), "Repeated packing is deterministic");
            window.Document.Selected.UnionWith(window.Document.Items.Select(i => i.Id)); window.Document.Notify();
            Capture(window, Path.Combine(folder, "ui-packed.png"));
            uint color, flags; byte alpha;
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            window.SetWindowOpacity(5);
            bool hasAlpha = WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags);
            Check(hasAlpha && alpha == 13 && (flags & 2) != 0,
                "5 percent opacity reaches the native Windows compositor (available=" + hasAlpha + ", alpha=" + alpha + ", flags=" + flags + ")");
            window.SetWindowOpacity(55);
            Check(WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags) && alpha == 140,
                "Intermediate opacity updates native window alpha");
            window.Width -= 40;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            hasAlpha = WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags);
            Check(hasAlpha && alpha == 140,
                "Opacity survives window resize and WPF repaint (available=" + hasAlpha + ", alpha=" + alpha + ")");
            window.Topmost = true;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.Topmost = false;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags) && alpha == 140,
                "Always-on-top changes preserve window opacity");
            window.SetWindowOpacity(0);
            Check(WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags) && alpha == 13,
                "Opacity cannot drop below 5 percent");
            window.SetWindowOpacity(100);
            Check(!WindowTransparency.IsLayered(handle), "100 percent opacity removes the layered window path entirely");
            window.Width = 2400; window.Height = 1500;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(!WindowTransparency.IsLayered(handle), "Enlarging beyond startup dimensions keeps opaque presentation");
            window.SetWindowOpacity(55);
            Check(WindowTransparency.GetLayeredWindowAttributes(handle, out color, out alpha, out flags) && alpha == 140, "Transparency can be re-enabled after returning to opaque rendering");
            window.SetWindowOpacity(100);
            Check(!WindowTransparency.IsLayered(handle), "Repeated opacity toggles restore the normal rendering path");
            int beforeText = window.Document.Items.Count, beforeAssets = window.Document.Assets.Count;
            window.ActivateTextTool();
            Check(window.Board.TextToolArmed && window.Board.TextInputActive, "Text tool arms placement mode");
            window.CommitText();
            Check(!window.Board.TextToolArmed && window.Document.Items.Count == beforeText, "Exiting an unused text tool adds no empty object");
            window.StartTextEditor(new Point(-100, -180), 48);
            Check(window.ActiveTextEditor.AcceptsReturn && window.ActiveTextEditor.CaretBrush == TextLayout.Foreground, "Native text editor supports line breaks and the light-gray caret");
            window.ActiveTextEditor.Text = "ARKBOARD\nReference notes · 日本語";
            ImageItem textItem = window.CommitText();
            Check(textItem.IsText && textItem.Text.Contains("\n") && window.ActiveTextEditor == null && !window.Board.TextInputActive,
                "Confirming text creates a multiline canvas object and exits typing mode");
            Check(window.Document.Assets.Count == beforeAssets && Near(textItem.X - textItem.Width / 2, -100) && Near(textItem.Y - textItem.Height / 2, -180),
                "Text retains its placement and needs no external image asset");
            Check(window.Board.Hit(window.Board.ToScreen(new Point(textItem.X, textItem.Y))) == textItem, "Confirmed text participates in canvas hit testing");
            window.Document.Undo(); Check(window.Document.Items.Count == beforeText, "Text creation can be undone");
            window.Document.Redo(); textItem = window.Document.Items.Last();
            Check(textItem.IsText && textItem.Text.Contains("日本語"), "Text creation can be redone with Unicode preserved");
            double textX = textItem.X;
            window.Document.Change(() => { textItem.X += 75; textItem.Y += 20; });
            Check(Near(textItem.X, textX + 75), "Text objects use normal canvas movement");
            window.Document.Selected.Clear(); window.Document.Selected.Add(textItem.Id); window.Document.Notify();
            window.Rotate(90); window.Flip(true);
            Check(Near(textItem.Rotation, 0) && !textItem.FlipX && !textItem.FlipY,
                "Text objects ignore rotation and flip commands");
            int itemsBeforeEdit = window.Document.Items.Count; string textId = textItem.Id, textBeforeEdit = textItem.Text;
            Check(window.Board.TryEditTextAt(window.Board.ToScreen(new Point(textItem.X, textItem.Y))) &&
                window.ActiveTextEditor != null && window.ActiveTextEditor.Text == textBeforeEdit && window.Board.EditingTextId == textId,
                "Double-click text routing reopens the existing object in the native editor");
            window.ActiveTextEditor.Text += "\nDouble-click editing";
            textItem = window.CommitText();
            Check(textItem.Id == textId && window.Document.Items.Count == itemsBeforeEdit && textItem.Text.EndsWith("Double-click editing") &&
                window.ActiveTextEditor == null && window.Board.EditingTextId == null,
                "Confirming an edit updates the same text object without creating a duplicate");
            window.Document.Undo(); textItem = window.Document.Items.Single(i => i.Id == textId);
            Check(textItem.Text == textBeforeEdit, "Text edits can be undone");
            window.Document.Redo(); textItem = window.Document.Items.Single(i => i.Id == textId);
            Check(textItem.Text.EndsWith("Double-click editing"), "Text edits can be redone");
            string textProject = Path.Combine(folder, "Example-with-text.arkboard"); window.Document.Save(textProject);
            var textRead = new BoardDocument(); textRead.Load(textProject);
            Check(textRead.Items.Last().Text == textItem.Text && Near(textRead.Items.Last().FontSize, textItem.FontSize) && Near(textRead.Items.Last().X, textItem.X),
                "Mixed projects preserve text, font size and position through save/load");
            var textOnly = new BoardDocument(); textOnly.AddText("Only text\nSecond line", 36, new Point(10, 20));
            textOnly.Save(Path.Combine(folder, "text-only.arkboard"));
            var textOnlyRead = new BoardDocument(); textOnlyRead.Load(textOnly.Path);
            Check(textOnlyRead.Assets.Count == 0 && textOnlyRead.Items.Single().IsText, "Text-only projects load without image assets");
            window.Width = 1280; window.Height = 820; window.Document.Selected.Clear(); window.Document.Notify();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.Board.Fit(false); Capture(window, Path.Combine(folder, "ui-text.png"));
            int beforeClipboardPaste = window.Document.Items.Count;
            ImageItem pastedMask = window.PasteClipboardImage(clipboardItem, clipboardAsset, new Point(50, 75));
            Check(window.Document.Items.Count == beforeClipboardPaste + 1 && pastedMask.HasMask && Near(pastedMask.MaskLeft, .3) &&
                Near(pastedMask.X, 50) && Near(pastedMask.Y, 75),
                "Pasting ArkBoard clipboard data creates a new image while preserving its mask");
            window.Document.Reset();
            ImageItem uiPsd = window.Document.Add(psd, "Layers.psd", new Point(320, 260));
            window.Document.Selected.Add(uiPsd.Id); window.Document.Notify(); window.Board.Fit(false);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(window.layersExpander.Visibility == Visibility.Visible && window.layersList.Children.Count == 2,
                "Selecting one PSD exposes its layer visibility list in the inspector");
            Check((string)((CheckBox)window.layersList.Children[0]).Content == psd.Psd.Layers[psd.Psd.Layers.Count - 1].Name,
                "PSD layers are listed in the same visual order as Photoshop");
            uiPsd.Width = 420; uiPsd.Height = 420; window.Document.Notify(); window.Board.Fit(false);
            Capture(window, Path.Combine(folder, "ui-psd-layers.png"));
            window.SetPsdLayerVisibility(uiPsd.Id, 1, false);
            Check(!uiPsd.LayerVisibility[1] && Pixel(psd.BitmapFor(uiPsd), 0, 0).B > 240,
                "The inspector layer toggle updates the selected PSD instance");
            uiPsd.Width /= 2; uiPsd.Height /= 2; uiPsd.Rotation = 61; uiPsd.MaskLeft = .2;
            window.ResetSize(); Check(Near(uiPsd.Width, 2) && Near(uiPsd.Height, 2), "Reset scale restores 100 percent dimensions for Alt+S");
            window.ResetRotation(); Check(Near(uiPsd.Rotation, 0), "Reset rotation restores zero degrees for Alt+R");
            window.RemoveMask(); Check(!uiPsd.HasMask, "Reset mask restores the full image for Alt+M");
            window.Document.Zoom = 2; window.Document.PanX = 140; window.Document.PanY = 80;
            Check(window.Board.FitOnEmptyDoubleClick(new Point(window.Board.ActualWidth - 8, window.Board.ActualHeight - 8), 2) && !Near(window.Document.Zoom, 2),
                "Double-clicking empty canvas fits the entire board");
            window.Close();
            File.WriteAllLines(Path.Combine(folder, "results.txt"), checks.Concat(new[] { "", checks.Count + " checks passed." }));
        }
    }
}
