using System;
using System.Drawing;
using System.Windows.Forms;
using Win32Interop.Enums;
using Win32Interop.Methods;
using Win32Interop.Structs;

namespace EasyTabs
{
    /// <summary>
    /// Form that actually displays the thumbnail content for <see cref="EasyTabs.TornTabForm" />
    /// </summary>
    internal class LayeredWindow : Form
    {
        public LayeredWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
        }

        /// <summary>
        /// Makes sure that the window is created with an <see cref="Win32Interop.Enums.WS_EX.WS_EX_LAYERED" /> flag set so that it can be alpha-blended properly with the desktop contents underneath it
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= (int) WS_EX.WS_EX_LAYERED;
                return createParams;
            }
        }

        /// <summary>
        /// Renders the tab thumbnail (<paramref name="image" />) using the given dimensions and coordinates and blends it properly with the underlying desktop elements
        /// </summary>
        /// <param name="image">Thumbnail to display</param>
        /// <param name="opacity">Opacity that <paramref name="image"/> should be displayed with</param>
        /// <param name="width">Width of <paramref name="image"/></param>
        /// <param name="height">Height of <paramref name="image"/></param>
        /// <param name="position">Screen position that <paramref name="image"/> should be displayed at</param>
        public void UpdateWindow(Bitmap image, byte opacity, int width, int height, POINT position)
        {
            IntPtr windowHandle = User32.GetWindowDC(Handle);
            IntPtr deviceContextHandle = Gdi32.CreateCompatibleDC(windowHandle);
            IntPtr bitmapHandle = image.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmapHandle = Gdi32.SelectObject(deviceContextHandle, bitmapHandle);
            SIZE size = new SIZE { cx = 0, cy = 0 };
            POINT destinationPosition = new POINT { x = 0, y = 0 };
            if (width == -1 || height == -1)
            {
                // No width and height specified, use the size of the image
                size.cx = image.Width;
                size.cy = image.Height;
            }
            else
            {
                // Use whichever size is smallest, so that the image will be clipped if necessary
                size.cx = Math.Min(image.Width, width);
                size.cy = Math.Min(image.Height, height);
            }
            // Set the opacity and blend the image with the underlying desktop elements using User32.UpdateLayeredWindow
            BLENDFUNCTION blendFunction = new BLENDFUNCTION { BlendOp = Convert.ToByte((int) AC.AC_SRC_OVER), SourceConstantAlpha = opacity, AlphaFormat = Convert.ToByte((int) AC.AC_SRC_ALPHA), BlendFlags = 0 };

            User32.UpdateLayeredWindow(Handle, windowHandle, ref position, ref size, deviceContextHandle, ref destinationPosition, 0, ref blendFunction, ULW.ULW_ALPHA);

            Gdi32.SelectObject(deviceContextHandle, oldBitmapHandle);
            Gdi32.DeleteObject(bitmapHandle);
            Gdi32.DeleteDC(deviceContextHandle);
            User32.ReleaseDC(Handle, windowHandle);
        }
    }
}