using System;
using Win32Interop.Structs;

namespace EasyTabs
{
    /// <summary>
    /// Contains information on mouse events captured by <see cref="EasyTabs.TitleBarTabsOverlay.MouseHookCallback" /> and processed by <see cref="EasyTabs.TitleBarTabsOverlay.InterpretMouseEvents" />
    /// </summary>
    public class MouseEvent
    {
        /// <summary>
        /// Code for the event
        /// </summary>
        public int nCode { get; set; }

        /// <summary>
        /// wParam value associated with the event
        /// </summary>
        public IntPtr wParam { get; set; }

        /// <summary>
        /// lParam value associated with the event
        /// </summary>
        public IntPtr lParam { get; set; }

        /// <summary>
        /// Data associated with the mouse event
        /// </summary>
        public MSLLHOOKSTRUCT? MouseData { get; set; }
    }
}