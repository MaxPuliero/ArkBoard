using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ArkBoard
{
    internal sealed class BeeImportResult
    {
        internal readonly List<ImageItem> Items = new List<ImageItem>();
        internal readonly Dictionary<string, AssetData> Assets = new Dictionary<string, AssetData>(StringComparer.Ordinal);
        internal int SkippedItems;
        internal int IgnoredEffects;
    }

    [DataContract]
    internal sealed class BeeExtraData
    {
        [DataMember(Name = "filename", EmitDefaultValue = false)] public string Filename = null;
        [DataMember(Name = "text", EmitDefaultValue = false)] public string Text = null;
        [DataMember(Name = "crop", EmitDefaultValue = false)] public double[] Crop = null;
        [DataMember(Name = "opacity", EmitDefaultValue = false)] public double? Opacity = null;
        [DataMember(Name = "grayscale", EmitDefaultValue = false)] public bool Grayscale = false;
    }

    internal static class BeeImporter
    {
        const int ApplicationId = 2060242126;
        const int MaxItems = 10000;
        const int MaxJsonBytes = 1024 * 1024;
        const long MaxTotalAssetBytes = 1024L * 1024 * 1024;
        const double DefaultTextSize = 12;

        internal static bool IsBeeFile(string path)
        { return path != null && path.EndsWith(".bee", StringComparison.OrdinalIgnoreCase); }

        internal static BeeImportResult Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("BeeRef project was not found.", path);
            if (new FileInfo(path).Length > MaxTotalAssetBytes + 64L * 1024 * 1024)
                throw new InvalidDataException("BeeRef project exceeds the supported size limit.");

            using (var database = BeeDatabase.OpenReadOnly(path))
            {
                int applicationId = database.ScalarInt("PRAGMA application_id");
                int version = database.ScalarInt("PRAGMA user_version");
                if (applicationId != ApplicationId) throw new InvalidDataException("This SQLite file is not a BeeRef project.");
                if (version < 1 || version > 2) throw new InvalidDataException("This BeeRef project version is not supported.");
                ValidateSchema(database, version);
                return ReadItems(database, version);
            }
        }

        static void ValidateSchema(BeeDatabase database, int version)
        {
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "id", "type", "x", "y", "z", "scale", "rotation", "flip", version == 1 ? "filename" : "data" };
            using (BeeStatement statement = database.Prepare("PRAGMA table_info(items)"))
                while (statement.Step()) required.Remove(statement.Text(1));
            if (required.Count != 0) throw new InvalidDataException("BeeRef item table is incomplete.");

            required = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "item_id", "name", "sz", "data" };
            using (BeeStatement statement = database.Prepare("PRAGMA table_info(sqlar)"))
                while (statement.Step()) required.Remove(statement.Text(1));
            if (required.Count != 0) throw new InvalidDataException("BeeRef image table is incomplete.");
        }

        static BeeImportResult ReadItems(BeeDatabase database, int version)
        {
            string extra = version == 1 ? "items.filename" : "items.data";
            string sql = "SELECT items.id,type,x,y,z,scale,rotation,flip," + extra + ",sqlar.name,sqlar.sz,sqlar.data " +
                "FROM items LEFT JOIN sqlar ON sqlar.item_id=items.id ORDER BY z,items.id";
            var result = new BeeImportResult();
            long totalAssetBytes = 0;
            using (BeeStatement statement = database.Prepare(sql))
            {
                while (statement.Step())
                {
                    if (result.Items.Count + result.SkippedItems >= MaxItems)
                        throw new InvalidDataException("BeeRef project contains too many items.");
                    try
                    {
                        string type = statement.Text(1);
                        double x = statement.Double(2), y = statement.Double(3), z = statement.Double(4);
                        double scale = statement.Double(5), rotation = statement.Double(6);
                        long flipValue = statement.Int64(7);
                        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(scale) || !Finite(rotation) ||
                            Math.Abs(x) > 100000000 || Math.Abs(y) > 100000000 || scale <= 0 || scale > 1000000)
                            throw new InvalidDataException("Invalid BeeRef item geometry.");
                        if (flipValue != 1 && flipValue != -1) throw new InvalidDataException("Invalid BeeRef flip value.");

                        BeeExtraData data = version == 1 ? new BeeExtraData { Filename = statement.Text(8) } : ParseExtraData(statement.Text(8));
                        ImageItem item;
                        if (string.Equals(type, "pixmap", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] bytes = statement.Blob(11, AssetData.MaxBytes);
                            long storedSize = statement.IsNull(10) ? bytes.LongLength : statement.Int64(10);
                            if (storedSize != bytes.LongLength) throw new InvalidDataException("Compressed SQLAR images are not supported.");
                            totalAssetBytes += bytes.LongLength;
                            if (totalAssetBytes > MaxTotalAssetBytes) throw new BeeProjectLimitException("BeeRef project exceeds the embedded image limit.");
                            AssetData asset = AssetData.Create(bytes);
                            item = CreateImageItem(asset, SourceName(data.Filename, statement.Text(9)), x, y, scale, rotation, flipValue, data);
                            result.Assets[asset.Key] = asset;
                            if (data.Grayscale || (data.Opacity.HasValue && Finite(data.Opacity.Value) && Math.Abs(data.Opacity.Value - 1) > .000001)) result.IgnoredEffects++;
                        }
                        else if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                            item = CreateTextItem(data.Text, x, y, scale, rotation, flipValue);
                        else
                        {
                            result.SkippedItems++;
                            continue;
                        }
                        result.Items.Add(item);
                    }
                    catch (BeeProjectLimitException) { throw; }
                    catch (InvalidDataException)
                    {
                        result.SkippedItems++;
                    }
                    catch (ArgumentException)
                    {
                        result.SkippedItems++;
                    }
                    catch (NotSupportedException)
                    {
                        result.SkippedItems++;
                    }
                    catch (FileFormatException)
                    {
                        result.SkippedItems++;
                    }
                }
            }
            if (result.Items.Count == 0 && result.SkippedItems > 0)
                throw new InvalidDataException("BeeRef project did not contain any readable items.");
            return result;
        }

        static ImageItem CreateImageItem(AssetData asset, string name, double x, double y, double scale, double rotation, long flip, BeeExtraData data)
        {
            double width = asset.Bitmap.PixelWidth * scale, height = asset.Bitmap.PixelHeight * scale;
            ValidateDimensions(width, height);
            Point center = ItemCenter(x, y, width, height, rotation, flip);
            var item = new ImageItem { Asset = asset.Key, Name = name, X = center.X, Y = center.Y,
                Width = width, Height = height, Rotation = NormalizeAngle(rotation), FlipX = flip == -1 };
            if (data.Crop != null)
            {
                if (data.Crop.Length != 4 || data.Crop[0] < 0 || data.Crop[1] < 0 || data.Crop[2] <= 0 || data.Crop[3] <= 0 ||
                    !Finite(data.Crop[0]) || !Finite(data.Crop[1]) || !Finite(data.Crop[2]) || !Finite(data.Crop[3]) ||
                    data.Crop[0] + data.Crop[2] > asset.Bitmap.PixelWidth + .01 || data.Crop[1] + data.Crop[3] > asset.Bitmap.PixelHeight + .01)
                    throw new InvalidDataException("Invalid BeeRef crop rectangle.");
                item.MaskLeft = Clamp01(data.Crop[0] / asset.Bitmap.PixelWidth);
                item.MaskTop = Clamp01(data.Crop[1] / asset.Bitmap.PixelHeight);
                item.MaskRight = Clamp01((asset.Bitmap.PixelWidth - data.Crop[0] - data.Crop[2]) / asset.Bitmap.PixelWidth);
                item.MaskBottom = Clamp01((asset.Bitmap.PixelHeight - data.Crop[1] - data.Crop[3]) / asset.Bitmap.PixelHeight);
                if (!BoardDocument.ValidMask(item)) throw new InvalidDataException("Invalid BeeRef crop rectangle.");
            }
            return item;
        }

        static ImageItem CreateTextItem(string text, double x, double y, double scale, double rotation, long flip)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 10000) throw new InvalidDataException("Invalid BeeRef text item.");
            Size natural = TextLayout.Measure(text, DefaultTextSize);
            double width = natural.Width * scale, height = natural.Height * scale;
            ValidateDimensions(width, height);
            Point center = ItemCenter(x, y, width, height, rotation, flip);
            return new ImageItem { Text = text, FontSize = DefaultTextSize, Name = "Text", X = center.X, Y = center.Y,
                Width = width, Height = height, Rotation = NormalizeAngle(rotation), FlipX = flip == -1 };
        }

        static Point ItemCenter(double x, double y, double width, double height, double rotation, long flip)
        {
            Matrix matrix = Matrix.Identity;
            matrix.Scale(flip, 1); matrix.Rotate(NormalizeAngle(rotation)); matrix.Translate(x, y);
            Point center = matrix.Transform(new Point(width / 2, height / 2));
            if (!Finite(center.X) || !Finite(center.Y) || Math.Abs(center.X) > 100000000 || Math.Abs(center.Y) > 100000000)
                throw new InvalidDataException("BeeRef item position is outside the supported range.");
            return center;
        }

        static BeeExtraData ParseExtraData(string json)
        {
            if (string.IsNullOrEmpty(json)) return new BeeExtraData();
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > MaxJsonBytes) throw new InvalidDataException("BeeRef item metadata is too large.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                    return (BeeExtraData)new DataContractJsonSerializer(typeof(BeeExtraData)).ReadObject(stream) ?? new BeeExtraData();
            }
            catch (SerializationException ex) { throw new InvalidDataException("Invalid BeeRef item metadata.", ex); }
            catch (System.Xml.XmlException ex) { throw new InvalidDataException("Invalid BeeRef item metadata.", ex); }
        }

        static string SourceName(string filename, string archiveName)
        {
            string value = string.IsNullOrWhiteSpace(filename) ? archiveName : filename;
            if (string.IsNullOrWhiteSpace(value)) return "BeeRef image";
            value = value.Replace('\\', '/'); int slash = value.LastIndexOf('/');
            value = slash >= 0 && slash + 1 < value.Length ? value.Substring(slash + 1) : value;
            return value.Length <= 512 ? value : value.Substring(0, 512);
        }

        static void ValidateDimensions(double width, double height)
        {
            if (!Finite(width) || !Finite(height) || width < .01 || height < .01 || width > 1000000 || height > 1000000)
                throw new InvalidDataException("BeeRef item dimensions are outside the supported range.");
        }
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
        static double NormalizeAngle(double value) { return ((value % 360) + 360) % 360; }
    }

    internal sealed class BeeDatabase : IDisposable
    {
        const int SqliteOpenReadOnly = 0x00000001;
        IntPtr handle;

        BeeDatabase(IntPtr handle) { this.handle = handle; }

        internal static BeeDatabase OpenReadOnly(string path)
        {
            IntPtr handle;
            int result = Native.sqlite3_open_v2(Utf8(path), out handle, SqliteOpenReadOnly, IntPtr.Zero);
            if (result != 0)
            {
                string message = handle == IntPtr.Zero ? "Unable to open SQLite database." : Native.Error(handle);
                if (handle != IntPtr.Zero) Native.sqlite3_close_v2(handle);
                throw new InvalidDataException(message);
            }
            Native.sqlite3_limit(handle, 0, 128 * 1024 * 1024);
            return new BeeDatabase(handle);
        }

        internal BeeStatement Prepare(string sql)
        {
            IntPtr statement;
            int result = Native.sqlite3_prepare_v2(handle, Utf8(sql), -1, out statement, IntPtr.Zero);
            if (result != 0) throw new InvalidDataException(Native.Error(handle));
            return new BeeStatement(handle, statement);
        }

        internal int ScalarInt(string sql)
        {
            using (BeeStatement statement = Prepare(sql))
            { if (!statement.Step()) throw new InvalidDataException("BeeRef metadata is missing."); return checked((int)statement.Int64(0)); }
        }

        static byte[] Utf8(string value)
        {
            byte[] text = Encoding.UTF8.GetBytes(value); byte[] terminated = new byte[text.Length + 1];
            Buffer.BlockCopy(text, 0, terminated, 0, text.Length); return terminated;
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero) { Native.sqlite3_close_v2(handle); handle = IntPtr.Zero; }
        }

        internal static class Native
        {
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr database);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int length, out IntPtr statement, IntPtr tail);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_step(IntPtr statement);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr statement);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern long sqlite3_column_int64(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern double sqlite3_column_double(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_blob(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_bytes(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_type(IntPtr statement, int column);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_errmsg(IntPtr database);
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_limit(IntPtr database, int id, int newValue);
            internal static string Error(IntPtr database) { return Marshal.PtrToStringAnsi(sqlite3_errmsg(database)) ?? "SQLite error."; }
        }
    }

    internal sealed class BeeStatement : IDisposable
    {
        const int Row = 100, Done = 101, Null = 5;
        IntPtr database, statement;
        internal BeeStatement(IntPtr database, IntPtr statement) { this.database = database; this.statement = statement; }
        internal bool Step()
        {
            int result = BeeDatabase.Native.sqlite3_step(statement);
            if (result == Row) return true;
            if (result == Done) return false;
            throw new InvalidDataException(BeeDatabase.Native.Error(database));
        }
        internal bool IsNull(int column) { return BeeDatabase.Native.sqlite3_column_type(statement, column) == Null; }
        internal long Int64(int column) { return BeeDatabase.Native.sqlite3_column_int64(statement, column); }
        internal double Double(int column) { return BeeDatabase.Native.sqlite3_column_double(statement, column); }
        internal string Text(int column)
        {
            if (IsNull(column)) return null;
            int length = BeeDatabase.Native.sqlite3_column_bytes(statement, column);
            if (length < 0 || length > 1024 * 1024) throw new InvalidDataException("BeeRef text value is too large.");
            IntPtr pointer = BeeDatabase.Native.sqlite3_column_text(statement, column);
            if (pointer == IntPtr.Zero) return "";
            byte[] bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return Encoding.UTF8.GetString(bytes);
        }
        internal byte[] Blob(int column, long limit)
        {
            if (IsNull(column)) throw new InvalidDataException("BeeRef image data is missing.");
            int length = BeeDatabase.Native.sqlite3_column_bytes(statement, column);
            if (length <= 0 || length > limit) throw new InvalidDataException("BeeRef image is empty or too large.");
            IntPtr pointer = BeeDatabase.Native.sqlite3_column_blob(statement, column);
            if (pointer == IntPtr.Zero) throw new InvalidDataException("BeeRef image data is missing.");
            byte[] bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return bytes;
        }
        public void Dispose()
        {
            if (statement != IntPtr.Zero) { BeeDatabase.Native.sqlite3_finalize(statement); statement = IntPtr.Zero; }
        }
    }

    internal sealed class BeeProjectLimitException : IOException
    { internal BeeProjectLimitException(string message) : base(message) { } }
}
