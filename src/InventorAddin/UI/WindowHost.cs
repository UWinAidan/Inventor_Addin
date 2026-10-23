using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.Theming;
using InventorAddin.UI.Controls;

namespace InventorAddin.UI
{
    /// <summary>
    /// Shows the add-in's WPF windows. A dialog is owned by Inventor's main window, so it opens in
    /// front of Inventor and Inventor cannot be used until it closes. A modeless window (<see cref="ShowModeless"/>,
    /// AWB Hole) is owned by Inventor's main window too, but Inventor stays usable while it is open. Every window gets the shared look here,
    /// in the colours of Inventor's active theme; no window applies the theme itself. Every window is also kept within
    /// the working area of the screen it opens on (Aidan, 2026-10-17): never taller, and moved up if it would run off
    /// the bottom. A window's body scrolls when its content is taller (the <c>DialogBody</c> style).
    /// A window whose XAML sets <c>ResizeMode="CanResize"</c> gets the resizable look and can be resized by its edges,
    /// but like every dialog it has only the close button: no minimise or maximise (Aidan, 2026-10-17). A window shown
    /// with a state key remembers, in <c>windows.json</c>, which of its named <see cref="CollapsibleSection"/>s were
    /// collapsed and, if resizable, its size and position.
    /// </summary>
    internal static class WindowHost
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE; confirmed on Windows 11 25H2 by spike 020.
        private const int DwmUseImmersiveDarkMode = 20;

        private const int GwlStyle = -16;
        private const int WsMinimizeBox = 0x00020000;
        private const int WsMaximizeBox = 0x00010000;

        private static bool _darkTitleBarFailureLogged;
        private static bool _screenFailureLogged;
        private static bool _stateFailureLogged;

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
        /// Properties); otherwise it is owned by Inventor's main window. With <paramref name="stateKey"/>, the window
        /// opens with the sections collapsed and, if resizable, at the size and position it had when it last closed.
        /// </summary>
        public static bool? ShowDialog(Window window, string? headerIconName = null, Window? owner = null, string? stateKey = null)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            Prepare(window, headerIconName, owner, stateKey);
            return window.ShowDialog();
        }

        /// <summary>
        /// Shows <paramref name="window"/> modeless over Inventor (task 090: AWB Hole stays open while the user picks in
        /// the model) and returns at once. It gets everything <see cref="ShowDialog"/> gives a window: owned by Inventor's
        /// main window, so it stays in front of Inventor; the theme; the header icon; kept on the screen. Keyboard input
        /// reaches it through <c>ElementHost.EnableModelessKeyboardInterop</c>, since Inventor runs the message loop.
        /// Call it after Inventor's event or command has returned, through <c>Dispatcher.BeginInvoke</c>: a window
        /// opened inside one shows but takes no input (spike 081 round 3).
        /// </summary>
        public static void ShowModeless(Window window, string? headerIconName = null, string? stateKey = null)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            Prepare(window, headerIconName, null, stateKey);
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window);
            window.Show();
        }

        /// <summary>Owner, theme, header icon, screen fit and remembered state, for a dialog or a modeless window.</summary>
        private static void Prepare(Window window, string? headerIconName, Window? owner, string? stateKey)
        {
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

            // Read before the style is applied: the XAML's own ResizeMode says whether the window is resizable.
            bool resizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

            window.ShowInTaskbar = false;
            UiTheme theme = ApplyTheme(window, resizable);
            if (headerIconName != null)
                SetHeaderIcon(window, headerIconName, theme);

            IntPtr screenHandle = owner != null
                ? new WindowInteropHelper(owner).Handle
                : new IntPtr(hwnd ?? 0);
            if (resizable)
                KeepToCloseButtonOnly(window);

            IReadOnlyList<ScreenArea>? screens = FitToScreen(window, screenHandle, resizable);
            if (stateKey != null)
                Remember(window, stateKey, resizable, screens);
        }

        /// <summary>
        /// Applies what <paramref name="window"/> remembered under <paramref name="key"/> (collapsed sections, and the
        /// placement of a resizable window), and saves them again when it closes, whether by Save, Cancel or the close
        /// button. A file that cannot be read or written is logged once a session; the window opens as usual.
        /// </summary>
        private static void Remember(Window window, string key, bool resizable, IReadOnlyList<ScreenArea>? screens)
        {
            string? folder = AddinServices.DataFolder;
            if (folder == null)
                return;

            var store = new WindowStateStore(folder);
            RememberedWindow remembered = store.Load(out string? warning).For(key);
            if (warning != null)
                LogStateFailureOnce(warning);

            var collapsed = new HashSet<string>(remembered.CollapsedSections ?? new List<string>(), StringComparer.Ordinal);
            foreach (CollapsibleSection section in SectionsIn(window))
            {
                if (section.Name.Length > 0 && collapsed.Contains(section.Name))
                    section.IsExpanded = false;
            }

            if (resizable && screens != null)
            {
                WindowPlacement? placement = WindowFit.Restore(remembered.Placement, screens, window.MinWidth, window.MinHeight);
                if (placement != null)
                {
                    window.SizeToContent = SizeToContent.Manual;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = placement.Left;
                    window.Top = placement.Top;
                    window.Width = placement.Width;
                    window.Height = placement.Height;
                }
            }

            window.Closing += (_, _) =>
            {
                var toSave = new RememberedWindow
                {
                    CollapsedSections = SectionsIn(window)
                        .Where(s => !s.IsExpanded && s.Name.Length > 0)
                        .Select(s => s.Name)
                        .ToList(),
                };

                if (resizable)
                {
                    // RestoreBounds is the normal size and position. Dialogs are never maximised, so that is all.
                    Rect bounds = window.RestoreBounds;
                    if (!bounds.IsEmpty)
                        toSave.Placement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, Maximized: false);
                }

                try
                {
                    store.Save(key, toSave);
                }
                catch (Exception ex)
                {
                    LogStateFailureOnce($"Could not save '{store.FilePath}': {ex.Message}");
                }
            };
        }

        /// <summary>Every <see cref="CollapsibleSection"/> in the window's logical tree, in order.</summary>
        private static IEnumerable<CollapsibleSection> SectionsIn(DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is not DependencyObject element)
                    continue;
                if (element is CollapsibleSection section)
                    yield return section;
                foreach (CollapsibleSection inner in SectionsIn(element))
                    yield return inner;
            }
        }

        private static void LogStateFailureOnce(string reason)
        {
            if (_stateFailureLogged)
                return;

            _stateFailureLogged = true;
            AddinServices.Log.Warn($"Window sizes and collapsed sections are not remembered. {reason}");
        }

        /// <summary>
        /// A resizable window keeps its resizing edges but shows only the close button, as every dialog does: WPF gives
        /// a resizable window minimise and maximise buttons, which are taken off here. Windows can still maximise a
        /// window by snapping it to the top of the screen or with Win+Up, so a maximised window is put straight back.
        /// Failing to remove the buttons leaves them and is not logged.
        /// </summary>
        private static void KeepToCloseButtonOnly(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                int style = GetWindowLong(handle, GwlStyle);
                if (style != 0)
                    SetWindowLong(handle, GwlStyle, style & ~(WsMinimizeBox | WsMaximizeBox));
            }
            catch (Exception)
            {
                // Cosmetic only.
            }

            window.StateChanged += (_, _) =>
            {
                if (window.WindowState != WindowState.Normal)
                    window.WindowState = WindowState.Normal;
            };
        }

        /// <summary>
        /// Keeps the window inside the working area of the screen that <paramref name="screenHandle"/> (the owner) is
        /// on, as it opens and whenever it changes size (windows size to their content). With no handle the primary
        /// screen is used. A fixed-size window's height is capped at the working area. A resizable window is not capped,
        /// so it can be resized freely by its edges; instead, while it still sizes to its content, a height taller than
        /// the working area is cut to it (the body then scrolls). Returns every screen's working area, for restoring a
        /// remembered placement, or null when the screens could not be read; that failure is logged once per session.
        /// </summary>
        private static IReadOnlyList<ScreenArea>? FitToScreen(Window window, IntPtr screenHandle, bool resizable)
        {
            Rect workArea;
            var screens = new List<ScreenArea>();
            try
            {
                System.Drawing.Rectangle pixels = System.Windows.Forms.Screen.FromHandle(screenHandle).WorkingArea;

                // The working area is in device pixels; the window's sizes are in WPF units at the window's own scale.
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                Matrix fromDevice = HwndSource.FromHwnd(handle)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                Rect ToUnits(System.Drawing.Rectangle r) => new Rect(
                    fromDevice.Transform(new Point(r.Left, r.Top)),
                    fromDevice.Transform(new Point(r.Right, r.Bottom)));

                workArea = ToUnits(pixels);
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                {
                    Rect area = ToUnits(screen.WorkingArea);
                    screens.Add(new ScreenArea(area.Left, area.Top, area.Width, area.Height));
                }
            }
            catch (Exception ex)
            {
                if (!_screenFailureLogged)
                {
                    _screenFailureLogged = true;
                    AddinServices.Log.Warn($"Could not read the screen's working area; windows are not kept on screen. {ex.Message}");
                }
                return null;
            }

            double? maxHeight = WindowFit.MaxHeight(workArea.Height);
            if (maxHeight.HasValue && !resizable)
                window.MaxHeight = Math.Min(window.MaxHeight, maxHeight.Value);

            void KeepOnScreen()
            {
                if (window.WindowState != WindowState.Normal)
                    return;

                if (resizable && maxHeight.HasValue && window.SizeToContent != SizeToContent.Manual
                    && window.ActualHeight > maxHeight.Value)
                {
                    window.SizeToContent = SizeToContent.Manual;
                    window.Height = maxHeight.Value;
                }

                double? top = WindowFit.ClampTop(window.Top, window.ActualHeight, workArea.Top, workArea.Bottom);
                if (top.HasValue)
                    window.Top = top.Value;
            }

            window.Loaded += (_, _) => KeepOnScreen();
            window.SizeChanged += (_, _) =>
            {
                // Once the user has resized a resizable window it no longer sizes to its content (WPF switches
                // SizeToContent off), and where they put it is left alone.
                if (!resizable || window.SizeToContent != SizeToContent.Manual)
                    KeepOnScreen();
            };
            return screens;
        }

        /// <summary>
        /// Merges the colour set for Inventor's theme and the shared styles into the window's resources and gives the
        /// window the dialog style. The window is already built, so it refers to shared styles with DynamicResource.
        /// If the dictionaries fail to load, the window keeps the default WPF look (the error is logged).
        /// Returns the theme used.
        /// </summary>
        private static UiTheme ApplyTheme(Window window, bool resizable)
        {
            UiTheme theme = ThemeResources.ReadInventorTheme();

            if (ThemeResources.TryMergeInto(window.Resources, theme))
                window.SetResourceReference(FrameworkElement.StyleProperty,
                    resizable ? ThemeResources.ResizableDialogWindowStyleKey : ThemeResources.DialogWindowStyleKey);

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

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
