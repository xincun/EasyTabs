using System.Collections.Generic;
using System.Windows.Forms;

namespace EasyTabs
{
    public class TitleBarTabsAppContext : ApplicationContext
    {
        public TitleBarTabsAppContext()
        {

        }

        /// <summary>
        /// List of all opened windows
        /// </summary>
        public List<TitleBarTabs> OpenWindows { get; protected set; } = new List<TitleBarTabs>();

        /// <summary>
        /// Constructor; takes the initial window to display and, if it's not closing, opens it and shows it
        /// </summary>
        /// <param name="initialFormInstance">Initial window to display</param>
        public void Start(TitleBarTabs initialFormInstance)
        {
            if (initialFormInstance.IsClosing)
                ExitThread();
            else
            {
                OpenWindow(initialFormInstance);
                initialFormInstance.Show();
            }
        }

        /// <summary>
        /// Adds <paramref name="window" /> to <see cref="OpenWindows" /> and attaches event handlers to its <see cref="System.Windows.Forms.Form.FormClosed" /> event to keep track of it
        /// </summary>
        /// <param name="window">Window that we're opening</param>
        public void OpenWindow(TitleBarTabs window)
        {
            if (!OpenWindows.Contains(window))
            {
                window.ApplicationContext = this;
                OpenWindows.Add(window);
                window.FormClosed += WindowFormClosed;
            }
        }

        /// <summary>
        /// Handler method that's called when an item in <see cref="OpenWindows" /> has its <see cref="System.Windows.Forms.Form.FormClosed" /> event invoked.  Removes the window from <see cref="OpenWindows" /> and, if there are no more windows open, calls <see cref="System.Windows.Forms.ApplicationContext.ExitThread" />
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void WindowFormClosed(object sender, FormClosedEventArgs e)
        {
            OpenWindows.Remove((TitleBarTabs)sender);
            if (OpenWindows.Count == 0)
                ExitThread();
        }
    }
}
