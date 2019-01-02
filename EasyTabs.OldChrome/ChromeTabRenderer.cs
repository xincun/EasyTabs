using EasyTabs.EventList;
using EasyTabs.OldChrome.Properties;
using EasyTabs.Renderer;
using System.Drawing;

namespace EasyTabs.OldChrome
{
    /// <summary>
    /// Renderer that produces tabs that mimic the appearance of the old Chrome browser
    /// </summary>
    public class ChromeTabRenderer : BaseTabRenderer
    {
        public ChromeTabRenderer(TitleBarTabs parentWindow) : base(parentWindow)
        {
            // Initialize the various images to use during rendering
            ActiveLeftSideImage = Resources.ChromeLeft;
            ActiveRightSideImage = Resources.ChromeRight;
            ActiveCenterImage = Resources.ChromeCenter;
            InactiveLeftSideImage = Resources.ChromeInactiveLeft;
            InactiveRightSideImage = Resources.ChromeInactiveRight;
            InactiveCenterImage = Resources.ChromeInactiveCenter;
            CloseButtonImage = Resources.ChromeClose;
            CloseButtonHoverImage = Resources.ChromeCloseHover;
            Background = Resources.ChromeBackground;
            AddButtonImage = new Bitmap(Resources.ChromeAdd);
            AddButtonHoverImage = new Bitmap(Resources.ChromeAddHover);

            // Set the various positioning properties
            CloseButtonMarginTop = 6;
            CloseButtonMarginLeft = 2;
            AddButtonMarginTop = 7;
            AddButtonMarginLeft = -1;
            CaptionMarginTop = 6;
            IconMarginTop = 7;
            IconMarginRight = 5;
            AddButtonMarginRight = 5;
        }

        /// <summary>
        /// A Chrome-specific right-side tab image that allows the separation between inactive tabs to be more clearly defined
        /// </summary>
        protected Image InactiveRightSideShadowImage => Resources.ChromeInactiveRightShadow;

        /// <summary>
        /// Since old Chrome tabs overlap, we set this property to the amount that they overlap by
        /// </summary>
        public override int OverlapWidth => 15;

        /// <summary>
        /// Gets the image to use for the right side of the tab.  For Chrome, we pick a specific image for inactive tabs that aren't at the end of the list to allow for the separation between inactive tabs to be more clearly defined
        /// </summary>
        /// <param name="tab">Tab that we are retrieving the image for</param>
        /// <returns>Right-side image for <paramref name="tab"/></returns>
        protected override Image GetTabRightImage(TitleBarTab tab)
        {
            ListWithEvents<TitleBarTab> allTabs = tab.Parent.Tabs;
            if (tab.Active || allTabs.IndexOf(tab) == allTabs.Count - 1)
                return base.GetTabRightImage(tab);
            return InactiveRightSideShadowImage;
        }
    }
}
