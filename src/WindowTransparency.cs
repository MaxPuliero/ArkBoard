using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ArkBoard
{
    // Uniform desktop alpha includes the native title bar and preserves window resizing.
    internal sealed class WindowTransparency
    {
        const int ExtendedStyle = -20;
        const long Layered = 0x80000;
        const uint Alpha = 2;
        [StructLayout(LayoutKind.Sequential)]
        struct StyleChange { public uint OldStyle; public uint NewStyle; }
        readonly IntPtr window;
        bool layered;
        internal WindowTransparency(HwndSource source)
        {
            window = source.Handle;
            source.AddHook(delegate(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (layered && message == 0x007C && wParam.ToInt64() == ExtendedStyle)
                {
                    StyleChange change = (StyleChange)Marshal.PtrToStructure(lParam, typeof(StyleChange));
                    // WPF normally owns the layered bit for per-pixel surfaces. This window
                    // keeps its opaque render surface and uses Win32's uniform alpha instead.
                    change.NewStyle |= (uint)Layered;
                    Marshal.StructureToPtr(change, lParam, false);
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
        [DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr window, IntPtr rect, IntPtr region, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLayeredWindowAttributes(IntPtr window, out uint color, out byte alpha, out uint flags);

        internal static bool IsLayered(IntPtr window) { return (GetWindowLongPtr(window, ExtendedStyle).ToInt64() & Layered) != 0; }
        internal void Apply(double percent, bool forceLayered = false)
        {
            if (window == IntPtr.Zero) return;
            if (!BoardDocument.Finite(percent)) throw new ArgumentOutOfRangeException("percent");
            long style = GetWindowLongPtr(window, ExtendedStyle).ToInt64();
            layered = percent < 100 || forceLayered;
            if (!layered)
            {
                // Restore WPF's normal opaque presentation path, including after a resize.
                if ((style & Layered) != 0)
                {
                    SetWindowLongPtr(window, ExtendedStyle, new IntPtr(style & ~Layered));
                    if (IsLayered(window)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    RedrawWindow(window, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0080 | 0x0400);
                }
                return;
            }
            if ((style & Layered) == 0)
            {
                SetWindowLongPtr(window, ExtendedStyle, new IntPtr(style | Layered));
                if ((GetWindowLongPtr(window, ExtendedStyle).ToInt64() & Layered) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            byte opacity = (byte)Math.Round(Math.Max(5, Math.Min(100, percent)) * 255 / 100);
            if (!SetLayeredWindowAttributes(window, 0, opacity, Alpha))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
