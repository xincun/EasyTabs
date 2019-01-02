using EasyTabs.Renderer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Win32Interop.Enums;
using Win32Interop.Methods;
using Win32Interop.Structs;

namespace EasyTabs
{
    /// <summary>
    /// Contains a semi-transparent window with a thumbnail of a tab that has been torn away from its parent window. This thumbnail will follow the cursor around as it's dragged around the screen
    /// </summary>
    public partial class TornTabForm : Form
    {
        public TornTabForm(TitleBarTab tab, BaseTabRenderer tabRenderer)
        {
            LayeredWindow = new LayeredWindow();
            Initialized = false;
            SetStyle(ControlStyles.DoubleBuffer, true);
            Opacity = 0.70;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            AllowTransparency = true;

            Disposed += OnDisposed;

            Bitmap tabContents = tab.GetThumbnail();
            Bitmap contentsAndTab = new Bitmap(tabContents.Width, tabContents.Height + tabRenderer.TabHeight,
                tabContents.PixelFormat);
            Graphics tabGraphics = Graphics.FromImage(contentsAndTab);

            tabGraphics.DrawImage(tabContents, 0, tabRenderer.TabHeight);

            bool oldShowAddButton = tabRenderer.ShowAddButton;
            tabRenderer.ShowAddButton = false;
            tabRenderer.Render(new List<TitleBarTab> { tab }, tabGraphics, new Point(0, 0), new Point(0, 0), true);
            tabRenderer.ShowAddButton = oldShowAddButton;

            TabThumbnail = new Bitmap(contentsAndTab.Width / 2, contentsAndTab.Height / 2, contentsAndTab.PixelFormat);
            Graphics thumbnailGraphics = Graphics.FromImage(TabThumbnail);
            thumbnailGraphics.InterpolationMode = InterpolationMode.High;
            thumbnailGraphics.CompositingQuality = CompositingQuality.HighQuality;
            thumbnailGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            thumbnailGraphics.DrawImage(contentsAndTab, 0, 0, TabThumbnail.Width, TabThumbnail.Height);

            Width = TabThumbnail.Width - 1;
            Height = TabThumbnail.Height - 1;
            CursorOffset = new Point(tabRenderer.TabContentWidth / 4, tabRenderer.TabHeight / 4);
            SetWindowPosition(Cursor.Position);
        }

        /// <summary>
        /// Window that contains the actual thumbnail image data
        /// </summary>
        private LayeredWindow LayeredWindow { get; }

        /// <summary>
        /// Offset of the cursor within the torn tab representation while dragging
        /// </summary>
        private Point CursorOffset { get; set; }

        /// <summary>
        /// Pointer to the low-level mouse hook callback (<see cref="MouseHookCallBack" />)
        /// </summary>
        private IntPtr HookID { get; set; }

        /// <summary>
        /// Flag indicating whether or not the constructor has finished running
        /// </summary>
        private bool Initialized { get; set; }

        /// <summary>
        /// Flag indicating whether <see cref="HookProc" /> is installed
        /// </summary>
        private bool HookInstalled { get; set; } = false;

        /// <summary>
        /// Delegate of <see cref="MouseHookCallBack" />; declared as a member variable to keep it from being garbage collected
        /// </summary>
        private HOOKPROC HookProc { get; set; }

        /// <summary>
        /// Thumbnail of the tab we are dragging
        /// </summary>
        private Bitmap TabThumbnail { get; set; }

        /// <summary>
        /// Calls <see cref="EasyTabs.LayeredWindow.UpdateWindow" /> to update the position of the thumbnail and blend it properly with the underlying desktop elements
        /// </summary>
        private void UpdateLayeredBackground()
        {
            if (TabThumbnail == null || !Initialized)
                return;
            byte opacity = (byte)(Opacity * 255);
            LayeredWindow.UpdateWindow(TabThumbnail, opacity, Width, Height,
                new POINT { x = Location.X, y = Location.Y });
        }

        /// <summary>
        /// Updates the window position to keep up with the cursor's movement
        /// </summary>
        /// <param name="cursorPosition">Current position of the cursor</param>
        private void SetWindowPosition(Point cursorPosition)
        {
            Left = cursorPosition.X - CursorOffset.X;
            Top = cursorPosition.Y - CursorOffset.Y;
            UpdateLayeredBackground();
        }

        /// <summary>
        /// Hook callback to process <see cref="Win32Interop.Enums.WM.WM_MOUSEMOVE" /> messages to move the thumbnail along with the cursor
        /// </summary>
        /// <param name="nCode">The message being received</param>
        /// <param name="wParam">Additional information about the message</param>
        /// <param name="lParam">Additional information about the message</param>
        /// <returns>A zero value if the procedure processes the message; a nonzero value if the procedure ignores the message</returns>
        protected IntPtr MouseHookCallBack(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)WM.WM_MOUSEMOVE == (int)wParam)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                Point cursorPosition = new Point(hookStruct.pt.x, hookStruct.pt.y);
                SetWindowPosition(cursorPosition);
            }

            return User32.CallNextHookEx(HookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Event handler that's called when the window is loaded; shows <see cref="_layeredWindow" /> and installs the mouse hook via <see cref="Win32Interop.Methods.User32.SetWindowsHookEx" />
        /// </summary>
        /// <param name="e">Arguments associated with this event</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Initialized = true;
            UpdateLayeredBackground();
            LayeredWindow.Show();
            LayeredWindow.Enabled = false;
            if (!HookInstalled)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                {
                    using (ProcessModule curModule = curProcess.MainModule)
                    {
                        HookProc = MouseHookCallBack;
                        HookID = User32.SetWindowsHookEx(WH.WH_MOUSE_LL, HookProc,
                            Kernel32.GetModuleHandle(curModule.ModuleName), 0);
                    }
                }

                HookInstalled = true;
            }
        }

        /// <summary>
        /// Event handler that is called when the window is closing; closes <see cref="LayeredWindow" /> as well
        /// </summary>
        /// <param name="e">Arguments associated with this event</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            LayeredWindow.Close();
        }

        /// <summary>
        /// Event handler that's called from <see cref="System.IDisposable.Dispose" />; calls <see cref="Win32Interop.Methods.User32.UnhookWindowsHookEx" /> to unsubscribe from the mouse hook
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnDisposed(object sender, EventArgs e)
        {
            User32.UnhookWindowsHookEx(HookID);
        }
    }
}