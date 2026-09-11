using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    public sealed class PsdLayer
    {
        public string Name;
        public bool DefaultVisible;
        public byte Opacity;
        public int Left, Top, Width, Height;
        public BitmapSource Bitmap;
    }

    public sealed class PsdDocument
    {
        public int Width, Height;
        public List<PsdLayer> Layers = new List<PsdLayer>();

        public static PsdDocument Read(byte[] bytes)
        {
            var reader = new BigEndianReader(bytes);
            if (reader.Ascii(4) != "8BPS") throw new InvalidDataException("Invalid Photoshop document signature.");
            if (reader.UInt16() != 1) throw new InvalidDataException("PSB files are not supported. Save as a standard PSD.");
            reader.Skip(6);
            int channelCount = reader.UInt16();
            int height = reader.Int32(), width = reader.Int32();
            int depth = reader.UInt16(), colorMode = reader.UInt16();
            if (channelCount < 1 || channelCount > 56 || width < 1 || height < 1 || (long)width * height > 80000000)
                throw new InvalidDataException("PSD dimensions or channel count are unsupported.");
            if (depth != 8 || colorMode != 3)
                throw new InvalidDataException("Basic PSD support requires an 8-bit RGB document.");
            reader.SkipSection32(); // Color mode data.
            reader.SkipSection32(); // Image resources.
            long layerMaskEnd = reader.SectionEnd32();
            if (layerMaskEnd == reader.Position) throw new InvalidDataException("The PSD does not contain layer data.");
            long layerInfoEnd = reader.SectionEnd32();
            if (layerInfoEnd == reader.Position) throw new InvalidDataException("The PSD does not contain readable layers.");
            int signedCount = reader.Int16();
            int count = Math.Abs(signedCount);
            if (count < 1 || count > 512) throw new InvalidDataException("PSD layer count is unsupported.");
            var records = new List<LayerRecord>(count);
            for (int index = 0; index < count; index++) records.Add(ReadRecord(reader, index));
            long totalPixels = 0;
            foreach (LayerRecord record in records)
            {
                foreach (ChannelRecord channel in record.Channels)
                {
                    long end = checked(reader.Position + channel.Length);
                    if (end > layerInfoEnd || channel.Length < 2) throw new InvalidDataException("PSD layer channel is truncated.");
                    int compression = reader.UInt16();
                    if (record.Width > 0 && record.Height > 0 && (channel.Id == -1 || channel.Id == 0 || channel.Id == 1 || channel.Id == 2))
                        record.Planes[channel.Id] = ReadPlane(reader, compression, record.Width, record.Height, end);
                    reader.Position = end;
                }
                if (record.Width > 0 && record.Height > 0 && record.Planes.ContainsKey(0) && record.Planes.ContainsKey(1) && record.Planes.ContainsKey(2))
                {
                    totalPixels += (long)record.Width * record.Height;
                    if (totalPixels > 80000000) throw new InvalidDataException("PSD raster layers exceed the 80 megapixel decoded limit.");
                }
            }
            reader.Position = layerInfoEnd;
            reader.Position = layerMaskEnd;
            var result = new PsdDocument { Width = width, Height = height };
            foreach (LayerRecord record in records)
            {
                if (record.Width <= 0 || record.Height <= 0 || !record.Planes.ContainsKey(0) || !record.Planes.ContainsKey(1) || !record.Planes.ContainsKey(2)) continue;
                byte[] pixels = new byte[checked(record.Width * record.Height * 4)];
                byte[] red = record.Planes[0], green = record.Planes[1], blue = record.Planes[2];
                byte[] alpha = record.Planes.ContainsKey(-1) ? record.Planes[-1] : null;
                for (int pixel = 0, output = 0; pixel < red.Length; pixel++, output += 4)
                {
                    pixels[output] = blue[pixel]; pixels[output + 1] = green[pixel]; pixels[output + 2] = red[pixel];
                    pixels[output + 3] = alpha == null ? (byte)255 : alpha[pixel];
                }
                BitmapSource bitmap = BitmapSource.Create(record.Width, record.Height, 96, 96, PixelFormats.Bgra32, null, pixels, record.Width * 4);
                bitmap.Freeze();
                result.Layers.Add(new PsdLayer { Name = string.IsNullOrWhiteSpace(record.Name) ? "Layer " + (record.Index + 1) : record.Name,
                    DefaultVisible = (record.Flags & 2) == 0, Opacity = record.Opacity, Left = record.Left, Top = record.Top,
                    Width = record.Width, Height = record.Height, Bitmap = bitmap });
            }
            if (result.Layers.Count == 0) throw new InvalidDataException("The PSD does not contain supported raster layers.");
            return result;
        }

        public BitmapSource Compose(IList<bool> visibility)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));
                // PSD layer records are stored from the bottom of the stack to the top.
                // Draw in that order so every later (higher) layer composites over it.
                for (int index = 0; index < Layers.Count; index++)
                {
                    PsdLayer layer = Layers[index];
                    bool visible = visibility == null || visibility.Count != Layers.Count ? layer.DefaultVisible : visibility[index];
                    if (!visible || layer.Opacity == 0) continue;
                    dc.PushOpacity(layer.Opacity / 255.0);
                    dc.DrawImage(layer.Bitmap, new Rect(layer.Left, layer.Top, layer.Width, layer.Height));
                    dc.Pop();
                }
            }
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }

        static LayerRecord ReadRecord(BigEndianReader reader, int index)
        {
            int top = reader.Int32(), left = reader.Int32(), bottom = reader.Int32(), right = reader.Int32();
            if (right < left || bottom < top || right - (long)left > 100000 || bottom - (long)top > 100000)
                throw new InvalidDataException("PSD layer bounds are invalid.");
            var record = new LayerRecord { Index = index, Top = top, Left = left, Width = right - left, Height = bottom - top };
            int channels = reader.UInt16();
            if (channels < 0 || channels > 64) throw new InvalidDataException("PSD layer channel count is unsupported.");
            for (int c = 0; c < channels; c++) record.Channels.Add(new ChannelRecord { Id = reader.Int16(), Length = reader.UInt32() });
            if (reader.Ascii(4) != "8BIM") throw new InvalidDataException("PSD layer blend signature is invalid.");
            reader.Skip(4); record.Opacity = reader.Byte(); reader.Skip(1); record.Flags = reader.Byte(); reader.Skip(1);
            long extraEnd = reader.SectionEnd32();
            reader.SkipSection32(); // Layer mask data.
            reader.SkipSection32(); // Blending ranges.
            int nameLength = reader.Byte();
            record.Name = Encoding.ASCII.GetString(reader.Bytes(nameLength));
            int namePadding = (4 - ((nameLength + 1) % 4)) % 4; reader.Skip(namePadding);
            while (reader.Position + 12 <= extraEnd)
            {
                string signature = reader.Ascii(4), key = reader.Ascii(4);
                uint length = reader.UInt32(); long dataEnd = checked(reader.Position + length);
                if ((signature != "8BIM" && signature != "8B64") || dataEnd > extraEnd) break;
                if (key == "luni" && length >= 4)
                {
                    int chars = reader.Int32();
                    if (chars >= 0 && chars <= 4096 && reader.Position + chars * 2L <= dataEnd)
                    {
                        byte[] unicode = reader.Bytes(chars * 2);
                        for (int a = 0; a + 1 < unicode.Length; a += 2) { byte swap = unicode[a]; unicode[a] = unicode[a + 1]; unicode[a + 1] = swap; }
                        record.Name = Encoding.Unicode.GetString(unicode).TrimEnd('\0');
                    }
                }
                reader.Position = dataEnd + (length & 1);
            }
            reader.Position = extraEnd;
            return record;
        }

        static byte[] ReadPlane(BigEndianReader reader, int compression, int width, int height, long channelEnd)
        {
            int pixels = checked(width * height); byte[] output = new byte[pixels];
            if (compression == 0)
            {
                if (reader.Position + pixels > channelEnd) throw new InvalidDataException("PSD raw layer channel is truncated.");
                return reader.Bytes(pixels);
            }
            if (compression != 1) throw new InvalidDataException("This PSD uses ZIP-compressed layer pixels. Save it with RLE compatibility enabled.");
            int[] rowLengths = new int[height];
            for (int row = 0; row < height; row++) rowLengths[row] = reader.UInt16();
            for (int row = 0; row < height; row++)
            {
                long rowEnd = reader.Position + rowLengths[row]; int target = row * width, written = 0;
                if (rowEnd > channelEnd) throw new InvalidDataException("PSD RLE row is truncated.");
                while (reader.Position < rowEnd && written < width)
                {
                    int control = reader.Byte();
                    if (control <= 127)
                    {
                        int count = control + 1;
                        if (written + count > width || reader.Position + count > rowEnd) throw new InvalidDataException("PSD RLE literal exceeds its row.");
                        byte[] literal = reader.Bytes(count); Buffer.BlockCopy(literal, 0, output, target + written, count); written += count;
                    }
                    else if (control >= 129)
                    {
                        int count = 257 - control;
                        if (written + count > width || reader.Position >= rowEnd) throw new InvalidDataException("PSD RLE repeat exceeds its row.");
                        byte value = reader.Byte(); for (int k = 0; k < count; k++) output[target + written++] = value;
                    }
                }
                if (written != width) throw new InvalidDataException("PSD RLE row has the wrong width.");
                reader.Position = rowEnd;
            }
            return output;
        }

        sealed class LayerRecord
        {
            public int Index, Left, Top, Width, Height;
            public byte Opacity, Flags;
            public string Name;
            public List<ChannelRecord> Channels = new List<ChannelRecord>();
            public Dictionary<short, byte[]> Planes = new Dictionary<short, byte[]>();
        }
        sealed class ChannelRecord { public short Id; public uint Length; }

        sealed class BigEndianReader
        {
            readonly byte[] data; long position;
            public BigEndianReader(byte[] bytes) { data = bytes ?? new byte[0]; }
            public long Position { get { return position; } set { if (value < 0 || value > data.LongLength) throw new InvalidDataException("PSD section is truncated."); position = value; } }
            public byte Byte() { Need(1); return data[position++]; }
            public byte[] Bytes(int count) { Need(count); byte[] result = new byte[count]; Buffer.BlockCopy(data, (int)position, result, 0, count); position += count; return result; }
            public string Ascii(int count) { return Encoding.ASCII.GetString(Bytes(count)); }
            public short Int16() { return unchecked((short)UInt16()); }
            public ushort UInt16() { Need(2); ushort value = (ushort)((data[position] << 8) | data[position + 1]); position += 2; return value; }
            public int Int32() { return unchecked((int)UInt32()); }
            public uint UInt32() { Need(4); uint value = ((uint)data[position] << 24) | ((uint)data[position + 1] << 16) | ((uint)data[position + 2] << 8) | data[position + 3]; position += 4; return value; }
            public void Skip(long count) { Position = checked(position + count); }
            public void SkipSection32() { Position = SectionEnd32(); }
            public long SectionEnd32() { uint length = UInt32(); long end = checked(position + length); if (end > data.LongLength) throw new InvalidDataException("PSD section is truncated."); return end; }
            void Need(long count) { if (count < 0 || position + count > data.LongLength) throw new InvalidDataException("PSD data is truncated."); }
        }
    }
}
