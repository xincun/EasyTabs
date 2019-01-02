using EasyTabs.EventList;
using EasyTabs.Renderer;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Win32Interop.Enums;
using Win32Interop.Methods;
using Win32Interop.Structs;

namespace EasyTabs
{
    /// <summary>
    /// Base class that contains the functionality to render tabs within a WinForms application's title bar area. This  is done through a borderless overlay window (<see cref="_overlay" />) rendered on top of the non-client area at the top of this window.  All an implementing class will need to do is set the <see cref="TabRenderer" /> property and begin adding tabs to <see cref="Tabs" />
    /// </summary>
    public abstract partial class TitleBarTabs : Form
    {
        private bool m_areroPeekEnabled = true;

        private BaseTabRenderer m_baseTabRenderer;

        public TitleBarTabs()
        {
            PreviousWindowState = null;
            ExitOnLastTabClose = true;
            InitializeComponent();
            SetWindowThemeAttributes(WTNCA.NODRAWCAPTION | WTNCA.NODRAWICON);
            Tabs.CollectionModified += TabsCollectionModified;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// Borderless window that is rendered over top of the non-client area of this window
        /// </summary>
        protected internal TitleBarTabsOverlay Overlay { get; set; }

        /// <summary>
        /// Maintains the previous window state so that we can respond properly to maximize/restore events in <see cref="OnSizeChanged" />
        /// </summary>
        protected FormWindowState? PreviousWindowState { get; set; }

        /// <summary>
        /// When switching between tabs, this keeps track of the tab that was previously active so that, when it is switched away from, we can generate a fresh Aero Peek preview image for it
        /// </summary>
        protected TitleBarTab PreviousActiveTab { get; set; } = null;

        /// <summary>
        /// The preview images for each tab used to display each tab when Aero Peek is activated
        /// </summary>
        protected Dictionary<Form, Bitmap> Previews { get; set; } = new Dictionary<Form, Bitmap>();

        /// <summary>
        /// List of tabs to display for this window
        /// </summary>
        public ListWithEvents<TitleBarTab> Tabs { get; protected set; } = new ListWithEvents<TitleBarTab>();

        /// <summary>
        /// Height of the non-client area at the top of the window
        /// </summary>
        public int NonClientAreaHeight { get; protected set; }

        /// <summary>
        /// Flag indicating whether the application itself should exit when the last tab is closed
        /// </summary>
        public bool ExitOnLastTabClose { get; set; }

        /// <summary>
        /// Flag indicating whether we are in the process of closing the window
        /// </summary>
        public bool IsClosing { get; set; }

        /// <summary>
        /// Application context under which this particular window runs
        /// </summary>
        public TitleBarTabsAppContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Area of the screen in which tabs can be dropped for this window
        /// </summary>
        public Rectangle TabDropArea => Overlay.TabDropArea;

        /// <summary>
        /// The tab that is currently selected by the user
        /// </summary>
        public TitleBarTab SelectedTab
        {
            get => Tabs.FirstOrDefault((TitleBarTab t) => t.Active);
            set => SelectedTabIndex = Tabs.IndexOf(value);
        }

        /// <summary>
        /// Flag indicating whether or not each tab has an Aero Peek entry allowing the user to switch between tabs from the taskbar
        /// </summary>
        public bool AeroPeekEnabled
        {
            get => m_areroPeekEnabled;
            set
            {
                m_areroPeekEnabled = value;
                if (!m_areroPeekEnabled)
                {
                    foreach (TitleBarTab tab in Tabs)
                        TaskbarManager.Instance.TabbedThumbnail.RemoveThumbnailPreview(tab.Content);
                    Previews.Clear();
                }
                else
                {
                    foreach (TitleBarTab tab in Tabs)
                        CreateThumbnailPreview(tab);
                    if (SelectedTab != null)
                        TaskbarManager.Instance.TabbedThumbnail.SetActiveTab(SelectedTab.Content);
                }
            }
        }

        /// <summary>
        /// The renderer to use when drawing the tabs
        /// </summary>
        public BaseTabRenderer TabRenderer
        {
            get => m_baseTabRenderer;
            set
            {
                m_baseTabRenderer = value;
                SetFrameSize();
            }
        }

        /// <summary>
        /// Flag indicating whether composition is enabled on the desktop
        /// </summary>
        internal bool IsCompositionEnabled
        {
            get
            {
                Dwmapi.DwmIsCompositionEnabled(out bool hasComposition);
                return hasComposition;
            }
        }

        /// <summary>
        /// Gets or sets the index of the tab that is currently selected by the user
        /// </summary>
        public int SelectedTabIndex // Todo: Rework this method, and all methods, with getting active tab etc
        {
            get => Tabs.FindIndex((TitleBarTab t) => t.Active);
            set
            {
                TitleBarTab selectedTab = SelectedTab;
                int selectedTabIndex = SelectedTabIndex;
                if (selectedTab != null && selectedTabIndex != value)
                {
                    TitleBarTabCancelEventArgs e = new TitleBarTabCancelEventArgs(TabControlAction.Deselecting, selectedTab, selectedTabIndex);
                    OnTabDeselecting(e);
                    if (e.Cancel)
                        return;
                    selectedTab.Active = false;
                    OnTabDeselected(new TitleBarTabEventArgs(TabControlAction.Deselected, selectedTab, selectedTabIndex, false));
                }
                if (value != -1)
                {
                    TitleBarTabCancelEventArgs e = new TitleBarTabCancelEventArgs(TabControlAction.Selecting, Tabs[value], value);
                    OnTabSelecting(e);
                    if (e.Cancel)
                        return;
                    Tabs[value].Active = true;
                    OnTabSelected(new TitleBarTabEventArgs(TabControlAction.Selected, Tabs[value], value, false));
                }
                Overlay?.Render();
            }
        }

        /// <summary>
        /// Resizes the <see cref="EasyTabs.TitleBarTab.Content" /> form of the <paramref name="tab" /> to match the size of the client area for this window
        /// </summary>
        /// <param name="tab">Tab whose <see cref="EasyTabs.TitleBarTab.Content" /> form we should resize; if not specified, we default to <see cref="SelectedTab" /></param>
        public void ResizeTabContents(TitleBarTab tab = null)
        {
            if (tab == null)
                tab = SelectedTab;
            if (tab != null)
            {
                tab.Content.Location = new System.Drawing.Point(0, Padding.Top - 1);
                tab.Content.Size = new System.Drawing.Size(ClientRectangle.Width, ClientRectangle.Height - Padding.Top + 1);
            }
        }

        /// <summary>
        /// Calls <see cref="EasyTabs.TitleBarTabsOverlay.Render(System.bool)"/> on <see cref="Overlay"/> to force a redrawing of the tabs
        /// </summary>
        public void RedrawTabs()
        {
            Overlay?.Render(true);
        }

        /// <summary>
        /// Calls <see cref="Uxtheme.SetWindowThemeAttribute" /> to set various attributes on the window
        /// </summary>
        /// <param name="attributes">Attributes to set on the window</param>
        private void SetWindowThemeAttributes(WTNCA attributes)
        {
            WTA_OPTIONS options = new WTA_OPTIONS { dwFlags = attributes, dwMask = WTNCA.VALIDBITS };
            Uxtheme.SetWindowThemeAttribute(Handle, WINDOWTHEMEATTRIBUTETYPE.WTA_NONCLIENT, ref options, (uint)Marshal.SizeOf(typeof(WTA_OPTIONS)));
        }

        /// <summary>
        /// Called when a <see cref="WM.WM_NCHITTEST" /> message is received to see where in the non-client area the user clicked
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private HT HitTest(Message m)
        {
            int lParam = (int)m.LParam;
            System.Drawing.Point point = new System.Drawing.Point(lParam & 0xffff, lParam >> 16);
            return HitTest(point, m.HWnd);
        }

        /// <summary>
        /// Called when a <see cref="WM.WM_NCHITTEST" /> message is received to see where in the non-client area the user clicked
        /// </summary>
        /// <param name="point">Screen location that we are to test</param>
        /// <param name="windowHandle">Handle to the window for which we are performing the test</param>
        /// <returns>One of the <see cref="Win32Interop.Enums.HT" /> values, depending on where the user clicked</returns>
        private HT HitTest(System.Drawing.Point point, IntPtr windowHandle)
        {
            RECT rect;
            User32.GetWindowRect(windowHandle, out rect);
            Rectangle area = new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            int row = 1;
            int column = 1;
            bool onResizeBorder = false;
            // Determine if we are on the top or bottom border
            if (point.Y >= area.Top && point.Y < area.Top + SystemInformation.VerticalResizeBorderThickness + NonClientAreaHeight - 2)
            {
                onResizeBorder = point.Y < (area.Top + SystemInformation.VerticalResizeBorderThickness);
                row = 0;
            }
            else if (point.Y < area.Bottom && point.Y > area.Bottom - SystemInformation.VerticalResizeBorderThickness)
                row = 2;
            // Determine if we are on the left border or the right border
            if (point.X >= area.Left && point.X < area.Left + SystemInformation.HorizontalResizeBorderThickness)
                column = 0;
            else if (point.X < area.Right && point.X >= area.Right - SystemInformation.HorizontalResizeBorderThickness)
                column = 2;
            HT[,] hitTests =
            {
                { onResizeBorder ? HT.HTTOPLEFT : HT.HTLEFT, onResizeBorder ? HT.HTTOP : HT.HTCAPTION, onResizeBorder ? HT.HTTOPRIGHT : HT.HTRIGHT },
                { HT.HTLEFT, HT.HTNOWHERE, HT.HTRIGHT },
                { HT.HTBOTTOMLEFT, HT.HTBOTTOM, HT.HTBOTTOMRIGHT }
            };
            return hitTests[row, column];
        }

        /// <summary>
        /// Forwards a message received by <see cref="EasyTabs.TitleBarTabsOverlay" /> to the underlying window
        /// </summary>
        /// <param name="m">Message received by the overlay</param>
        internal void ForwardMessage(ref Message m)
        {
            m.HWnd = Handle;
            WndProc(ref m);
        }

        /// <summary>
		/// When the window's state (maximized, minimized, or restored) changes, this sets the size of the non-client area at the top of the window properly so that the tabs can be displayed
        /// </summary>
        protected void SetFrameSize()
        {
            if (TabRenderer == null || WindowState == FormWindowState.Minimized)
                return;
            int topPadding = WindowState == FormWindowState.Maximized ? TabRenderer.TabHeight - SystemInformation.CaptionHeight : (TabRenderer.TabHeight + SystemInformation.CaptionButtonSize.Height) - SystemInformation.CaptionHeight;
            Padding = new Padding(Padding.Left, topPadding > 0 ? topPadding : 0, Padding.Right, Padding.Bottom);
            MARGINS margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyBottomHeight = 0, cyTopHeight = topPadding > 0 ? topPadding : 0 };
            Dwmapi.DwmExtendFrameIntoClientArea(Handle, ref margins);
            NonClientAreaHeight = SystemInformation.CaptionHeight + (topPadding > 0 ? topPadding : 0);
            if (AeroPeekEnabled)
            {
                foreach (TabbedThumbnail preview in Tabs.Select(tab => TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview(tab.Content)).Where(preview => preview != null))
                    preview.PeekOffset = new Vector(Padding.Left, Padding.Top - 1);
            }
        }

        /// <summary>
        /// Generate a new thumbnail image for <paramref name="tab" />
        /// </summary>
        /// <param name="tab">Tab that we need to generate a thumbnail for</param>
        protected void UpdateTabThumbnail(TitleBarTab tab)
        {
            TabbedThumbnail preview = TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview(tab.Content);
            if (preview == null)
                return;
            Bitmap bitmap = TabbedThumbnailScreenCapture.GrabWindowBitmap(tab.Content.Handle, tab.Content.Size);
            preview.SetImage(bitmap);
            if (Previews.ContainsKey(tab.Content) && Previews[tab.Content] != null)
                Previews[tab.Content].Dispose();
            Previews[tab.Content] = bitmap;
        }

        /// <summary>
		/// Creates a new thumbnail for <paramref name="tab" /> when the application is initially enabled for AeroPeek or when it is turned on sometime during execution
        /// </summary>
        /// <param name="tab">Tab that we are to create the thumbnail for</param>
        /// <returns>Thumbnail created for <paramref name="tab" /></returns>
        protected virtual TabbedThumbnail CreateThumbnailPreview(TitleBarTab tab)
        {
            TabbedThumbnail preview = TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview(tab.Content);
            if (preview != null)
                TaskbarManager.Instance.TabbedThumbnail.RemoveThumbnailPreview(tab.Content);
            preview = new TabbedThumbnail(Handle, tab.Content);
            preview.Title = tab.Content.Text;
            preview.Tooltip = tab.Content.Text;
            preview.SetWindowIcon((Icon)tab.Content.Icon.Clone());
            preview.TabbedThumbnailActivated += PreviewTabbedThumbnailActivated;
            preview.TabbedThumbnailClosed += PreviewTabbedThumbnailClosed;
            preview.TabbedThumbnailBitmapRequested += PreviewTabbedThumbnailBitmapRequested;
            preview.PeekOffset = new Vector(Padding.Left, Padding.Top - 1);
            TaskbarManager.Instance.TabbedThumbnail.AddThumbnailPreview(preview);
            return preview;
        }

        /// <summary>
        /// Removes <paramref name="closingTab" /> from <see cref="Tabs" /> and selects the next applicable tab in the list
        /// </summary>
        /// <param name="closingTab">Tab that is being closed</param>
        protected virtual void CloseTab(TitleBarTab closingTab)
        {
            int removeIndex = Tabs.IndexOf(closingTab);
            int selectedTabIndex = SelectedTabIndex;

            Tabs.Remove(closingTab);

            if (selectedTabIndex > removeIndex)
                SelectedTabIndex = selectedTabIndex - 1;
            else if (selectedTabIndex == removeIndex)
                SelectedTabIndex = Math.Min(selectedTabIndex, Tabs.Count - 1);
            else
                SelectedTabIndex = selectedTabIndex;
            if (Previews.ContainsKey(closingTab.Content))
            {
                Previews[closingTab.Content].Dispose();
                Previews.Remove(closingTab.Content);
            }
            if (PreviousActiveTab != null && closingTab.Content == PreviousActiveTab.Content)
                PreviousActiveTab = null;
            if (Tabs.Count == 0 && ExitOnLastTabClose)
                Close();
        }

        /// <summary>
        /// When a child tab updates its <see cref="System.Windows.Forms.Form.Icon"/> property, it should call this method to update the icon in the AeroPeek preview
        /// </summary>
        /// <param name="tab">Tab whose icon was updated</param>
        /// <param name="icon">The new icon to use. If this is left as null, we use <see cref="System.Windows.Forms.Form.Icon"/> on <paramref name="tab"/></param>
        public virtual void UpdateThumbnailPreviewIcon(TitleBarTab tab, Icon icon = null)
        {
            if (!AeroPeekEnabled)
                return;
            TabbedThumbnail preview = TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview(tab.Content);
            if (preview == null)
                return;
            if (icon == null)
                Icon = tab.Content.Icon;
            preview.SetWindowIcon((Icon)icon.Clone());
        }

        /// <summary>
        /// Calls <see cref="CreateTab" />, adds the resulting tab to the <see cref="Tabs"/> collection, and activates it
        /// </summary>
        public virtual void AddNewTab()
        {
            TitleBarTab newTab = CreateTab();
            Tabs.Add(newTab);
            ResizeTabContents(newTab);
            SelectedTabIndex = Tabs.Count - 1;
        }

        /// <summary>
        /// Event handler that is invoked when the <see cref="System.Windows.Forms.Form.Load" /> event is fired.  Instantiates <see cref="Overlay" /> and clears out the window's caption.
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Overlay = TitleBarTabsOverlay.GetInstance(this);
            if (TabRenderer != null)
            {
                Overlay.MouseMove += TabRenderer.OverlayOnMouseMove;
                Overlay.MouseUp += TabRenderer.OverlayOnMouseUp;
                Overlay.MouseDown += TabRenderer.OverlayOnMouseDown;
            }
        }

        /// <summary>
		/// Callback for the <see cref="System.Windows.Forms.Control.ClientSizeChanged" /> event that resizes the <see cref="EasyTabs.TitleBarTab.Content" /> form of the currently selected tab when the size of the client area for this window changes
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            ResizeTabContents();
        }

        /// <summary>
        /// Overrides the <see cref= "System.Windows.Forms.Control.SizeChanged" /> handler so that we can detect when the user has maximized or restored the window and adjust the size of the non-client area accordingly
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected override void OnSizeChanged(EventArgs e)
        {
            if (PreviousWindowState != null && WindowState != PreviousWindowState.Value)
                SetFrameSize();
            PreviousWindowState = WindowState;
            base.OnSizeChanged(e);
        }

        /// <summary>
        /// Overrides the message processor for the window so that we can respond to windows events to render and manipulate the tabs properly
        /// </summary>
        /// <param name="m">Message received by the pump</param>
        protected override void WndProc(ref Message m)
        {
            bool callDwp = true;
            switch ((WM)m.Msg)
            {
                // When the window is activated, set the size of the non-client area appropriately
                case WM.WM_ACTIVATE:
                    if ((m.WParam.ToInt64() & 0x0000FFFF) != 0)
                    {
                        SetFrameSize();
                        ResizeTabContents();
                        m.Result = IntPtr.Zero;
                    }
                    break;
                case WM.WM_NCHITTEST:
                    // Call the base message handler to see where the user clicked in the window
                    base.WndProc(ref m);
                    HT hitResult = (HT)m.Result.ToInt32();
                    // If they were over the minimize/maximize/close buttons or the system menu, let the message pass
                    if (!(hitResult == HT.HTCLOSE || hitResult == HT.HTMINBUTTON || hitResult == HT.HTMAXBUTTON || hitResult == HT.HTMENU || hitResult == HT.HTSYSMENU))
                        m.Result = new IntPtr((int)HitTest(m));
                    callDwp = false;
                    break;
                // Catch the case where the user is clicking the minimize button and use this opportunity to update the AeroPeek thumbnail for the current tab
                case WM.WM_NCLBUTTONDOWN:
                    if (((HT)m.WParam.ToInt32()) == HT.HTMINBUTTON && AeroPeekEnabled && SelectedTab != null)
                        UpdateTabThumbnail(SelectedTab);
                    break;
            }
            if (callDwp)
                base.WndProc(ref m);
        }

        /// <summary>
        /// Override of the handler for the paint background event that is left blank so that code is never executed
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {

        }

        /// <summary>
        /// Callback for the<see cref="TabDeselecting"/> event. Called when a <see cref="EasyTabs.TitleBarTab" /> is in the process of losing focus.  Grabs an image of the tab's content to be used when Aero Peek is activated
        /// </summary>
        /// <param name="e"></param>
        protected void OnTabDeselecting(TitleBarTabCancelEventArgs e)
        {
            if (PreviousActiveTab != null && AeroPeekEnabled)
                UpdateTabThumbnail(PreviousActiveTab);
            TabDeselecting?.Invoke(this, e);
        }

        /// <summary>
        /// Callback for the <see cref="TabSelecting"/> event
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected void OnTabSelecting(TitleBarTabCancelEventArgs e)
        {
            ResizeTabContents(e.Tab);
            TabSelecting?.Invoke(this, e);
        }

        /// <summary>
        /// Callback for the <see cref="TabSelected" /> event.  Called when a <see cref="EasyTabs.TitleBarTab" /> gains focus. Sets the active window in Aero Peek via a call to <see cref="Microsoft.WindowsAPICodePack.Taskbar.TabbedThumbnail.SetActiveTab(System.Windows.Forms.Control)"/>.
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected void OnTabSelected(TitleBarTabEventArgs e)
        {
            if (SelectedTabIndex != -1 && Previews.ContainsKey(SelectedTab.Content) && AeroPeekEnabled)
                TaskbarManager.Instance.TabbedThumbnail.SetActiveTab(SelectedTab.Content);
            PreviousActiveTab = SelectedTab;
            TabSelected?.Invoke(this, e);
        }

        /// <summary>
        /// Callback for the <see cref="TabDeselected" /> event
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected void OnTabDeselected(TitleBarTabEventArgs e)
        {
            TabDeselected?.Invoke(this, e);
        }

        /// <summary>
		/// Callback for the <see cref="TabClicked" /> event
        /// </summary>
        /// <param name="e">Arguments associated with the event</param>
        protected internal void OnTabClicked(TitleBarTabEventArgs e)
        {
            TabClicked?.Invoke(this, e);
        }

        /// <summary>
        /// Callback that is invoked whenever anything is added or removed from <see cref="Tabs" /> so that we can trigger a redraw of the tabs
        /// </summary>
        /// <param name="sender">Object for which this event was raised</param>
        /// <param name="e">Arguments associated with the event</param>
        private void TabsCollectionModified(object sender, ListModificationEventArgs e)
        {
            SetFrameSize();
            if (e.Modification == ListModification.ItemAdded || e.Modification == ListModification.RangeAdded)
            {
                for (int i = 0; i < e.Count; i++)
                {
                    TitleBarTab currentTab = Tabs[i + e.StartIndex];
                    currentTab.Content.TextChanged += ContentTextChanged;
                    currentTab.OnClosing += TitleBarTabsClosing;
                    if (AeroPeekEnabled)
                        TaskbarManager.Instance.TabbedThumbnail.SetActiveTab(CreateThumbnailPreview(currentTab));
                }
            }
            Overlay?.Render(true);
        }

        /// <summary>
		/// Event handler that is called when a tab's <see cref="System.Windows.Forms.Form.Text" /> property is changed, which re-renders the tab text and updates the title of the Aero Peek preview
        /// </summary>
        /// <param name="sender">Object from which this event originated (the <see cref="EasyTabs.TitleBarTab.Content" /> object in this case)</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ContentTextChanged(object sender, EventArgs e)
        {
            if (AeroPeekEnabled)
            {
                TabbedThumbnail preview = TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview((Form)sender);
                if (preview != null)
                    preview.Title = (sender as Form).Text;
            }
            Overlay?.Render(true);
        }

        /// <summary>
		/// Event handler that is called when a tab's <see cref="EasyTabs.TitleBarTab.Closing" /> event is fired, which removes the tab from <see cref="Tabs"/> and re-renders <see cref="_overlay" />
        /// </summary>
        /// <param name="sender">Object from which this event originated (the <see cref="EasyTabs.TitleBarTab" /> in this case)</param>
        /// <param name="e">Arguments associated with the event</param>
        private void TitleBarTabsClosing(object sender, CancelEventArgs e)
        {
            TitleBarTab tab = (TitleBarTab)sender;
            CloseTab(tab);

            if (!tab.Content.IsDisposed && AeroPeekEnabled)
                TaskbarManager.Instance.TabbedThumbnail.RemoveThumbnailPreview(tab.Content);
            Overlay?.Render(true);
        }

        /// <summary>
		/// Handler method that's called when the user clicks on an Aero Peek preview thumbnail. Finds the tab associated with the thumbnail and focuses on it
        /// </summary>
        /// <param name="sender">Object from which this event originated</param>
        /// <param name="e">Arguments associated with this event</param>
        private void PreviewTabbedThumbnailActivated(object sender, TabbedThumbnailEventArgs e)
        {
            foreach (TitleBarTab tab in Tabs.Where(tab => tab.Content.Handle == e.WindowHandle))
            {
                SelectedTabIndex = Tabs.IndexOf(tab);
                TaskbarManager.Instance.TabbedThumbnail.SetActiveTab(tab.Content);
                break; // Todo: why break here ??? 
            }
            if (WindowState == FormWindowState.Minimized)
                User32.ShowWindow(Handle, 3);
            else
                Focus();
        }

        /// <summary>
        /// Handler method that's called when the user clicks the close button in an Aero Peek preview thumbnail. Finds the window associated with the thumbnail and calls <see cref="System.Windows.Forms.Form.Close"/> on it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreviewTabbedThumbnailClosed(object sender, TabbedThumbnailEventArgs e)
        {
            foreach (TitleBarTab tab in Tabs.Where(tab => tab.Content.Handle == e.WindowHandle))
            {
                CloseTab(tab);
                break; // Todo: why break here ???
            }
        }

        /// <summary>
		/// Handler method that's called when Aero Peek needs to display a thumbnail for a <see cref="EasyTabs.TitleBarTab" /> finds the preview bitmap generated in <see cref="TabDeselecting" /> and returns that
        /// </summary>
        /// <param name="sender">Object from which this event originated</param>
        /// <param name="e">Arguments associated with this event</param>
        private void PreviewTabbedThumbnailBitmapRequested(object sender, TabbedThumbnailBitmapRequestedEventArgs e)
        {
            foreach (TitleBarTab rdcWindow in Tabs.Where(rdcWindow => rdcWindow.Content.Handle == e.WindowHandle && Previews.ContainsKey(rdcWindow.Content)))
            {
                TabbedThumbnail preview = TaskbarManager.Instance.TabbedThumbnail.GetThumbnailPreview(rdcWindow.Content);
                preview.SetImage(Previews[rdcWindow.Content]);
                break; // Todo: why break here ???
            }
        }

        /// <summary>
		/// Callback that should be implemented by the inheriting class that will create a new <see cref="EasyTabs.TitleBarTab" /> object when the add button is clicked
        /// </summary>
        /// <returns>A newly created tab</returns>
        public abstract TitleBarTab CreateTab();

        /// <summary>
        /// Event that is raised immediately prior to a tab being deselected (<see cref="TabDeselected" />)
        /// </summary>
        public event TitleBarTabCancelEventHandler TabDeselecting;

        /// <summary>
        /// Event that is raised after a tab has been deselected
        /// </summary>
        public event TitleBarTabEventHandler TabDeselected;

        /// <summary>
        /// Event that is raised immediately prior to a tab being selected (<see cref="TabSelected" />)
        /// </summary>
        public event TitleBarTabCancelEventHandler TabSelecting;

        /// <summary>
        /// Event that is raised after a tab has been selected
        /// </summary>
        public event TitleBarTabEventHandler TabSelected;

        /// <summary>
        /// Event that is raised after a tab has been clicked
        /// </summary>
        public event TitleBarTabEventHandler TabClicked;

        /// <summary>
        /// Event delegate for <see cref="EasyTabs.TitleBarTabs.TabDeselecting" /> and <see cref="EasyTabs.TitleBarTabs.TabSelecting" /> that allows subscribers to cancel the event and keep it from proceeding.
        /// </summary>
        /// <param name="sender">Object for which this event was raised.</param>
        /// <param name="e">Data associated with the event.</param>
        public delegate void TitleBarTabCancelEventHandler(object sender, TitleBarTabCancelEventArgs e);

        /// <summary>Event delegate for <see cref="EasyTabs.TitleBarTabs.TabSelected" /> and <see cref="EasyTabs.TitleBarTabs.TabDeselected" />.</summary>
        /// <param name="sender">Object for which this event was raised.</param>
        /// <param name="e">Data associated with the event.</param>
        public delegate void TitleBarTabEventHandler(object sender, TitleBarTabEventArgs e);
    }
}
