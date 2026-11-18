using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ArkBoard
{
    // The Windows Common Item Dialog used by Save As, configured to select folders.
    internal static class FolderPicker
    {
        const uint PickFolders = 0x20, ForceFileSystem = 0x40, PathMustExist = 0x800, NoChangeDirectory = 0x8;
        const int Cancelled = unchecked((int)0x800704C7);

        internal static string Show(Window owner)
        {
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
            IShellItem item = null;
            try
            {
                var dialog = (IFileDialog)instance;
                uint options; dialog.GetOptions(out options);
                dialog.SetOptions(options | PickFolders | ForceFileSystem | PathMustExist | NoChangeDirectory);
                dialog.SetTitle(Localization.T("Export Images"));
                dialog.SetOkButtonLabel(Localization.T("Export"));
                int result = dialog.Show(new WindowInteropHelper(owner).EnsureHandle());
                if (result == Cancelled) return null;
                Marshal.ThrowExceptionForHR(result);
                dialog.GetResult(out item);
                IntPtr name;
                item.GetDisplayName(0x80058000, out name); // SIGDN_FILESYSPATH, allocated by the shell.
                try { return Marshal.PtrToStringUni(name); }
                finally { Marshal.FreeCoTaskMem(name); }
            }
            finally
            {
                if (item != null) Marshal.ReleaseComObject(item);
                Marshal.ReleaseComObject(instance);
            }
        }

        // Keep the native vtable order, including unused methods preceding GetResult.
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem selection);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem result);
        }
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint format, out IntPtr name);
        }
    }
}
