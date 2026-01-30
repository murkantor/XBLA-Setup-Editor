using System;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Interface for tabs that use the shared 21990 file state.
    /// Implement this interface to receive 21990 data when loaded.
    /// </summary>
    public interface I21990Tab
    {
        /// <summary>
        /// Called when a 21990 file is loaded in the main form.
        /// </summary>
        /// <param name="data">The raw 21990 file bytes</param>
        /// <param name="path">The file path of the loaded 21990 file</param>
        void On21990Loaded(byte[] data, string path);

        /// <summary>
        /// Called when the 21990 file is being unloaded.
        /// </summary>
        void On21990Unloaded();
    }

    /// <summary>
    /// Event args for 21990 loaded event.
    /// </summary>
    public class File21990LoadedEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public string Path { get; }

        public File21990LoadedEventArgs(byte[] data, string path)
        {
            Data = data;
            Path = path;
        }
    }

    /// <summary>
    /// Interface for tabs that use the shared XEX state.
    /// Implement this interface to participate in the shared XEX workflow.
    /// </summary>
    public interface IXexTab
    {
        /// <summary>
        /// Called when a XEX file is loaded in the main form.
        /// The tab should update its UI based on the loaded data.
        /// </summary>
        /// <param name="xexData">The loaded XEX data bytes</param>
        /// <param name="path">The file path of the loaded XEX</param>
        void OnXexLoaded(byte[] xexData, string path);

        /// <summary>
        /// Called when another tab modifies the XEX data.
        /// This is a lightweight update that just refreshes the data reference
        /// without re-parsing or rebuilding UI.
        /// </summary>
        /// <param name="xexData">The updated XEX data bytes</param>
        void OnXexDataUpdated(byte[] xexData);

        /// <summary>
        /// Called when the XEX is being unloaded or the tab should reset.
        /// </summary>
        void OnXexUnloaded();

        /// <summary>
        /// Gets the modified XEX data if the tab has made changes.
        /// Returns null if no changes have been made.
        /// </summary>
        /// <returns>Modified XEX data, or null if no modifications</returns>
        byte[]? GetModifiedXexData();

        /// <summary>
        /// Indicates whether this tab has unsaved changes.
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Gets the display name for this tab (shown in the UI).
        /// </summary>
        string TabDisplayName { get; }
    }

    /// <summary>
    /// Event args for XEX loaded event.
    /// </summary>
    public class XexLoadedEventArgs : EventArgs
    {
        public byte[] XexData { get; }
        public string Path { get; }

        public XexLoadedEventArgs(byte[] xexData, string path)
        {
            XexData = xexData;
            Path = path;
        }
    }

    /// <summary>
    /// Event args for when a tab modifies the XEX data.
    /// </summary>
    public class XexModifiedEventArgs : EventArgs
    {
        public byte[] ModifiedData { get; }
        public string Source { get; }

        public XexModifiedEventArgs(byte[] modifiedData, string source)
        {
            ModifiedData = modifiedData;
            Source = source;
        }
    }
}
