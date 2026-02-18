using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Helper class for DPI-aware scaling of pixel values.
    /// Use Scale() to convert design-time pixel values (at 96 DPI) to runtime values.
    /// </summary>
    public static class DpiHelper
    {
        private static float? _cachedScaleFactor;

        [DllImport("user32.dll")]
        private static extern int GetDpiForSystem();

        /// <summary>
        /// Gets the system DPI scale factor, cached for performance.
        /// </summary>
        private static float SystemScaleFactor
        {
            get
            {
                if (_cachedScaleFactor == null)
                {
                    try
                    {
                        // Use Windows API to get the system DPI (works on Windows 10+)
                        int dpi = GetDpiForSystem();
                        _cachedScaleFactor = dpi / 96f;
                    }
                    catch
                    {
                        // Fallback: Get DPI from the desktop
                        using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        {
                            _cachedScaleFactor = g.DpiX / 96f;
                        }
                    }
                }
                return _cachedScaleFactor.Value;
            }
        }

        /// <summary>
        /// Scales a pixel value from 96 DPI (100% scaling) to the current system DPI.
        /// </summary>
        /// <param name="control">Any control (ignored, uses system DPI).</param>
        /// <param name="value">The pixel value designed at 96 DPI.</param>
        /// <returns>The scaled pixel value for the current DPI.</returns>
        public static int Scale(Control control, int value)
        {
            return (int)(value * SystemScaleFactor);
        }

        /// <summary>
        /// Gets the current DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, etc.)
        /// </summary>
        /// <param name="control">Any control (ignored, uses system DPI).</param>
        /// <returns>The scale factor.</returns>
        public static float GetScaleFactor(Control control)
        {
            return SystemScaleFactor;
        }
    }
}
