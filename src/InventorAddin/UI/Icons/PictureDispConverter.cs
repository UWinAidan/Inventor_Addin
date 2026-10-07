using System;
using System.Drawing;
using System.Runtime.InteropServices;

// Namespace InventorAddin.UI, not InventorAddin.UI.Icons, to match ThemeResources and keep one namespace for the UI helpers.
namespace InventorAddin.UI
{
    /// <summary>
    /// Turns a bitmap into the OLE picture (<c>IPictureDisp</c>) that Inventor's button definitions take as icons.
    /// Uses the method spike 020 proved in Inventor 2026: <c>OleCreatePictureIndirect</c> with <c>PICTYPE_ICON</c>
    /// and <see cref="Bitmap.GetHicon"/>. <c>PICTYPE_BITMAP</c> with <c>GetHbitmap</c> also loads, but loses the
    /// PNG's transparency and leaves a white edge round the icon on the dark ribbon.
    /// </summary>
    internal static class PictureDispConverter
    {
        private const int PictypeIcon = 3;

        // IID of IPictureDisp.
        private static readonly Guid PictureDispIid = new Guid("7BF80981-BF32-101A-8BBB-00AA00300CAB");

        /// <summary>
        /// A new picture holding a copy of <paramref name="bitmap"/> as an icon. The picture owns the icon handle and
        /// destroys it when the picture is released, so the caller may dispose the bitmap straight away and has no handle
        /// to free. Throws if the conversion fails; the icon handle is destroyed in that case.
        /// </summary>
        public static object FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            IntPtr icon = bitmap.GetHicon();
            try
            {
                var desc = new PictDesc
                {
                    cbSizeofstruct = Marshal.SizeOf<PictDesc>(),
                    picType = PictypeIcon,
                    handle = icon,
                };
                Guid iid = PictureDispIid;

                // own = true: the picture takes the icon handle and destroys it when it is released.
                OleCreatePictureIndirect(ref desc, ref iid, true, out object picture);
                return picture;
            }
            catch
            {
                // The picture was not created, so the handle is still ours.
                DestroyIcon(icon);
                throw;
            }
        }

        // PICTDESC: two UINTs and a union whose largest member (bmp or wmf) is two pointer-sized fields on x64.
        // For PICTYPE_ICON only the first union field (the HICON) is read.
        [StructLayout(LayoutKind.Sequential)]
        private struct PictDesc
        {
            public int cbSizeofstruct;
            public int picType;
            public IntPtr handle;
            public IntPtr hpal;
        }

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void OleCreatePictureIndirect(
            ref PictDesc desc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Bool)] bool own,
            [MarshalAs(UnmanagedType.IUnknown)] out object picture);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
