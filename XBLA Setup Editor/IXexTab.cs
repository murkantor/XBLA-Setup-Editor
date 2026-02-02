// =============================================================================
// IXexTab.cs - Shared State Interfaces and Event Arguments
// =============================================================================
// This file defines the interfaces that tabs must implement to participate in
// the shared file state system. The architecture allows:
//
// 1. XEX Sharing (IXexTab):
//    - MainForm loads XEX once, all tabs share the same data
//    - When any tab modifies the XEX, changes are broadcast to other tabs
//    - On save, all tabs contribute their modifications
//
// 2. 21990 Sharing (I21990Tab):
//    - MainForm loads 21990 file, tabs that need it receive the data
//    - Used by tabs that import N64 configuration (sky, fog, music, weapon stats)
//
// Event Flow:
// 1. User loads XEX → XexLoaded event → tabs call OnXexLoaded()
// 2. Tab modifies XEX → XexModified event → MainForm updates shared state
// 3. MainForm calls OnXexDataUpdated() on other tabs
// 4. User saves → MainForm calls GetModifiedXexData() on all tabs
// =============================================================================

using System;

namespace XBLA_Setup_Editor
{
    // =========================================================================
    // 21990 FILE INTERFACES
    // =========================================================================

    /// <summary>
    /// Interface for tabs that use the shared 21990 file state.
    /// Implement this interface to receive 21990 data when loaded.
    ///
    /// 21990 files are N64 configuration files containing:
    /// - Sky color data for each level
    /// - Fog settings (distance, color)
    /// - Music track assignments
    /// - Menu text IDs (level names/descriptions)
    /// - Weapon statistics (in N64 format)
    /// </summary>
    public interface I21990Tab
    {
        /// <summary>
        /// Called when a 21990 file is loaded in the main form.
        /// The tab should parse and display the relevant data.
        /// </summary>
        /// <param name="data">The raw 21990 file bytes</param>
        /// <param name="path">The file path of the loaded 21990 file</param>
        void On21990Loaded(byte[] data, string path);

        /// <summary>
        /// Called when the 21990 file is being unloaded.
        /// The tab should clear any 21990-related state and UI.
        /// </summary>
        void On21990Unloaded();
    }

    /// <summary>
    /// Event args for 21990 loaded event.
    /// Contains the loaded file data and path.
    /// </summary>
    public class File21990LoadedEventArgs : EventArgs
    {
        /// <summary>Raw bytes of the loaded 21990 file.</summary>
        public byte[] Data { get; }

        /// <summary>File path of the loaded 21990 file.</summary>
        public string Path { get; }

        /// <summary>
        /// Creates event args for a 21990 file load event.
        /// </summary>
        /// <param name="data">The loaded file bytes.</param>
        /// <param name="path">The file path.</param>
        public File21990LoadedEventArgs(byte[] data, string path)
        {
            Data = data;
            Path = path;
        }
    }

    // =========================================================================
    // XEX FILE INTERFACES
    // =========================================================================

    /// <summary>
    /// Interface for tabs that use the shared XEX state.
    /// Implement this interface to participate in the shared XEX workflow.
    ///
    /// XEX files are Xbox 360 executables that contain:
    /// - Game code
    /// - Embedded data (level setups, weapon data, menu data, etc.)
    /// - The main target for modding
    ///
    /// Implementing tabs should:
    /// 1. Parse their relevant data from XEX in OnXexLoaded()
    /// 2. Fire XexModified event when making changes
    /// 3. Return modified data in GetModifiedXexData() when saving
    /// </summary>
    public interface IXexTab
    {
        /// <summary>
        /// Called when a XEX file is loaded in the main form.
        /// The tab should update its UI based on the loaded data.
        /// This is a full load - parse all data and rebuild UI.
        /// </summary>
        /// <param name="xexData">The loaded XEX data bytes</param>
        /// <param name="path">The file path of the loaded XEX</param>
        void OnXexLoaded(byte[] xexData, string path);

        /// <summary>
        /// Called when another tab modifies the XEX data.
        /// This is a lightweight update that just refreshes the data reference
        /// without re-parsing or rebuilding UI.
        ///
        /// Use this to keep tabs synchronized when other tabs make changes.
        /// For example, if the 21990 importer patches fog data, the setup
        /// patching tab needs the updated data for split XEX operations.
        /// </summary>
        /// <param name="xexData">The updated XEX data bytes</param>
        void OnXexDataUpdated(byte[] xexData);

        /// <summary>
        /// Called when the XEX is being unloaded or the tab should reset.
        /// Clear all XEX-related state and disable UI elements.
        /// </summary>
        void OnXexUnloaded();

        /// <summary>
        /// Gets the modified XEX data if the tab has made changes.
        /// Returns null if no changes have been made.
        ///
        /// Called by MainForm when saving to collect all modifications.
        /// After returning data, the tab should reset its HasUnsavedChanges flag.
        /// </summary>
        /// <returns>Modified XEX data, or null if no modifications</returns>
        byte[]? GetModifiedXexData();

        /// <summary>
        /// Indicates whether this tab has unsaved changes.
        /// Used by MainForm to warn about unsaved changes.
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Gets the display name for this tab (shown in the UI).
        /// Used in status messages like "Modified by [TabDisplayName]".
        /// </summary>
        string TabDisplayName { get; }
    }

    // =========================================================================
    // XEX EVENT ARGUMENTS
    // =========================================================================

    /// <summary>
    /// Event args for XEX loaded event.
    /// Contains the loaded XEX data and file path.
    /// </summary>
    public class XexLoadedEventArgs : EventArgs
    {
        /// <summary>The loaded XEX file bytes.</summary>
        public byte[] XexData { get; }

        /// <summary>The file path of the loaded XEX.</summary>
        public string Path { get; }

        /// <summary>
        /// Creates event args for a XEX file load event.
        /// </summary>
        /// <param name="xexData">The loaded XEX bytes.</param>
        /// <param name="path">The file path.</param>
        public XexLoadedEventArgs(byte[] xexData, string path)
        {
            XexData = xexData;
            Path = path;
        }
    }

    /// <summary>
    /// Event args for when a tab modifies the XEX data.
    /// Contains the modified data and the name of the source tab.
    /// </summary>
    public class XexModifiedEventArgs : EventArgs
    {
        /// <summary>The modified XEX data after changes were applied.</summary>
        public byte[] ModifiedData { get; }

        /// <summary>
        /// Name of the tab that made the modification.
        /// Used in status messages and for debugging.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Creates event args for a XEX modification event.
        /// </summary>
        /// <param name="modifiedData">The modified XEX bytes.</param>
        /// <param name="source">Name of the source tab (usually TabDisplayName).</param>
        public XexModifiedEventArgs(byte[] modifiedData, string source)
        {
            ModifiedData = modifiedData;
            Source = source;
        }
    }
}
