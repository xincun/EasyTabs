using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EasyTabs
{
    /// <summary>
    /// Wraps a <see cref="System.Windows.Forms.Form"/> instance (<see cref="m_formContent"/>), that repredents the content that should be displayed winthin a tab instance
    /// </summary>
    public class TitleBarTab
    {
        protected Form m_formContent;

        protected TitleBarTabs m_parent;

        protected bool m_active;

        public TitleBarTab(TitleBarTabs parent)
        {
            ShowCloseButton = true;
            Parent = parent;
        }

        /// <summary>
        /// The area in which the tab is rendered in the client window
        /// </summary>
        internal Rectangle Area { get; set; }

        /// <summary>
        /// The area of the close button for this tab in the client window
        /// </summary>
        internal Rectangle CloseButtonArea { get; set; }

        /// <summary>
        /// Pre-rendered image of the tab's background
        /// </summary>
        internal Bitmap TabImage { get; set; }

        /// <summary>
        /// Flag indicating whether or not we should display the close button for this tab
        /// </summary>
        public bool ShowCloseButton { get; set; }

        /// <summary>
        /// Parent window that contains this tab
        /// </summary>
        public TitleBarTabs Parent
        {
            get => m_parent;
            internal set
            {
                m_parent = value;
                if (Content != null)
                    Content.Parent = m_parent;
            }
        }

        /// <summary>
        /// The content that should be displayed for this tab
        /// </summary>
        public Form Content
        {
            get => m_formContent;
            set
            {
                if (Content != null)
                {

                }
                m_formContent = value;
                Content.FormBorderStyle = FormBorderStyle.None;
                Content.TopLevel = false;
                Content.Parent = Parent;
                Content.FormClosing += ContentFormClosing;
                Content.TextChanged += ContentTextChanged;
            }
        }

        /// <summary>
        /// Flag indicating whether or not this tab is active
        /// </summary>
        public bool Active
        {
            get => m_active;
            internal set
            {
                m_active = value;
                TabImage = null;
                Content.Visible = value;
            }
        }

        /// <summary>
        /// The caption that's displayed in the tab's title(simply uses the<see cref="System.Windows.Forms.Form.Text" /> of <see cref="Content" />)
        /// </summary>
        public string Caption
        {
            get => Content.Text;
            set => Content.Text = value;
        }

        /// <summary>
        /// The icon that's displayed in the tab's title (simply uses the <see cref="System.Windows.Forms.Form.Icon" /> of <see cref="Content" />)
        /// </summary>
        public Icon Icon
        {
            get => Content.Icon;
            set => Content.Icon = value;
        }

        /// <summary>
        /// Unsubscribes the tab from any event handlers that may have been attached to its <see cref="OnClosing" /> or <see cref="OnTextChanged" /> events
        /// </summary>
        public void ClearEventSubscriptions()
        {
            OnClosing = null;
            OnTextChanged = null;
        }

        /// <summary>
        /// Called from <see cref="EasyTabs.TornTabForm" /> when we need to generate a thumbnail for a tab when it is torn out of its parent window. We simply call <see cref="Graphics.CopyFromScreen(System.Drawing.Point,System.Drawing.Point,System.Drawing.Size)" /> to copy the screen contents to a <see cref="System.Drawing.Bitmap"/>
        /// </summary>
        /// <returns>An image of the tab's contents</returns>
        public virtual Bitmap GetThumbnail()
        {
            Bitmap tabContent = new Bitmap(Content.Size.Width, Content.Size.Height);
            using (Graphics contentGraphics = Graphics.FromImage(tabContent))
                contentGraphics.CopyFromScreen(Content.PointToScreen(Point.Empty).X, Content.PointToScreen(Point.Empty).Y, 0, 0, Content.Size);
            return tabContent;
        }

        /// <summary>
        /// Event handler that is invoked when <see cref="Content" />'s <see cref="System.Windows.Forms.Control.TextChanged" /> event is fired, which in turn fires this class <see cref="OnTextChanged" /> event
        /// </summary>
        /// <param name="sender">Object from which this event originated (<see cref="Content" /> in this case)</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ContentTextChanged(object sender, EventArgs e) => OnTextChanged?.Invoke(this, e);

        /// <summary>
        /// Event handler that is invoked when <see cref="Content" />'s <see cref="System.Windows.Forms.Form.Closing" /> event is fired, which in turn fires this class <see cref="OnClosing" /> event
        /// </summary>
        /// <param name="sender">Object from which this event originated (<see cref="Content" /> in this case)</param>
        /// <param name="e">Arguments associated with the event</param>
        private void ContentFormClosing(object sender, FormClosingEventArgs e) => OnClosing?.Invoke(this, e);

        /// <summary>
        /// Event that is fired when <see cref="Content" />'s <see cref="System.Windows.Forms.Form.Closing" /> event is fired
        /// </summary>
        public event CancelEventHandler OnClosing;

        /// <summary>
        /// Event that is fired when <see cref="Content" />'s <see cref="System.Windows.Forms.Control.TextChanged" /> event is fired
        /// </summary>
        public event EventHandler OnTextChanged;
    }
}