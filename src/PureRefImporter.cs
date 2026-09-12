using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace ArkBoard
{
    // Read-only migration support for the reverse-engineered PureRef 1.10/1.11 binary format.
    // Format structure verified against FyorDev/PureRef-format (MIT): https://github.com/FyorDev/PureRef-format
    // PureRef 2 uses a different, undocumented format and is deliberately not accepted here.
    internal sealed class PureRefImportResult
    {
        internal readonly List<ImageItem> Items = new List<ImageItem>();
        internal readonly Dictionary<string, AssetData> Assets = new Dictionary<string, AssetData>(StringComparer.Ordinal);
        internal int SkippedItems;
    }

    internal static class PureRefImporter
    {
        const int HeaderSize = 224, MaxItems = 10000;
        const long MaxFileBytes = 1024L * 1024 * 1024;
        const uint ImageItem = 34, TextItem = 32;
        static readonly byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };
        static readonly byte[] PngEnd = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };

        sealed class PendingImage { internal uint Id; internal long Z; internal double X, Y, ScaleX, ScaleY, Rotation; internal bool FlipX; internal string Name; }
        sealed class EmbeddedImage { internal ulong Start; internal byte[] Bytes; }

        internal static bool IsPureRefFile(string path) { return path != null && path.EndsWith(".pur", StringComparison.OrdinalIgnoreCase); }
        internal static PureRefImportResult Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("PureRef project was not found.", path);
            var info = new FileInfo(path);
            if (info.Length < HeaderSize || info.Length > MaxFileBytes) throw new InvalidDataException("PureRef project exceeds the supported size limit.");
            return Read(File.ReadAllBytes(path));
        }

        static PureRefImportResult Read(byte[] data)
        {
            var r = new Reader(data);
            if (r.U32At(0) != 8 || r.U64At(16) > (ulong)data.Length) throw new InvalidDataException("This file is not a supported PureRef project.");
            if (Encoding.BigEndianUnicode.GetString(data, 4, 8) != "1.10") throw new InvalidDataException("Only PureRef legacy 1.10/1.11 projects are supported. PureRef 2 projects cannot yet be imported.");
            r.Position = HeaderSize;
            var images = ReadImages(r); var pending = new Dictionary<uint, PendingImage>(); var text = new List<Tuple<long, ImageItem>>();
            while (r.Remaining >= 12 && (r.U32At(r.Position + 8) == ImageItem || r.U32At(r.Position + 8) == TextItem))
            {
                if (pending.Count + text.Count >= MaxItems) throw new InvalidDataException("PureRef project contains too many items.");
                ulong end = r.U64(); uint type = r.U32(); if (end <= (ulong)r.Position || end > (ulong)data.Length) throw new InvalidDataException("PureRef item has an invalid length.");
                try { if (type == ImageItem) { PendingImage image = ReadImageItem(r); pending.Add(image.Id, image); } else text.Add(ReadTextItem(r)); r.Position = checked((int)end); }
                catch { r.Position = checked((int)end); }
            }
            r.String();
            var byAddress = new Dictionary<ulong, EmbeddedImage>(); foreach (EmbeddedImage image in images) byAddress[image.Start] = image;
            var result = new PureRefImportResult(); var ordered = new List<Tuple<long, ImageItem>>(); ordered.AddRange(text);
            while (r.Remaining >= 20)
            {
                uint id = r.U32(); ulong start = r.U64(); r.U64(); PendingImage item; EmbeddedImage image;
                if (!pending.TryGetValue(id, out item) || !byAddress.TryGetValue(start, out image)) { result.SkippedItems++; continue; }
                try
                {
                    AssetData asset = AssetData.Create(image.Bytes); result.Assets[asset.Key] = asset;
                    double width = asset.Bitmap.PixelWidth * item.ScaleX, height = asset.Bitmap.PixelHeight * item.ScaleY;
                    if (!Finite(width) || !Finite(height) || width < .01 || height < .01 || width > 1000000 || height > 1000000) throw new InvalidDataException();
                    ordered.Add(Tuple.Create(item.Z, new ImageItem { Asset = asset.Key, Name = item.Name, X = item.X, Y = item.Y, Width = width, Height = height, Rotation = item.Rotation, FlipX = item.FlipX }));
                }
                catch { result.SkippedItems++; }
            }
            ordered.Sort((a, b) => a.Item1.CompareTo(b.Item1)); foreach (var entry in ordered) result.Items.Add(entry.Item2);
            if (result.Items.Count == 0) throw new InvalidDataException("PureRef project did not contain readable embedded images or text.");
            return result;
        }

        static List<EmbeddedImage> ReadImages(Reader r)
        {
            var result = new List<EmbeddedImage>();
            while (r.Remaining >= 12 && r.U32At(r.Position + 8) != ImageItem && r.U32At(r.Position + 8) != TextItem)
                if (StartsWith(r.Data, r.Position, PngHeader)) { int end = Find(r.Data, PngEnd, r.Position + PngHeader.Length); if (end < 0) throw new InvalidDataException("PureRef image data is incomplete."); end += PngEnd.Length; byte[] bytes = new byte[end - r.Position]; Buffer.BlockCopy(r.Data, r.Position, bytes, 0, bytes.Length); result.Add(new EmbeddedImage { Start = (ulong)r.Position, Bytes = bytes }); r.Position = end; }
                else r.Skip(4); // duplicate/external-image marker
            return result;
        }

        static PendingImage ReadImageItem(Reader r)
        {
            r.Skip(checked((int)r.U32At(r.Position - 4))); bool bruteForce = r.U32At(r.Position) == 0; if (bruteForce) r.Skip(4);
            ReadNullableString(r); string name = bruteForce ? "PureRef image" : ReadNullableString(r); r.Skip(8);
            double m11 = r.Double(), m12 = r.Double(); r.Skip(8); double m21 = r.Double(), m22 = r.Double(); r.Skip(8); double x = r.Double(), y = r.Double(); r.Skip(8); uint id = r.U32(); double z = r.Double();
            double sx = Math.Sqrt(m11 * m11 + m12 * m12), sy = Math.Sqrt(m21 * m21 + m22 * m22);
            if (!Finite(x) || !Finite(y) || !Finite(sx) || !Finite(sy) || sx <= 0 || sy <= 0) throw new InvalidDataException("PureRef image has invalid geometry.");
            return new PendingImage { Id = id, Z = checked((long)Math.Round(z * 1000)), X = x, Y = y, ScaleX = sx, ScaleY = sy, Rotation = Normalize(Math.Atan2(m12, m11) * 180 / Math.PI), FlipX = m11 * m22 - m12 * m21 < 0, Name = CleanName(name) };
        }

        static Tuple<long, ImageItem> ReadTextItem(Reader r)
        {
            r.Skip(checked((int)r.U32At(r.Position - 4))); string value = r.String(); double m11 = r.Double(), m12 = r.Double(); r.Skip(8); double m21 = r.Double(), m22 = r.Double(); r.Skip(8); double x = r.Double(), y = r.Double(); r.Skip(8); r.U32(); double z = r.Double();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 10000) throw new InvalidDataException("PureRef text is invalid.");
            double sx = Math.Sqrt(m11 * m11 + m12 * m12), sy = Math.Sqrt(m21 * m21 + m22 * m22); Size natural = TextLayout.Measure(value, 12);
            // QGraphicsTextItem positions its local top-left at (x,y); ArkBoard positions items by center.
            // Apply the complete PureRef affine basis to that local center, including rotation and flipping.
            double centerX = x + m11 * natural.Width / 2 + m21 * natural.Height / 2;
            double centerY = y + m12 * natural.Width / 2 + m22 * natural.Height / 2;
            if (!Finite(centerX) || !Finite(centerY)) throw new InvalidDataException("PureRef text has invalid geometry.");
            var item = new ImageItem { Text = value, Name = "Text", FontSize = 12, X = centerX, Y = centerY, Width = natural.Width * sx, Height = natural.Height * sy, Rotation = Normalize(Math.Atan2(m12, m11) * 180 / Math.PI), FlipX = m11 * m22 - m12 * m21 < 0 };
            return Tuple.Create(checked((long)Math.Round(z * 1000)), item);
        }

        static string ReadNullableString(Reader r) { if (r.U32At(r.Position) == 0xffffffff) { r.Skip(4); return null; } return r.String(); }
        static string CleanName(string value) { if (string.IsNullOrWhiteSpace(value)) return "PureRef image"; value = value.Replace('\\', '/'); int p = value.LastIndexOf('/'); return p >= 0 ? value.Substring(p + 1) : value; }
        static int Find(byte[] data, byte[] value, int start) { for (int i = start; i <= data.Length - value.Length; i++) { int j = 0; while (j < value.Length && data[i + j] == value[j]) j++; if (j == value.Length) return i; } return -1; }
        static bool StartsWith(byte[] data, int start, byte[] value) { if (start + value.Length > data.Length) return false; for (int i = 0; i < value.Length; i++) if (data[start + i] != value[i]) return false; return true; }
        static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) <= 100000000; }
        static double Normalize(double v) { return ((v % 360) + 360) % 360; }

        sealed class Reader
        {
            internal readonly byte[] Data; internal int Position; internal Reader(byte[] data) { Data = data; } internal int Remaining { get { return Data.Length - Position; } }
            internal void Need(int count) { if (count < 0 || Position < 0 || count > Remaining) throw new InvalidDataException("PureRef project is truncated."); }
            internal void Skip(int count) { Need(count); Position += count; } internal uint U32() { uint value = U32At(Position); Position += 4; return value; }
            internal uint U32At(int pos) { if (pos < 0 || pos > Data.Length - 4) throw new InvalidDataException("PureRef project is truncated."); return (uint)(Data[pos] << 24 | Data[pos + 1] << 16 | Data[pos + 2] << 8 | Data[pos + 3]); }
            internal ulong U64() { ulong value = U64At(Position); Position += 8; return value; } internal ulong U64At(int pos) { if (pos < 0 || pos > Data.Length - 8) throw new InvalidDataException("PureRef project is truncated."); ulong v = 0; for (int i = 0; i < 8; i++) v = (v << 8) | Data[pos + i]; return v; }
            internal double Double() { return BitConverter.Int64BitsToDouble(unchecked((long)U64())); }
            internal string String() { int length = checked((int)U32()); if ((length & 1) != 0) throw new InvalidDataException("PureRef string is invalid."); Need(length); string value = Encoding.BigEndianUnicode.GetString(Data, Position, length); Position += length; return value; }
        }
    }
}
