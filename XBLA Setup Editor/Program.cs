// =============================================================================
// XBLA Setup Editor - Application Entry Point
// =============================================================================
// This is a GoldenEye 007 XBLA (Xbox Live Arcade) modding tool that allows
// editing of various game data including:
//   - STR/ADB string database files (localized text)
//   - Setup files (level configurations)
//   - 21990 N64 configuration files (sky, fog, music data)
//   - Multiplayer weapon sets
//   - Weapon statistics and models
//   - XEX executable patching
//
// The application uses a Windows Forms tabbed interface where multiple editing
// tabs share common XEX and 21990 file state managed by the MainForm.
// =============================================================================

using System;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Application entry point class.
    /// Initializes Windows Forms and launches the main application window.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Sets up the Windows Forms environment and creates the main form.
        /// </summary>
        /// <remarks>
        /// The STAThread attribute is required for Windows Forms applications
        /// to ensure proper COM interop and UI thread handling.
        /// </remarks>
        [STAThread]
        static void Main()
        {
            // Initialize application configuration (enables visual styles, etc.)
            ApplicationConfiguration.Initialize();

            // Create and run the main application window
            // This will block until the main form is closed
            Application.Run(new MainForm());
        }
    }
}
