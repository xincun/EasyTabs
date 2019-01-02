using EasyTabs;
using EasyTabs.OldChrome;
using SampleApp.Properties;

namespace SampleApp
{
    public partial class frmTab : TitleBarTabs
    {
        public frmTab()
        {
            InitializeComponent();

            AeroPeekEnabled = true;
            TabRenderer = new ChromeTabRenderer(this);
            Icon = Resources.DefaultIcon;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override TitleBarTab CreateTab()
        {
            TitleBarTab tab = new TitleBarTab(this);
            tab.Content = new frmMain() { Text = "New Tab" };
            return tab;
        }
    }
}
