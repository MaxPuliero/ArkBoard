using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Runtime.CompilerServices;

namespace ArkBoard
{
    internal static class TextLayout
    {
        internal static readonly Brush Foreground = MakeForeground();
        sealed class CachedText { internal string Text; internal double FontSize; internal FormattedText Layout; }
        static readonly ConditionalWeakTable<ImageItem, CachedText> cache = new ConditionalWeakTable<ImageItem, CachedText>();
        static Brush MakeForeground() { var brush = new SolidColorBrush(Color.FromRgb(226, 226, 226)); brush.Freeze(); return brush; }
        internal static FormattedText Format(string text, double fontSize)
        {
            return new FormattedText(string.IsNullOrEmpty(text) ? "\u200b" : text + (text.EndsWith("\n") ? "\u200b" : ""),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize, Foreground, 1);
        }
        internal static Size Measure(string text, double fontSize)
        {
            FormattedText formatted = Format(text, fontSize);
            return new Size(Math.Max(1, formatted.WidthIncludingTrailingWhitespace), Math.Max(fontSize, formatted.Height));
        }
        internal static void Draw(DrawingContext dc, ImageItem item)
        {
            FormattedText layout = GetLayout(item);
            double width = Math.Max(1, layout.WidthIncludingTrailingWhitespace), height = Math.Max(item.FontSize, layout.Height);
            dc.PushTransform(new ScaleTransform(item.Width / width, item.Height / height));
            dc.DrawText(layout, new Point(-width / 2, -height / 2)); dc.Pop();
        }
        static FormattedText GetLayout(ImageItem item)
        {
            CachedText entry = cache.GetValue(item, key => new CachedText());
            if (entry.Layout == null || entry.Text != item.Text || entry.FontSize != item.FontSize)
            { entry.Text = item.Text; entry.FontSize = item.FontSize; entry.Layout = Format(item.Text, item.FontSize); }
            return entry.Layout;
        }
        internal static Size Measure(ImageItem item)
        {
            FormattedText layout = GetLayout(item);
            return new Size(Math.Max(1, layout.WidthIncludingTrailingWhitespace), Math.Max(item.FontSize, layout.Height));
        }
    }
}
