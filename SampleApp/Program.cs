using EasyTabs;
using System;
using System.Windows.Forms;

namespace SampleApp
{
    static class Program
    {
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            frmTab frmTab = new frmTab();
            TitleBarTab tab = new TitleBarTab(frmTab);
            frmTab.Tabs.Add(frmTab.CreateTab());
            frmTab.SelectedTabIndex = 0;

            TitleBarTabsAppContext appContext = new TitleBarTabsAppContext();
            appContext.Start(frmTab);

            Application.Run(appContext);
        }
    }
}
