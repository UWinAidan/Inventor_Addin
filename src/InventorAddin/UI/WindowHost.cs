using System;
using System.Windows;
using System.Windows.Interop;

namespace InventorAddin.UI
{
    /// <summary>
    /// Shows the add-in's WPF windows. A dialog is owned by Inventor's main window, so it opens in
    /// front of Inventor and Inventor cannot be used until it closes.
    /// </summary>
    internal static class WindowHost
    {
        /// <summary>Shows <paramref name="window"/> modally over Inventor and returns its dialog result.</summary>
        public static bool? ShowDialog(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            // Read as long so the conversion works whether the interop declares the handle as int or long.
            long? hwnd = ComSafe.Get(() => (long?)InventorHost.App.MainFrameHWND);
            if (hwnd.HasValue && hwnd.Value != 0)
            {
                new WindowInteropHelper(window).Owner = new IntPtr(hwnd.Value);
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                AddinServices.Log.Warn($"Could not read Inventor's main window handle; '{window.Title}' is shown without an owner.");
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowInTaskbar = false;
            return window.ShowDialog();
        }
    }
}
