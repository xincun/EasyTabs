using System.ComponentModel;
using System.Windows.Forms;

namespace EasyTabs
{
    /// <summary>
    /// Event arguments class for a cancelable event that occurs on a collection of collection of <see cref="EasyTabs.TitleBarTab" />s
    /// </summary>
    public class TitleBarTabCancelEventArgs : CancelEventArgs
    {
        public TitleBarTabCancelEventArgs()
        {

        }

        public TitleBarTabCancelEventArgs(TabControlAction action, TitleBarTab tab, int tabIndex)
        {
            Action = action;
            Tab = tab;
            TabIndex = tabIndex;
        }

        /// <summary>
        /// Action that is being performed
        /// </summary>
        public TabControlAction Action { get; }

        /// <summary>
        /// The tab that the <see cref="Action" /> is being performed on
        /// </summary>
        public TitleBarTab Tab { get; }

        /// <summary>
        /// Index of the tab within the collection
        /// </summary>
        public int TabIndex { get; }
    }
}
