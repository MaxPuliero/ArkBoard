using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    [DataContract]
    public sealed class ImageItem
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Asset;
        [DataMember] public string Name;
        [DataMember] public double X;
        [DataMember] public double Y;
        [DataMember] public double Width;
        [DataMember] public double Height;
        [DataMember] public double Rotation;
        [DataMember] public bool FlipX;
        [DataMember] public bool FlipY;
        [DataMember(EmitDefaultValue = false)] public string Text;
        [DataMember(EmitDefaultValue = false)] public double FontSize;
        [DataMember(EmitDefaultValue = false)] public double MaskLeft;
        [DataMember(EmitDefaultValue = false)] public double MaskTop;
        [DataMember(EmitDefaultValue = false)] public double MaskRight;
        [DataMember(EmitDefaultValue = false)] public double MaskBottom;
        [DataMember(EmitDefaultValue = false)] public List<bool> LayerVisibility;
        public bool IsText { get { return Text != null; } }
        public bool HasMask { get { return !IsText && (MaskLeft > 0 || MaskTop > 0 || MaskRight > 0 || MaskBottom > 0); } }
        public Rect VisibleRect
        {
            get
            {
                return new Rect(-Width / 2 + Width * MaskLeft, -Height / 2 + Height * MaskTop,
                    Width * (1 - MaskLeft - MaskRight), Height * (1 - MaskTop - MaskBottom));
            }
        }
        public ImageItem Copy()
        {
            ImageItem copy = (ImageItem)MemberwiseClone();
            if (LayerVisibility != null) copy.LayerVisibility = new List<bool>(LayerVisibility);
            return copy;
        }
        public Matrix Matrix
        {
            get
            {
                Matrix m = Matrix.Identity;
                m.Scale(FlipX ? -1 : 1, FlipY ? -1 : 1);
                m.Rotate(Rotation);
                m.Translate(X, Y);
                return m;
            }
        }
        public Point[] Corners()
        {
            Matrix m = Matrix;
            return new[] { m.Transform(new Point(-Width / 2, -Height / 2)),
                m.Transform(new Point(Width / 2, -Height / 2)),
                m.Transform(new Point(Width / 2, Height / 2)),
                m.Transform(new Point(-Width / 2, Height / 2)) };
        }
        public Rect Bounds()
        {
            Point[] p = Corners();
            return new Rect(new Point(p.Min(v => v.X), p.Min(v => v.Y)),
                new Point(p.Max(v => v.X), p.Max(v => v.Y)));
        }
        public bool Contains(Point world)
        {
            Matrix m = Matrix; m.Invert();
            Point p = m.Transform(world);
            return Math.Abs(p.X) <= Width / 2 && Math.Abs(p.Y) <= Height / 2;
        }
        public bool ContainsVisible(Point world)
        {
            Matrix m = Matrix; m.Invert();
            return VisibleRect.Contains(m.Transform(world));
        }
    }

    [DataContract]
    public sealed class Manifest
    {
        [DataMember] public string Format = "ArkBoard";
        [DataMember] public int Version = 1;
        [DataMember] public List<ImageItem> Images = new List<ImageItem>();
        [DataMember] public double Zoom = 1;
        [DataMember] public double PanX = 0;
        [DataMember] public double PanY = 0;
    }

    public sealed class AssetData
    {
        public const long MaxBytes = 100L * 1024 * 1024;
        public string Key;
        public byte[] Bytes;
        public BitmapSource Bitmap;
        public PsdDocument Psd;
        readonly Dictionary<string, BitmapSource> psdComposites = new Dictionary<string, BitmapSource>();
        public bool IsPsd { get { return Psd != null; } }
        public static AssetData Create(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.LongLength > MaxBytes)
                throw new InvalidDataException("Image is empty or exceeds 100 MB.");
            BitmapSource frame; PsdDocument psd = null;
            if (bytes.Length >= 4 && bytes[0] == 56 && bytes[1] == 66 && bytes[2] == 80 && bytes[3] == 83)
            {
                psd = PsdDocument.Read(bytes); frame = psd.Compose(null);
            }
            else using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
                if ((long)frame.PixelWidth * frame.PixelHeight > 80000000)
                    throw new InvalidDataException("Image exceeds the 80 megapixel limit.");
                frame.Freeze();
            }
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            return new AssetData { Key = hash + Extension(bytes), Bytes = bytes, Bitmap = frame, Psd = psd };
        }
        public BitmapSource BitmapFor(ImageItem item)
        {
            if (Psd == null || item == null || item.LayerVisibility == null || item.LayerVisibility.Count != Psd.Layers.Count) return Bitmap;
            string key = string.Concat(item.LayerVisibility.Select(v => v ? '1' : '0'));
            BitmapSource result;
            if (!psdComposites.TryGetValue(key, out result))
            {
                result = Psd.Compose(item.LayerVisibility);
                if (psdComposites.Count >= 8) psdComposites.Clear();
                psdComposites[key] = result;
            }
            return result;
        }
        static string Extension(byte[] b)
        {
            if (b.Length > 12 && b[0] == 137 && b[1] == 80) return ".png";
            if (b.Length > 2 && b[0] == 255 && b[1] == 216) return ".jpg";
            if (b.Length > 3 && b[0] == 71 && b[1] == 73 && b[2] == 70) return ".gif";
            if (b.Length > 2 && b[0] == 66 && b[1] == 77) return ".bmp";
            if (b.Length > 12 && b[8] == 87 && b[9] == 69 && b[10] == 66 && b[11] == 80) return ".webp";
            if (b.Length > 4 && ((b[0] == 73 && b[1] == 73) || (b[0] == 77 && b[1] == 77))) return ".tiff";
            if (b.Length > 4 && b[0] == 0 && b[1] == 0 && b[2] == 1 && b[3] == 0) return ".ico";
            if (b.Length > 4 && b[0] == 56 && b[1] == 66 && b[2] == 80 && b[3] == 83) return ".psd";
            return ".image";
        }
        public static AssetData FromBitmap(BitmapSource source)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream stream = new MemoryStream()) { encoder.Save(stream); return Create(stream.ToArray()); }
        }
    }

    public sealed class BoardDocument
    {
        public List<ImageItem> Items = new List<ImageItem>();
        public Dictionary<string, AssetData> Assets = new Dictionary<string, AssetData>(StringComparer.Ordinal);
        public HashSet<string> Selected = new HashSet<string>();
        public double Zoom = 1, PanX = 0, PanY = 0;
        public string Path;
        public bool Dirty;
        public event Action Changed;
        readonly List<List<ImageItem>> undo = new List<List<ImageItem>>();
        readonly List<List<ImageItem>> redo = new List<List<ImageItem>>();
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public IEnumerable<ImageItem> Selection { get { return Items.Where(i => Selected.Contains(i.Id)); } }
        public void Notify() { if (Changed != null) Changed(); }
        static List<ImageItem> Clone(List<ImageItem> list) { return list.Select(i => i.Copy()).ToList(); }
        public void Checkpoint()
        {
            undo.Add(Clone(Items));
            if (undo.Count > 40) undo.RemoveAt(0);
            redo.Clear(); Dirty = true;
        }
        public void Change(Action action) { Checkpoint(); action(); CollectAssets(); Notify(); }
        public void Undo()
        {
            if (!CanUndo) return;
            redo.Add(Clone(Items)); Items = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            Selected.IntersectWith(Items.Select(i => i.Id)); Dirty = true; CollectAssets(); Notify();
        }
        public void Redo()
        {
            if (!CanRedo) return;
            undo.Add(Clone(Items)); Items = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            Selected.IntersectWith(Items.Select(i => i.Id)); Dirty = true; CollectAssets(); Notify();
        }
        public void CollectAssets()
        {
            HashSet<string> used = new HashSet<string>(Items.Select(i => i.Asset));
            foreach (var state in undo.Concat(redo)) foreach (var i in state) used.Add(i.Asset);
            foreach (string key in Assets.Keys.Where(k => !used.Contains(k)).ToArray()) Assets.Remove(key);
        }
        public void Reset()
        {
            Items.Clear(); Assets.Clear(); Selected.Clear(); undo.Clear(); redo.Clear();
            Zoom = 1; PanX = PanY = 0; Path = null; Dirty = false; Notify();
        }
        public Rect Bounds(bool selectedOnly)
        {
            Rect r = Rect.Empty;
            foreach (ImageItem i in selectedOnly ? Selection : Items) r.Union(i.Bounds());
            return r;
        }
        public ImageItem Add(AssetData asset, string name, Point center)
        {
            Assets[asset.Key] = asset;
            double size = Math.Min(1, 520.0 / Math.Max(asset.Bitmap.PixelWidth, asset.Bitmap.PixelHeight));
            ImageItem item = new ImageItem { Asset = asset.Key, Name = name, X = center.X, Y = center.Y,
                Width = asset.Bitmap.PixelWidth * size, Height = asset.Bitmap.PixelHeight * size };
            if (asset.Psd != null) item.LayerVisibility = asset.Psd.Layers.Select(l => l.DefaultVisible).ToList();
            Items.Add(item); return item;
        }
        public ImageItem AddText(string text, double fontSize, Point topLeft)
        {
            Size size = TextLayout.Measure(text, fontSize);
            double scale = Math.Min(1, 1000000 / Math.Max(size.Width, size.Height));
            var item = new ImageItem { Text = text, FontSize = fontSize, Name = "Text",
                Width = size.Width * scale, Height = size.Height * scale };
            item.X = topLeft.X + item.Width / 2; item.Y = topLeft.Y + item.Height / 2;
            Items.Add(item); return item;
        }
        public void Save(string path)
        {
            string full = System.IO.Path.GetFullPath(path);
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    var manifest = new Manifest { Version = Items.Any(i => i.LayerVisibility != null) ? 4 : Items.Any(i => i.HasMask) ? 3 : Items.Any(i => i.IsText) ? 2 : 1,
                        Images = Clone(Items), Zoom = Zoom, PanX = PanX, PanY = PanY };
                    using (Stream entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal).Open())
                        new DataContractJsonSerializer(typeof(Manifest)).WriteObject(entry, manifest);
                    foreach (string key in Items.Where(i => !i.IsText).Select(i => i.Asset).Distinct())
                    {
                        using (Stream entry = zip.CreateEntry("assets/" + key, CompressionLevel.Optimal).Open())
                        { byte[] b = Assets[key].Bytes; entry.Write(b, 0, b.Length); }
                    }
                }
                if (File.Exists(full)) File.Replace(temp, full, null);
                else File.Move(temp, full);
                Path = full; Dirty = false; Notify();
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public void Load(string path)
        {
            // Parse and validate separately: a damaged file must never replace the open board.
            Manifest manifest;
            var assets = new Dictionary<string, AssetData>(StringComparer.Ordinal);
            using (FileStream file = File.OpenRead(path))
            using (ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                ZipArchiveEntry me = zip.GetEntry("manifest.json");
                if (me == null || me.Length > 8 * 1024 * 1024) throw new InvalidDataException("Project manifest is missing or too large.");
                using (Stream stream = me.Open()) manifest = (Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(stream);
                if (manifest == null || (manifest.Format != "ArkBoard" && manifest.Format != "RefCanvas") || manifest.Version < 1 || manifest.Version > 4 || manifest.Images == null)
                    throw new InvalidDataException("Unsupported project format or version.");
                if (manifest.Images.Count > 10000) throw new InvalidDataException("Project contains too many images.");
                var ids = new HashSet<string>();
                long total = 0;
                foreach (ImageItem i in manifest.Images)
                {
                    if (i == null || string.IsNullOrEmpty(i.Id) || !ids.Add(i.Id) || !Finite(i.X) || !Finite(i.Y) ||
                        !Finite(i.Width) || !Finite(i.Height) || !Finite(i.Rotation) || i.Width < 0.01 || i.Height < 0.01 ||
                        i.Width > 1000000 || i.Height > 1000000 || Math.Abs(i.X) > 100000000 || Math.Abs(i.Y) > 100000000)
                        throw new InvalidDataException("Invalid image data.");
                    if (!Finite(i.MaskLeft) || !Finite(i.MaskTop) || !Finite(i.MaskRight) || !Finite(i.MaskBottom))
                        throw new InvalidDataException("Invalid image mask.");
                    if (i.IsText)
                    {
                        if (manifest.Version < 2 || i.Text.Length > 10000 || string.IsNullOrWhiteSpace(i.Text) ||
                            !Finite(i.FontSize) || i.FontSize < 1 || i.FontSize > 8192 ||
                            i.MaskLeft != 0 || i.MaskTop != 0 || i.MaskRight != 0 || i.MaskBottom != 0)
                            throw new InvalidDataException("Invalid text object.");
                        continue;
                    }
                    if (i.MaskLeft < 0 || i.MaskTop < 0 || i.MaskRight < 0 || i.MaskBottom < 0 ||
                        i.MaskLeft + i.MaskRight >= .999 || i.MaskTop + i.MaskBottom >= .999 ||
                        (i.HasMask && manifest.Version < 3))
                        throw new InvalidDataException("Invalid image mask.");
                    if (string.IsNullOrEmpty(i.Asset) || i.Asset.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                        throw new InvalidDataException("Invalid image asset.");
                    if (!assets.ContainsKey(i.Asset))
                    {
                        ZipArchiveEntry ae = zip.GetEntry("assets/" + i.Asset);
                        if (ae == null || ae.Length > AssetData.MaxBytes || (total += ae.Length) > 1024L * 1024 * 1024)
                            throw new InvalidDataException("Missing image asset or project exceeds supported limits.");
                        using (Stream s = ae.Open())
                        {
                            AssetData asset = AssetData.Create(ReadLimited(s, AssetData.MaxBytes));
                            if (asset.Key != i.Asset) throw new InvalidDataException("Image asset integrity check failed.");
                            assets.Add(i.Asset, asset);
                        }
                    }
                    AssetData itemAsset = assets[i.Asset];
                    if (i.LayerVisibility != null && (manifest.Version < 4 || itemAsset.Psd == null || i.LayerVisibility.Count != itemAsset.Psd.Layers.Count))
                        throw new InvalidDataException("Invalid PSD layer visibility data.");
                    if (itemAsset.Psd != null && i.LayerVisibility == null)
                        i.LayerVisibility = itemAsset.Psd.Layers.Select(l => l.DefaultVisible).ToList();
                }
            }
            Items = manifest.Images; Assets = assets; Selected.Clear(); undo.Clear(); redo.Clear();
            Zoom = Finite(manifest.Zoom) ? Math.Max(.01, Math.Min(16, manifest.Zoom)) : 1;
            PanX = Finite(manifest.PanX) && Math.Abs(manifest.PanX) < 1e9 ? manifest.PanX : 0;
            PanY = Finite(manifest.PanY) && Math.Abs(manifest.PanY) < 1e9 ? manifest.PanY : 0;
            Path = System.IO.Path.GetFullPath(path); Dirty = false; Notify();
        }
        public static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public static bool ValidMask(ImageItem i)
        {
            return i != null && Finite(i.MaskLeft) && Finite(i.MaskTop) && Finite(i.MaskRight) && Finite(i.MaskBottom) &&
                i.MaskLeft >= 0 && i.MaskTop >= 0 && i.MaskRight >= 0 && i.MaskBottom >= 0 &&
                i.MaskLeft + i.MaskRight < .999 && i.MaskTop + i.MaskBottom < .999;
        }
        public static byte[] ReadLimited(Stream stream, long limit)
        {
            using (MemoryStream result = new MemoryStream())
            {
                byte[] buffer = new byte[81920]; int n;
                while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
                { if (result.Length + n > limit) throw new InvalidDataException("Image asset is too large."); result.Write(buffer, 0, n); }
                return result.ToArray();
            }
        }
    }
}
