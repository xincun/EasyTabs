using System;
using System.Windows.Forms;

namespace EasyTabs
{
    /// <summary>
    /// Event arguments class for an event that occurs on a collection of collection of <see cref="EasyTabs.TitleBarTab" />s
    /// </summary>
    public class TitleBarTabEventArgs : EventArgs
    {
        public TitleBarTabEventArgs()
        {

        }

        public TitleBarTabEventArgs(TabControlAction? action, TitleBarTab tab, int tabIndex, bool wasDragging)
        {
            Action = action;
            Tab = tab;
            TabIndex = tabIndex;
            WasDragging = wasDragging;
        }

        /// <summary>
        /// Action that is being performed
        /// </summary>
        public TabControlAction? Action { get; }

        /// <summary>
        /// The tab that the <see cref="Action" /> is being performed on
        /// </summary>
        public TitleBarTab Tab { get; }

        /// <summary>
        /// Index of the tab within the collection
        /// </summary>
        public int TabIndex { get; }

        /// <summary>
        /// Flag indicating if the user was dragging the tab when the event occurred
        /// </summary>
        public bool WasDragging { get; }
    }
}
