using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    // Decode directly through the bundled x64 library, never through installed WIC codecs.
    internal static class WebpSupport
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int WebPGetInfo(byte[] data, UIntPtr length, out int width, out int height);
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr WebPDecodeBGRAInto(byte[] data, UIntPtr length, byte[] output, UIntPtr outputSize, int stride);
        [StructLayout(LayoutKind.Sequential)]
        struct WebPData { internal IntPtr Bytes; internal UIntPtr Size; }
        [DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr WebPAnimDecoderNewInternal(ref WebPData data, IntPtr options, int abi);
        [DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int WebPAnimDecoderGetNext(IntPtr decoder, out IntPtr rgba, out int timestamp);
        [DllImport("libwebpdemux.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void WebPAnimDecoderDelete(IntPtr decoder);
        static readonly object gate = new object();
        static bool loaded;
        static void EnsureLoaded()
        {
            lock (gate)
            {
                if (loaded) return;
                foreach (string name in new[] { "libwebp.dll", "libwebpdemux.dll" })
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                    if (LoadLibraryEx(path, IntPtr.Zero, 0x1100) == IntPtr.Zero)
                        throw new IOException("Unable to load bundled WebP decoder: " + name + ". Keep all portable application files together.", new Win32Exception(Marshal.GetLastWin32Error()));
                }
                loaded = true;
            }
        }
        internal static bool IsWebp(byte[] b)
        {
            return b != null && b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
                b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';
        }
        internal static BitmapSource Decode(byte[] bytes)
        {
            EnsureLoaded(); int width, height;
            if (WebPGetInfo(bytes, (UIntPtr)bytes.Length, out width, out height) == 0 || width < 1 || height < 1)
                throw new InvalidDataException("Invalid WebP image.");
            if ((long)width * height > 80000000) throw new InvalidDataException("Image exceeds the 80 megapixel limit.");
            int stride = checked(width * 4); byte[] pixels = new byte[checked(stride * height)];
            bool animated = bytes.Length > 20 && bytes[12] == 'V' && bytes[13] == 'P' && bytes[14] == '8' && bytes[15] == 'X' && (bytes[20] & 2) != 0;
            PixelFormat format = PixelFormats.Bgra32;
            if (animated)
            {
                GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                IntPtr decoder = IntPtr.Zero;
                try
                {
                    var data = new WebPData { Bytes = pinned.AddrOfPinnedObject(), Size = (UIntPtr)bytes.Length };
                    decoder = WebPAnimDecoderNewInternal(ref data, IntPtr.Zero, 0x0107);
                    IntPtr rgba; int timestamp;
                    if (decoder == IntPtr.Zero || WebPAnimDecoderGetNext(decoder, out rgba, out timestamp) == 0)
                        throw new InvalidDataException("Unable to decode animated WebP image.");
                    // Animation decoder defaults to straight-alpha RGBA bytes; WPF expects BGRA.
                    Marshal.Copy(rgba, pixels, 0, pixels.Length);
                    for (int i = 0; i < pixels.Length; i += 4) { byte red = pixels[i]; pixels[i] = pixels[i + 2]; pixels[i + 2] = red; }
                    format = PixelFormats.Bgra32;
                }
                finally { if (decoder != IntPtr.Zero) WebPAnimDecoderDelete(decoder); pinned.Free(); }
            }
            else if (WebPDecodeBGRAInto(bytes, (UIntPtr)bytes.Length, pixels, (UIntPtr)pixels.Length, stride) == IntPtr.Zero)
                throw new InvalidDataException("Unable to decode WebP image.");
            BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
            bitmap.Freeze(); return bitmap;
        }
    }
}
