using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Theming;

namespace InventorAddin.UI
{
    /// <summary>
    /// Shows the add-in's WPF windows. A dialog is owned by Inventor's main window, so it opens in
    /// front of Inventor and Inventor cannot be used until it closes. Every window gets the shared look here,
    /// in the colours of Inventor's active theme; no window applies the theme itself. Every window is also kept within
    /// the working area of the screen it opens on (Aidan, 2026-10-17): never taller, and moved up if it would run off
    /// the bottom. A window's body scrolls when its content is taller (the <c>DialogBody</c> style).
    /// </summary>
    internal static class WindowHost
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE; confirmed on Windows 11 25H2 by spike 020.
        private const int DwmUseImmersiveDarkMode = 20;

        private static bool _darkTitleBarFailureLogged;
        private static bool _screenFailureLogged;

        /// <summary>
        /// Resource key of the header icon, which <see cref="ShowDialog"/> puts in the window's resources.
        /// A window's <c>DialogHeader</c> shows it with <c>Icon="{DynamicResource HeaderIcon}"</c>.
        /// </summary>
        public const string HeaderIconKey = "HeaderIcon";

        /// <summary>
        /// Shows <paramref name="window"/> modally over Inventor and returns its dialog result.
        /// <paramref name="headerIconName"/> (one of <c>IconNames</c>) is the icon shown in the window's header tile,
        /// in the theme the window opens in; with none, the tile stays empty. With <paramref name="owner"/>, an add-in
        /// window that is open, the window is owned by it and opens over it (task 076: Material and Finish from Part
        /// Properties); otherwise it is owned by Inventor's main window.
        /// </summary>
        public static bool? ShowDialog(Window window, string? headerIconName = null, Window? owner = null)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            // Read as long so the conversion works whether the interop declares the handle as int or long.
            long? hwnd = owner == null ? ComSafe.Get(() => (long?)InventorHost.App.MainFrameHWND) : null;
            if (owner != null)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else if (hwnd.HasValue && hwnd.Value != 0)
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
            UiTheme theme = ApplyTheme(window);
            if (headerIconName != null)
                SetHeaderIcon(window, headerIconName, theme);

            IntPtr screenHandle = owner != null
                ? new WindowInteropHelper(owner).Handle
                : new IntPtr(hwnd ?? 0);
            FitToScreen(window, screenHandle);
            return window.ShowDialog();
        }

        /// <summary>
        /// Caps the window's height at the working area of the screen that <paramref name="screenHandle"/> (the owner)
        /// is on, and keeps the window inside it as it opens and whenever it changes size (windows size to their
        /// content). With no handle the primary screen is used. A failure leaves the window as it is and is logged once
        /// per session.
        /// </summary>
        private static void FitToScreen(Window window, IntPtr screenHandle)
        {
            Rect workArea;
            try
            {
                System.Drawing.Rectangle pixels = System.Windows.Forms.Screen.FromHandle(screenHandle).WorkingArea;

                // The working area is in device pixels; the window's sizes are in WPF units at the window's own scale.
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                Matrix fromDevice = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                workArea = new Rect(
                    fromDevice.Transform(new Point(pixels.Left, pixels.Top)),
                    fromDevice.Transform(new Point(pixels.Right, pixels.Bottom)));
            }
            catch (Exception ex)
            {
                if (!_screenFailureLogged)
                {
                    _screenFailureLogged = true;
                    AddinServices.Log.Warn($"Could not read the screen's working area; windows are not kept on screen. {ex.Message}");
                }
                return;
            }

            double? maxHeight = WindowFit.MaxHeight(workArea.Height);
            if (maxHeight.HasValue)
                window.MaxHeight = Math.Min(window.MaxHeight, maxHeight.Value);

            void KeepOnScreen()
            {
                double? top = WindowFit.ClampTop(window.Top, window.ActualHeight, workArea.Top, workArea.Bottom);
                if (top.HasValue)
                    window.Top = top.Value;
            }

            window.Loaded += (_, _) => KeepOnScreen();
            window.SizeChanged += (_, _) => KeepOnScreen();
        }

        /// <summary>
        /// Merges the colour set for Inventor's theme and the shared styles into the window's resources and gives the
        /// window the dialog style. The window is already built, so it refers to shared styles with DynamicResource.
        /// If the dictionaries fail to load, the window keeps the default WPF look (the error is logged).
        /// Returns the theme used.
        /// </summary>
        private static UiTheme ApplyTheme(Window window)
        {
            UiTheme theme = ThemeResources.ReadInventorTheme();

            if (ThemeResources.TryMergeInto(window.Resources, theme))
                window.SetResourceReference(FrameworkElement.StyleProperty, ThemeResources.DialogWindowStyleKey);

            if (theme == UiTheme.Dark)
                UseDarkTitleBar(window);

            return theme;
        }

        /// <summary>
        /// Puts the 32 px icon <paramref name="iconName"/> for <paramref name="theme"/> in the window's resources under
        /// <see cref="HeaderIconKey"/>. A failure leaves the header tile empty and is logged; the window still opens.
        /// </summary>
        private static void SetHeaderIcon(Window window, string iconName, UiTheme theme)
        {
            try
            {
                window.Resources[HeaderIconKey] = IconLoader.LoadImageSource(iconName, theme, RibbonIcons.LargeSizePx);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn(
                    $"Header icon {iconName} ({UiThemes.ResourceSuffix(theme)}) could not be loaded for '{window.Title}'; " +
                    $"the icon tile stays empty. {ex.Message}");
            }
        }

        /// <summary>
        /// Asks Windows for a dark title bar. Needs the handle, so it is created here, after the owner is set and
        /// before ShowDialog, as in spike 020. A failure leaves the light title bar and is logged once per session.
        /// </summary>
        private static void UseDarkTitleBar(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                int enabled = 1;
                int result = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
                if (result != 0)
                    LogDarkTitleBarFailureOnce($"DwmSetWindowAttribute returned 0x{result:X8}.");
            }
            catch (Exception ex)
            {
                LogDarkTitleBarFailureOnce(ex.Message);
            }
        }

        private static void LogDarkTitleBarFailureOnce(string reason)
        {
            if (_darkTitleBarFailureLogged)
                return;

            _darkTitleBarFailureLogged = true;
            AddinServices.Log.Warn($"Could not turn on the dark title bar; windows keep the light one. {reason}");
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
