using System;
using System.Runtime.InteropServices;

namespace ArkBoard
{
    internal sealed class WindowInputLock
    {
        const int ExtendedStyle = -20;
        const long Transparent = 0x00000020;
        readonly IntPtr window;

        internal WindowInputLock(IntPtr handle) { window = handle; }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        internal void Apply(bool locked)
        {
            long style = GetWindowLongPtr(window, ExtendedStyle).ToInt64();
            long wanted = locked ? style | Transparent : style & ~Transparent;
            if (wanted != style)
                SetWindowLongPtr(window, ExtendedStyle, new IntPtr(wanted));
        }

        internal static bool IsClickThrough(IntPtr window)
        { return (GetWindowLongPtr(window, ExtendedStyle).ToInt64() & Transparent) != 0; }
    }
}
