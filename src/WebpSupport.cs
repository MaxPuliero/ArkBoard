using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    internal sealed class WebpCodecUnavailableException : NotSupportedException
    {
        internal WebpCodecUnavailableException(Exception inner) : base(WebpSupport.HelpMessage, inner) { }
    }

    internal static class WebpSupport
    {
        internal const string ExtensionUrl = "https://apps.microsoft.com/detail/9PG2DK419DRG";
        internal static string HelpMessage
        {
            get { return Localization.T("This PC cannot read WebP images. Install or update Microsoft's WebP Image Extension, then close and reopen ArkBoard and try again.") + "\n\n" + ExtensionUrl; }
        }
        internal static bool IsWebp(byte[] b)
        {
            return b != null && b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
                b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';
        }
        internal static bool IsCodecUnavailable(Exception error)
        {
            for (Exception ex = error; ex != null; ex = ex.InnerException)
                if (ex is NotSupportedException || ex.HResult == unchecked((int)0x88982f50) || ex.HResult == unchecked((int)0x88982f07)) return true;
            return false;
        }
        internal static bool NeedsExtensionHelp(Exception error)
        {
            for (Exception ex = error; ex != null; ex = ex.InnerException)
                if (ex is WebpCodecUnavailableException) return true;
            return false;
        }
        internal static BitmapSource Decode(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                {
                    BitmapSource decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    if ((long)decoded.PixelWidth * decoded.PixelHeight > 80000000)
                        throw new InvalidDataException("Image exceeds the 80 megapixel limit.");
                    // Detach WIC frame metadata from its decoder/dispatcher before background PNG saves.
                    var converted = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
                    int stride = checked(decoded.PixelWidth * 4);
                    byte[] pixels = new byte[checked(stride * decoded.PixelHeight)];
                    converted.CopyPixels(pixels, stride, 0);
                    BitmapSource frame = BitmapSource.Create(decoded.PixelWidth, decoded.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                    frame.Freeze(); return frame;
                }
            }
            catch (Exception ex)
            {
                if (IsCodecUnavailable(ex)) throw new WebpCodecUnavailableException(ex);
                throw;
            }
        }
    }
}
