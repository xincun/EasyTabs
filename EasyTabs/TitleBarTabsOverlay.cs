using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Win32Interop.Enums;
using Win32Interop.Methods;
using Win32Interop.Structs;

namespace EasyTabs
{
    public partial class TitleBarTabsOverlay : Form
    {
        /// <summary>
        /// All of the parent forms and their overlays so that we don't create duplicate overlays across the application domain
        /// </summary>
        protected static Dictionary<TitleBarTabs, TitleBarTabsOverlay> s_parents = new Dictionary<TitleBarTabs, TitleBarTabsOverlay>();

        /// <summary>
        /// Tab that has been torn off from this window and is being dragged
        /// </summary>
        protected static TitleBarTab s_tornTab;

        /// <summary>
        /// Thumbnail representation of <see cref="s_tornTab" /> used when dragging
        /// </summary>
        protected static TornTabForm s_tornTabForm;

        /// <summary>
        /// Flag used in <see cref="WndProc" /> and <see cref="MouseHookCallback" /> to track whether the user was click/dragging when a particular event occurred
        /// </summary>
        protected static bool s_wasDragging = false;

        /// <summary>
        /// Semaphore to control access to <see cref="s_tornTab" />
        /// </summary>
        protected static object s_tornTabLock = new object();

        /// <summary>
        /// Flag indicating whether or not <see cref="s_hookproc" /> has been installed as a hook
        /// </summary>
        protected static bool s_hookProcInstalled;

        /// <summary>
        /// Retrieves or creates the overlay for <paramref name="s_parentForm" />
        /// </summary>
        /// <param name="parentForm">Parent form that we are to create the overlay for</param>
        /// <returns>Newly-created or previously existing overlay for <paramref name="s_parentForm" /></returns>
        public static TitleBarTabsOverlay GetInstance(TitleBarTabs parentForm)
        {
            if (!s_parents.ContainsKey(parentForm))
                s_parents.Add(parentForm, new TitleBarTabsOverlay(parentForm));
            return s_parents[parentForm];
        }

        public TitleBarTabsOverlay(TitleBarTabs parentForm)
        {
            ParentForm = parentForm;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            AeroEnabled = ParentForm.IsCompositionEnabled;
            Show(parentForm);
            AttachHandlers();
        }

        /// <summary>
        /// Parent form for the overlay
        /// </summary>
        public new TitleBarTabs ParentForm { get; protected set; }

        /// <summary>
        /// Flag indicating whether or not the underlying window is active
        /// </summary>
        public bool Active { get; protected set; } = false;

        /// <summary>
        /// Flag indicating whether we should draw the titlebar background (i.e. we are in a non-Aero environment)
        /// </summary>
        public bool AeroEnabled { get; protected set; }

        /// <summary>
        /// When a tab is torn from the window, this is where we store the areas on all open windows where tabs can be dropped to combine the tab with that window
        /// </summary>
        protected Tuple<TitleBarTabs, Rectangle>[] DropAreas { get; set; }

        /// <summary>
        /// Pointer to the low-level mouse hook callback (<see cref="MouseHookCallback" />)
        /// </summary>
        protected IntPtr HookID { get; set; }

        /// <summary>
        /// Delegate of <see cref="MouseHookCallback" />; declared as a member variable to keep it from being garbage collected
        /// </summary>
        protected HOOKPROC HookProc { get; set; }

        /// <summary>
        /// Consumer thread for processing events in <see cref="MouseEvents" />
        /// </summary>
        protected Thread MouseEventsThread { get; set; }

        /// <summary>
        /// Index of the tab, if any, whose close button is being hovered over
        /// </summary>
        protected int IsOverCLoseButtonForTab { get; set; } = -1;

        /// <summary>
        /// Queue of mouse events reported by <see cref="HookProc" /> that need to be processed
        /// </summary>
        protected BlockingCollection<MouseEvent> MouseEvents { get; set; } = new BlockingCollection<MouseEvent>();

        /// <summary>
        /// Primary color for the titlebar background
        /// </summary>
        protected Color TitleBarColor
        {
            get
            {
                if (Application.RenderWithVisualStyles && Environment.OSVersion.Version.Major >= 6)
                    return Active ? SystemColors.GradientActiveCaption : SystemColors.GradientInactiveCaption;
                return Active ? SystemColors.ActiveCaption : SystemColors.InactiveCaption;
            }
        }

        /// <summary>
        /// Gradient color for the titlebar background
        /// </summary>
        protected Color TitleBarGradientColor
        {
            get
            {
                if (Active)
                    return SystemInformation.IsTitleBarGradientEnabled ? SystemColors.GradientActiveCaption : SystemColors.GradientInactiveCaption;
                return SystemInformation.IsTitleBarGradientEnabled ? SystemColors.GradientInactiveCaption : SystemColors.InactiveCaption;
            }
        }

        /// <summary>
        /// Screen area in which tabs can be dragged to and dropped for this window
        /// </summary>
        public Rectangle TabDropArea
        {
            get
            {
                User32.GetWindowRect(ParentForm.Handle, out RECT windowRectangle);
                return new Rectangle(windowRectangle.left + SystemInformation.HorizontalResizeBorderThickness,
                    windowRectangle.top + SystemInformation.VerticalResizeBorderThickness, ClientRectangle.Width,
                    ParentForm.NonClientAreaHeight - SystemInformation.VerticalResizeBorderThickness);
            }
        }

        /// <summary>
        /// Type of theme being used by the OS to render the desktop
        /// </summary>
        protected DisplayType DisplayType
        {
            get
            {
                if (AeroEnabled)
                    return DisplayType.Aero;
                if (Application.RenderWithVisualStyles && Environment.OSVersion.Version.Major >= 6)
                    return DisplayType.Basic;
                return DisplayType.Classic;
            }
        }

        /// <summary>
        /// Makes sure that the window is created with an <see cref="WS_EX.WS_EX_LAYERED" /> flag set so that it can be alpha-blended properly with the content (<see cref="ParentForm" />) underneath the overlay
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= (int)(WS_EX.WS_EX_LAYERED | WS_EX.WS_EX_NOACTIVATE);
                return createParams;
            }
        }

        /// <summary>
        /// Gets the relative location of the cursor within the overlay
        /// </summary>
        /// <param name="cursorPosition">Cursor position that represents the absolute position of the cursor on the screen</param>
        /// <returns>The relative location of the cursor within the overlay</returns>
        public Point GetRelativeCursorPosition(Point cursorPosition)
        {
            return new Point(cursorPosition.X - Location.X, cursorPosition.Y - Location.Y);
        }

        /// <summary>
        /// Renders the tabs and then calls <see cref="Win32Interop.Methods.User32.UpdateLayeredWindow" /> to blend the tab content with the underlying window (<see cref="ParentForm" />)
        /// </summary>
        /// <param name="forceRedraw">Flag indicating whether a full render should be forced</param>
        public void Render(bool forceRedraw = false)
        {
            Render(Cursor.Position, forceRedraw);
        }

        /// <summary>
        /// Renders the tabs and then calls <see cref="Win32Interop.Methods.User32.UpdateLayeredWindow" /> to blend the tab content with the underlying window (<see cref="ParentForm" />)
        /// </summary>
        /// <param name="cursorPosition">Current position of the cursor</param>
        /// <param name="forceRedraw">Flag indicating whether a full render should be forced</param>
        public void Render(Point cursorPosition, bool forceRedraw = false)
        {
            if (!IsDisposed && ParentForm.TabRenderer != null && ParentForm.WindowState != FormWindowState.Minimized && ParentForm.ClientRectangle.Width > 0)
            {
                cursorPosition = GetRelativeCursorPosition(cursorPosition);
                using (Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        DrawTitleBarBackground(graphics);

                        // Since classic mode themes draw over the *entire* titlebar, not just the area immediately behind the tabs, we have to offset the tabs when rendering in the window
                        Point offset = ParentForm.WindowState != FormWindowState.Maximized && DisplayType == DisplayType.Classic ? new Point(0, SystemInformation.CaptionButtonSize.Height) : ParentForm.WindowState != FormWindowState.Maximized ? new Point(0, SystemInformation.VerticalResizeBorderThickness - SystemInformation.BorderSize.Height) : new Point(0, 0);
                        ParentForm.TabRenderer.Render(ParentForm.Tabs, graphics, offset, cursorPosition, forceRedraw);
                        if (DisplayType == DisplayType.Classic && (ParentForm.ControlBox || ParentForm.MaximizeBox || ParentForm.MinimizeBox))
                        {
                            int boxWidth = 0;
                            if (ParentForm.ControlBox)
                                boxWidth += SystemInformation.CaptionButtonSize.Width;
                            if (ParentForm.MinimizeBox)
                                boxWidth += SystemInformation.CaptionButtonSize.Width;
                            if (ParentForm.MaximizeBox)
                                boxWidth += SystemInformation.CaptionButtonSize.Width;
                            CompositingMode oldCompositingMode = graphics.CompositingMode;
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.FillRectangle(new SolidBrush(Color.Transparent), Width - boxWidth, 0, boxWidth, SystemInformation.CaptionButtonSize.Height);
                            graphics.CompositingMode = oldCompositingMode;
                        }
                        IntPtr screenDc = User32.GetDC(IntPtr.Zero);
                        IntPtr memDc = Gdi32.CreateCompatibleDC(screenDc);
                        IntPtr oldBitmap = IntPtr.Zero;
                        IntPtr bitmapHandle = IntPtr.Zero;
                        try
                        {
                            // Copy the contents of the bitmap into memDc
                            bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
                            oldBitmap = Gdi32.SelectObject(memDc, bitmapHandle);

                            SIZE size = new SIZE { cx = bitmap.Width, cy = bitmap.Height };
                            POINT pointSource = new POINT { x = 0, y = 0 };
                            POINT topPos = new POINT { x = Left, y = Top };
                            BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = Convert.ToByte((int)AC.AC_SRC_OVER), BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = Convert.ToByte((int)AC.AC_SRC_ALPHA) };
                            if (!User32.UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW.ULW_ALPHA))
                            {
                                int error = Marshal.GetLastWin32Error();
                                throw new Win32Exception(error, "Error while calling UpdateLayeredWindow().");
                            }
                        }
                        finally
                        {
                            User32.ReleaseDC(IntPtr.Zero, screenDc);
                            if (bitmapHandle != IntPtr.Zero)
                            {
                                Gdi32.SelectObject(memDc, oldBitmap);
                                Gdi32.DeleteObject(bitmapHandle);
                            }
                            Gdi32.DeleteDC(memDc);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attaches the various event handlers to <see cref="ParentForm" /> so that the overlay is moved in synchronization to <see cref="ParentForm" />
        /// </summary>
        protected void AttachHandlers()
        {
            ParentForm.Closing += ParentFormOnClosing;
            ParentForm.Disposed += ParentFormOnDisposed;
            ParentForm.Deactivate += ParentFormOnDeactivate;
            ParentForm.Activated += ParentFormOnActivated;
            ParentForm.SizeChanged += ParentFormOnRefresh;
            ParentForm.Shown += ParentFormOnRefresh;
            ParentForm.VisibleChanged += ParentFormOnRefresh;
            ParentForm.Move += ParentFormOnRefresh;
            ParentForm.SystemColorsChanged += ParentFormOnSystemColorsChanged;
            if (HookProc == null)
            {
                MouseEventsThread = new Thread(InterpretMouseEvents);
                MouseEventsThread.Name = "Low Level mouse hooks processing thread";
                MouseEventsThread.Start();
                using (Process curProcess = Process.GetCurrentProcess())
                {
                    using (ProcessModule curModule = curProcess.MainModule)
                    {
                        HookProc = MouseHookCallBack;
                        HookID = User32.SetWindowsHookEx(WH.WH_MOUSE_LL, HookProc,
                            Kernel32.GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the position of the overlay window to match that of <see cref="ParentForm" /> so that it moves in tandem with it
        /// </summary>
        protected void OnPosition()
        {
            if (!IsDisposed)
            {
                // 92 is SM_CXPADDEDBORDER, which returns the amount of extra border padding around captioned windows
                int borderPadding = DisplayType == DisplayType.Classic ? 0 : User32.GetSystemMetrics(92);
                // If the form is in a non-maximized state, we position the tabs below the minimize/maximize/close buttons
                Top = ParentForm.Top + (DisplayType == DisplayType.Classic ? SystemInformation.VerticalResizeBorderThickness : ParentForm.WindowState == FormWindowState.Maximized ? SystemInformation.VerticalResizeBorderThickness + borderPadding : SystemInformation.CaptionHeight + borderPadding);
                Left = ParentForm.Left + SystemInformation.HorizontalResizeBorderThickness - SystemInformation.BorderSize.Width + borderPadding;
                Width = ParentForm.Width - ((SystemInformation.VerticalResizeBorderThickness + borderPadding) * 2) + (SystemInformation.BorderSize.Width * 2);
                Height = ParentForm.TabRenderer.TabHeight + (DisplayType == DisplayType.Classic && ParentForm.WindowState != FormWindowState.Maximized ? SystemInformation.CaptionButtonSize.Height : 0);
                Render();
            }
        }

        /// <summary>
        /// Consumer method that processes mouse events in <see cref="MouseEvents" /> that are recorded by <see cref="MouseHookCallback" />
        /// </summary>
        protected void InterpretMouseEvents()
        {
            foreach (MouseEvent mouseEvent in MouseEvents.GetConsumingEnumerable())
            {
                int nCode = mouseEvent.nCode;
                IntPtr wParam = mouseEvent.wParam;
                MSLLHOOKSTRUCT? hookStruct = mouseEvent.MouseData;
                if (nCode >= 0 && (int)WM.WM_MOUSEMOVE == (int)wParam)
                {
                    Point cursorPosition = new Point(hookStruct.Value.pt.x, hookStruct.Value.pt.y);
                    bool reRender = false;
                    if (s_tornTab != null && DropAreas != null)
                    {
                        for (int i = 0; i < DropAreas.Length; i++)
                        {
                            if (DropAreas[i].Item2.Contains(cursorPosition)) // If the cursor is within the drop area, combine the tab for the window that belongs to that drop area
                            {
                                TitleBarTab tabToCombine = null;
                                lock (s_tornTabLock)
                                {
                                    if (s_tornTab != null)
                                    {
                                        tabToCombine = s_tornTab;
                                        s_tornTab = null;
                                    }
                                }

                                if (tabToCombine != null)
                                {
                                    int i1 = i;
                                    Invoke(new Action(() =>
                                    {
                                        DropAreas[i1].Item1.TabRenderer.CombineTab(tabToCombine, cursorPosition);
                                        tabToCombine = null;
                                        s_tornTabForm.Close();
                                        s_tornTabForm = null;
                                        if (ParentForm.Tabs.Count == 0)
                                            ParentForm.Close();
                                    }));
                                }
                            }
                        }
                    }
                    else if (!ParentForm.TabRenderer.IsTabRepositioning)
                    {
                        // If we were over a close button previously, check to see if the cursor is still over that tab's close button; if not, re-render
                        if (IsOverCLoseButtonForTab != -1 && (IsOverCLoseButtonForTab >= ParentForm.Tabs.Count || !ParentForm.TabRenderer.IsOverCloseButton(ParentForm.Tabs[IsOverCLoseButtonForTab], GetRelativeCursorPosition(cursorPosition))))
                        {
                            reRender = true;
                            IsOverCLoseButtonForTab = -1;
                        }
                        else // Otherwise, see if any tabs' close button is being hovered over
                        {
                            for (int i = 0; i < ParentForm.Tabs.Count; i++)
                            {
                                if (ParentForm.TabRenderer.IsOverCloseButton(ParentForm.Tabs[i], GetRelativeCursorPosition(cursorPosition)))
                                {
                                    IsOverCLoseButtonForTab = i;
                                    reRender = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        Invoke(new Action(() =>
                        {
                            s_wasDragging = true;
                            Rectangle dragArea = TabDropArea;
                            dragArea.Inflate(ParentForm.TabRenderer.TabTearDragDistance, ParentForm.TabRenderer.TabTearDragDistance);
                            if (!dragArea.Contains(cursorPosition) && s_tornTab == null)
                            {
                                lock (s_tornTabLock)
                                {
                                    if (s_tornTab == null)
                                    {
                                        ParentForm.TabRenderer.IsTabRepositioning = false;
                                        s_tornTab = ParentForm.SelectedTab;
                                        s_tornTab.ClearEventSubscriptions();
                                        s_tornTabForm = new TornTabForm(s_tornTab, ParentForm.TabRenderer);
                                    }
                                }

                                if (s_tornTab != null)
                                {
                                    ParentForm.SelectedTabIndex = ParentForm.SelectedTabIndex == ParentForm.Tabs.Count - 1 ? ParentForm.SelectedTabIndex - 1 : ParentForm.SelectedTabIndex + 1;
                                    ParentForm.Tabs.Remove(s_tornTab);
                                    if (ParentForm.Tabs.Count == 0)
                                        ParentForm.Hide();
                                    s_tornTabForm.Show();
                                    DropAreas = (from window in ParentForm.ApplicationContext.OpenWindows.Where(w => w.Tabs.Count > 0) select new Tuple<TitleBarTabs, Rectangle>(window, window.TabDropArea)).ToArray();
                                }
                            }
                        }));
                    }
                    Invoke(new Action(() => OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, cursorPosition.X, cursorPosition.Y, 0))));
                    if (ParentForm.TabRenderer.IsTabRepositioning)
                        reRender = true;
                    if (reRender)
                        Invoke(new Action(() => Render(cursorPosition, true)));
                }
                else if (nCode >= 0 && (int)WM.WM_LBUTTONDOWN == (int)wParam)
                    s_wasDragging = false;
                else if (nCode >= 0 && (int)WM.WM_LBUTTONUP == (int)wParam)
                {
                    if (s_tornTab != null)
                    {
                        TitleBarTab tabToRelease = null;
                        lock (s_tornTabLock)
                        {
                            if (s_tornTab != null)
                            {
                                tabToRelease = s_tornTab;
                                s_tornTab = null;
                            }
                        }

                        if (tabToRelease != null)
                        {
                            Invoke(new Action(() =>
                            {
                                TitleBarTabs newWindow = (TitleBarTabs)Activator.CreateInstance(ParentForm.GetType());
                                if (newWindow.WindowState == FormWindowState.Maximized)
                                {
                                    Screen screen = Screen.AllScreens.First(s => s.WorkingArea.Contains(Cursor.Position));
                                    newWindow.StartPosition = FormStartPosition.Manual;
                                    newWindow.WindowState = FormWindowState.Normal;
                                    newWindow.Left = screen.WorkingArea.Left;
                                    newWindow.Top = screen.WorkingArea.Top;
                                    newWindow.Width = screen.WorkingArea.Width;
                                    newWindow.Height = screen.WorkingArea.Height;
                                }
                                else
                                {
                                    newWindow.Left = Cursor.Position.X;
                                    newWindow.Top = Cursor.Position.Y;
                                }
                                tabToRelease.Parent = newWindow;
                                ParentForm.ApplicationContext.OpenWindow(newWindow);
                                newWindow.Show();
                                newWindow.Tabs.Add(tabToRelease);
                                newWindow.SelectedTabIndex = 0;
                                newWindow.ResizeTabContents();
                                s_tornTabForm.Close();
                                s_tornTabForm = null;
                                if (ParentForm.Tabs.Count == 0)
                                    ParentForm.Close();
                            }));
                        }
                    }
                    Invoke(new Action(() => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, Cursor.Position.X, Cursor.Position.Y, 0))));
                }
            }
        }

        /// <summary>
        /// Hook callback to process <see cref="Win32Interop.Enums.WM.WM_MOUSEMOVE" /> messages to highlight/un-highlight the close button on each tab
        /// </summary>
        /// <param name="nCode">The message being received</param>
        /// <param name="wParam">Additional information about the message</param>
        /// <param name="lParam">Additional information about the message</param>
        /// <returns>A zero value if the procedure processes the message; a nonzero value if the procedure ignores the message</returns>
        private IntPtr MouseHookCallBack(int nCode, IntPtr wParam, IntPtr lParam)
        {
            MouseEvent mouseEvent = new MouseEvent();
            mouseEvent.nCode = nCode;
            mouseEvent.wParam = wParam;
            mouseEvent.lParam = lParam;
            if (nCode >= 0 && (int)WM.WM_MOUSEMOVE == (int)wParam)
                mouseEvent.MouseData = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            MouseEvents.Add(mouseEvent);
            return User32.CallNextHookEx(HookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Draws the titlebar background behind the tabs if Aero glass is not enabled
        /// </summary>
        /// <param name="graphics">Graphics context with which to draw the background</param>
        protected virtual void DrawTitleBarBackground(Graphics graphics)
        {
            if (DisplayType == DisplayType.Aero)
                return;
            Rectangle fillArea;
            if (DisplayType == DisplayType.Basic)
                fillArea = new Rectangle(new Point(1, Top == 0 ? SystemInformation.CaptionHeight - 1 : (SystemInformation.CaptionHeight + SystemInformation.VerticalResizeBorderThickness) - (Top - ParentForm.Top) - 1), new Size(Width - 2, ParentForm.Padding.Top));
            else
                fillArea = new Rectangle(new Point(1, 0), new Size(Width - 2, Height - 1));
            if (fillArea.Height <= 0)
                return;
            int rightMargin = 3;
            if (ParentForm.ControlBox && ParentForm.MinimizeBox)
                rightMargin += SystemInformation.CaptionButtonSize.Width;
            if (ParentForm.ControlBox && ParentForm.MaximizeBox)
                rightMargin += SystemInformation.CaptionButtonSize.Width;
            if (ParentForm.ControlBox)
                rightMargin += SystemInformation.CaptionButtonSize.Width;
            LinearGradientBrush gradient = new LinearGradientBrush(new Point(24, 0), new Point(fillArea.Width - rightMargin + 1, 0), TitleBarColor, TitleBarGradientColor);
            using (BufferedGraphics bufferedGraphics = BufferedGraphicsManager.Current.Allocate(graphics, fillArea))
            {
                bufferedGraphics.Graphics.FillRectangle(new SolidBrush(TitleBarColor), fillArea);
                bufferedGraphics.Graphics.FillRectangle(new SolidBrush(TitleBarGradientColor), new Rectangle(new Point(fillArea.Location.X + fillArea.Width - rightMargin, fillArea.Location.Y), new Size(rightMargin, fillArea.Height)));
                bufferedGraphics.Graphics.FillRectangle(gradient, new Rectangle(fillArea.Location, new Size(fillArea.Width - rightMargin, fillArea.Height)));
                bufferedGraphics.Graphics.FillRectangle(new SolidBrush(TitleBarColor), new Rectangle(fillArea.Location, new Size(24, fillArea.Height)));
                bufferedGraphics.Render(graphics);
            }
        }

        /// <summary>
        /// Overrides the message pump for the window so that we can respond to click events on the tabs themselves
        /// </summary>
        /// <param name="m">Message received by the pump</param>
        protected override void WndProc(ref Message m)
        {
            switch ((WM)m.Msg)
            {
                case WM.WM_NCLBUTTONDOWN:
                case WM.WM_LBUTTONDOWN:
                    Point relativeCursorPosition = GetRelativeCursorPosition(Cursor.Position);
                    // If we were over a tab, set the capture state for the window so that we'll actually receive a WM_LBUTTONUP message
                    if (ParentForm.TabRenderer.IsOverTab(ParentForm.Tabs, relativeCursorPosition) == null && !ParentForm.TabRenderer.IsOverAddButton(relativeCursorPosition))
                        ParentForm.ForwardMessage(ref m);
                    else
                    {
                        TitleBarTab clickedTab = ParentForm.TabRenderer.IsOverTab(ParentForm.Tabs, relativeCursorPosition);
                        if (clickedTab != null)
                        {
                            // If the user clicked the close button, remove the tab from the list
                            if (!ParentForm.TabRenderer.IsOverCloseButton(clickedTab, relativeCursorPosition))
                            {
                                ParentForm.ResizeTabContents(clickedTab);
                                ParentForm.SelectedTabIndex = ParentForm.Tabs.IndexOf(clickedTab);
                                Render();
                            }
                            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, Cursor.Position.X, Cursor.Position.Y, 0));
                        }
                        ParentForm.Activate();
                    }
                    break;
                case WM.WM_LBUTTONDBLCLK:
                    ParentForm.ForwardMessage(ref m);
                    break;
                // We always return HTCAPTION for the hit test message so that the underlying window doesn't have its focus removed
                case WM.WM_NCHITTEST:
                    m.Result = new IntPtr((int)HT.HTCAPTION);
                    break;
                case WM.WM_LBUTTONUP:
                case WM.WM_NCLBUTTONUP:
                case WM.WM_MBUTTONUP:
                case WM.WM_NCMBUTTONUP:
                    Point relativeCursorPosition2 = GetRelativeCursorPosition(Cursor.Position);
                    if (ParentForm.TabRenderer.IsOverTab(ParentForm.Tabs, relativeCursorPosition2) == null && !ParentForm.TabRenderer.IsOverAddButton(relativeCursorPosition2))
                        ParentForm.ForwardMessage(ref m);
                    else
                    {
                        TitleBarTab clickedTab = ParentForm.TabRenderer.IsOverTab(ParentForm.Tabs, relativeCursorPosition2);
                        if (clickedTab != null)
                        {
                            // If the user clicks the middle button/scroll wheel over a tab, close it
                            if ((WM)m.Msg == WM.WM_MBUTTONUP || (WM)m.Msg == WM.WM_NCMBUTTONUP)
                            {
                                clickedTab.Content.Close();
                                Render();
                            }
                            else
                            {
                                // If the user clicked the close button, remove the tab from the list
                                if (ParentForm.TabRenderer.IsOverCloseButton(clickedTab, relativeCursorPosition2))
                                {
                                    clickedTab.Content.Close();
                                    Render();
                                }
                                else
                                    ParentForm.OnTabClicked(new TitleBarTabEventArgs(TabControlAction.Selected, clickedTab, ParentForm.SelectedTabIndex, s_wasDragging));
                            }
                        }
                        // Otherwise, if the user clicked the add button, call CreateTab to add a new tab to the list and select it
                        else if (ParentForm.TabRenderer.IsOverAddButton(relativeCursorPosition2))
                            ParentForm.AddNewTab();
                        if ((WM)m.Msg == WM.WM_LBUTTONUP || (WM)m.Msg == WM.WM_NCLBUTTONUP)
                            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, Cursor.Position.X, Cursor.Position.Y, 0));
                    }
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" />'s <see cref="System.Windows.Forms.Control.SystemColorsChanged" /> event is fired which re-renders the tabs
        /// </summary>
        /// <param name="sender">Object from which the event originated</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ParentFormOnSystemColorsChanged(object sender, EventArgs e)
        {
            AeroEnabled = ParentForm.IsCompositionEnabled;
            OnPosition();
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" />'s <see cref="System.Windows.Forms.Control.SizeChanged" />, <see cref="System.Windows.Forms.Control.VisibleChanged" />, or <see cref="System.Windows.Forms.Control.Move" /> events are fired which re-renders the tabs
        /// </summary>
        /// <param name="sender">Object from which the event originated</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ParentFormOnRefresh(object sender, EventArgs e)
        {
            if (ParentForm.WindowState == FormWindowState.Minimized)
                Visible = false;
            else
                OnPosition();
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" />'s <see cref="System.Windows.Forms.Form.Activated" /> event is fired
        /// </summary>
        /// <param name="sender">Object from which this event originated</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ParentFormOnActivated(object sender, EventArgs e)
        {
            Active = true;
            Render();
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" />'s <see cref="System.Windows.Forms.Form.Deactivate" /> event is fired
        /// </summary>
        /// <param name="sender">Object from which this event originated</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ParentFormOnDeactivate(object sender, EventArgs e)
        {
            Active = false;
            Render();
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" /> is in the process of closing.  This uninstalls <see cref="HookProc" /> from the low-level hooks list and stops the consumer thread that processes those events
        /// </summary>
        /// <param name="sender">Object from which this event originated, <see cref="ParentForm" /> in this case</param>
        /// <param name="e">Arguments associated with this event</param>
        private void ParentFormOnClosing(object sender, CancelEventArgs e)
        {
            TitleBarTabs form = (TitleBarTabs)sender;
            if (form == null)
                return;
            if (s_parents.ContainsKey(form))
                s_parents.Remove(form);
            User32.UnhookWindowsHookEx(HookID);
            MouseEvents.CompleteAdding();
            MouseEventsThread.Abort();
        }

        /// <summary>
        /// Event handler that is called when <see cref="ParentForm" />'s <see cref="System.ComponentModel.Component.Disposed" /> event is fired
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParentFormOnDisposed(object sender, EventArgs e)
        {

        }
    }
}