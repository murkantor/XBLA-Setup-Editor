// =============================================================================
// File21990ImporterControl.cs - N64 Config Import Tab
// =============================================================================
// UserControl that provides the "Skies, Fog and Music" tab in the editor.
// Imports sky/atmosphere, music, and menu data from N64 21990 configuration
// files and applies them to GoldenEye XBLA XEX files.
//
// TAB FUNCTIONALITY:
// ==================
// This tab allows users to:
// 1. Load a 21990 file (via MainForm's shared file dialog)
// 2. Preview extracted data in list views (menu, sky, music entries)
// 3. Select which data categories to apply (checkboxes)
// 4. Apply selected patches to the loaded XEX
//
// IMPORT OPTIONS:
// ===============
// - Apply Folder/Icon Text: Menu text ID references for level selection screen
// - Apply Sky/Fog: Sky colors, cloud settings, water parameters
// - Sky->Fog Color: Copy sky color to fog color (visual consistency)
// - N64 Fog Distances: Apply level-specific fog distance ratios
// - Apply Music: Stage music track assignments
//
// DATA FLOW:
// ==========
// 1. MainForm loads 21990 file and calls On21990Loaded()
// 2. File21990Parser extracts menu/sky/music entries
// 3. ListView controls display extracted data for preview
// 4. User clicks "Apply" to patch XEX via parser's Apply methods
// 5. XexModified event notifies MainForm of changes
//
// APPLY BEHAVIOR:
// ===============
// The control stores a copy of the original XEX data (_originalXexData) to
// prevent "stacking" modifications. Each Apply restores from original first,
// then applies all selected patches fresh. This prevents issues like fog
// ratios being divided multiple times.
//
// INTERFACE IMPLEMENTATIONS:
// ==========================
// IXexTab: Receives XEX load/unload notifications, provides modified data
// I21990Tab: Receives 21990 load/unload notifications
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// Tab control for importing N64 21990 configuration data (sky, fog, music)
    /// into GoldenEye XBLA XEX files. Implements IXexTab and I21990Tab interfaces.
    /// </summary>
    public sealed class File21990ImporterControl : UserControl, IXexTab, I21990Tab
    {
        private readonly TextBox _txtLog;
        private readonly Button _btnApplyPatches;

        private readonly CheckBox _chkApplySkyData;
        private readonly CheckBox _chkSkyToFog;
        private readonly CheckBox _chkApplyN64FogDistances;
        private readonly CheckBox _chkApplyMusic;
        private readonly CheckBox _chkApplyMenuData;

        private readonly ListView _lvSkyEntries;
        private readonly ListView _lvMusicEntries;
        private readonly ListView _lvMenuEntries;

        private File21990Parser? _parser;
        private byte[]? _xexData;
        private byte[]? _originalXexData;  // Store original for repeated Apply without stacking
        private string? _xexPath;
        private bool _hasUnsavedChanges = false;

        // Events
        public event EventHandler<XexModifiedEventArgs>? XexModified;

        public File21990ImporterControl()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 12,
                Padding = new Padding(8)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            int row = 0;

            // Row 0: Options and Apply button
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            _chkApplyMenuData = new CheckBox { Text = "Apply Folder/Icon Text", Checked = true, AutoSize = true };
            _chkApplySkyData = new CheckBox { Text = "Apply Sky/Fog", Checked = true, AutoSize = true };
            _chkSkyToFog = new CheckBox { Text = "Sky->Fog Col", Checked = true, AutoSize = true };
            _chkApplyN64FogDistances = new CheckBox { Text = "N64 Fog Dist", Checked = true, AutoSize = true };
            _chkApplyMusic = new CheckBox { Text = "Apply Music", Checked = true, AutoSize = true };

            optionsPanel.Controls.Add(_chkApplyMenuData);
            optionsPanel.Controls.Add(_chkApplySkyData);
            optionsPanel.Controls.Add(_chkSkyToFog);
            optionsPanel.Controls.Add(_chkApplyN64FogDistances);
            optionsPanel.Controls.Add(_chkApplyMusic);

            mainLayout.Controls.Add(optionsPanel, 0, row);
            mainLayout.SetColumnSpan(optionsPanel, 3);
            _btnApplyPatches = new Button { Text = "Apply", Dock = DockStyle.Fill, Enabled = false };
            mainLayout.Controls.Add(_btnApplyPatches, 3, row);
            row++;

            // Row 2: Menu Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblMenu = new Label { Text = "Scanned Menu Entries (Folder/Icon):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblMenu, 0, row);
            mainLayout.SetColumnSpan(lblMenu, 4);
            row++;

            // Row 3: Menu List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            _lvMenuEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvMenuEntries.Columns.Add("Level ID", 60);
            _lvMenuEntries.Columns.Add("Name", 80);
            _lvMenuEntries.Columns.Add("Folder Text", 80);
            _lvMenuEntries.Columns.Add("Icon Text", 80);
            mainLayout.Controls.Add(_lvMenuEntries, 0, row);
            mainLayout.SetColumnSpan(_lvMenuEntries, 4);
            row++;

            // Row 4: Sky Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblSky = new Label { Text = "Sky Entries:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblSky, 0, row);
            mainLayout.SetColumnSpan(lblSky, 4);
            row++;

            // Row 5: Sky List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            _lvSkyEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvSkyEntries.Columns.Add("#", 30);
            _lvSkyEntries.Columns.Add("Level", 60);
            _lvSkyEntries.Columns.Add("Sky RGB", 80);
            mainLayout.Controls.Add(_lvSkyEntries, 0, row);
            mainLayout.SetColumnSpan(_lvSkyEntries, 4);
            row++;

            // Row 6: Music Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblMusic = new Label { Text = "Music Entries:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblMusic, 0, row);
            mainLayout.SetColumnSpan(lblMusic, 4);
            row++;

            // Row 7: Music List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            _lvMusicEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvMusicEntries.Columns.Add("#", 30);
            _lvMusicEntries.Columns.Add("Level", 60);
            _lvMusicEntries.Columns.Add("Main", 60);
            mainLayout.Controls.Add(_lvMusicEntries, 0, row);
            mainLayout.SetColumnSpan(_lvMusicEntries, 4);
            row++;

            // Row 8: Log Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, row);
            mainLayout.SetColumnSpan(lblLog, 4);
            row++;

            // Row 9: Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false
            };
            mainLayout.Controls.Add(_txtLog, 0, row);
            mainLayout.SetColumnSpan(_txtLog, 4);

            Controls.Add(mainLayout);

            // Events
            _btnApplyPatches.Click += (_, __) => ApplyPatches();

            SetupTooltips();
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_btnApplyPatches, TooltipTexts.File21990.ApplyPatches);
            toolTip.SetToolTip(_chkApplyMenuData, TooltipTexts.File21990.ApplyMenuData);
            toolTip.SetToolTip(_chkApplySkyData, TooltipTexts.File21990.ApplySkyFog);
            toolTip.SetToolTip(_chkSkyToFog, TooltipTexts.File21990.SkyToFog);
            toolTip.SetToolTip(_chkApplyN64FogDistances, TooltipTexts.File21990.N64FogDistances);
            toolTip.SetToolTip(_chkApplyMusic, TooltipTexts.File21990.ApplyMusic);
        }

        private void ClearAnalysis()
        {
            _lvSkyEntries.Items.Clear();
            _lvMusicEntries.Items.Clear();
            _lvMenuEntries.Items.Clear();
            _txtLog.Clear();
        }

        private void Log(string message) => _txtLog.AppendText(message + Environment.NewLine);

        #region IXexTab Implementation

        public string TabDisplayName => "Skies, Fog and Music";

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void OnXexLoaded(byte[] xexData, string path)
        {
            _xexData = xexData;
            _xexPath = path;

            // Store a copy of the original XEX data so Apply can be pressed multiple times
            // without stacking modifications (e.g., fog ratios being applied repeatedly)
            _originalXexData = (byte[])xexData.Clone();

            Log($"XEX loaded: {path} ({xexData.Length:N0} bytes)");

            // Enable apply button if we have both 21990 and XEX
            _btnApplyPatches.Enabled = _parser != null;
        }

        public void OnXexDataUpdated(byte[] xexData)
        {
            // Lightweight update - just refresh the data reference
            _xexData = xexData;
        }

        public void OnXexUnloaded()
        {
            _xexData = null;
            _originalXexData = null;
            _xexPath = null;
            _btnApplyPatches.Enabled = false;
            _hasUnsavedChanges = false;
        }

        public byte[]? GetModifiedXexData()
        {
            if (!_hasUnsavedChanges)
                return null;

            _hasUnsavedChanges = false;
            return _xexData;
        }

        #endregion

        #region I21990Tab Implementation

        public void On21990Loaded(byte[] data, string path)
        {
            // Automatically analyze the loaded file
            ClearAnalysis();
            Log($"21990 file loaded: {Path.GetFileName(path)} ({data.Length:N0} bytes)");

            try
            {
                _parser = File21990Parser.Load(data);
                Log("");

                PopulateAnalysisLists();

                // Enable apply button if we have XEX loaded
                _btnApplyPatches.Enabled = _xexData != null;
                Log("Analysis complete. Ready to apply.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
            }
        }

        public void On21990Unloaded()
        {
            _parser = null;
            _btnApplyPatches.Enabled = false;
            ClearAnalysis();
        }

        #endregion

        private void PopulateAnalysisLists()
        {
            if (_parser == null) return;

            // Populate Menu Entries
            _lvMenuEntries.BeginUpdate();
            foreach (var m in _parser.MenuEntries)
            {
                var item = new ListViewItem($"0x{m.LevelId:X2}");
                item.SubItems.Add(m.LevelName);
                item.SubItems.Add($"0x{m.FolderTextId:X4}");
                item.SubItems.Add($"0x{m.IconTextId:X4}");
                _lvMenuEntries.Items.Add(item);
            }
            _lvMenuEntries.EndUpdate();

            // Populate Sky
            _lvSkyEntries.BeginUpdate();
            foreach (var sky in _parser.SkyEntries)
            {
                var item = new ListViewItem(sky.Index.ToString());
                item.SubItems.Add($"0x{sky.LevelId:X4}");
                item.SubItems.Add($"{sky.SkyColourRed},{sky.SkyColourGreen},{sky.SkyColourBlue}");
                _lvSkyEntries.Items.Add(item);
            }
            _lvSkyEntries.EndUpdate();

            // Populate Music
            _lvMusicEntries.BeginUpdate();
            foreach (var m in _parser.MusicEntries)
            {
                var item = new ListViewItem(m.Index.ToString());
                item.SubItems.Add($"0x{m.LevelId:X4}");
                item.SubItems.Add($"0x{m.MainTheme:X4}");
                _lvMusicEntries.Items.Add(item);
            }
            _lvMusicEntries.EndUpdate();

            // Generate Comparison Log
            var scanLog = new List<string>();
            _parser.GenerateComparisonLog(scanLog);
            foreach (var line in scanLog) Log(line);
        }

        private void ApplyPatches()
        {
            if (_parser == null)
            {
                MessageBox.Show(FindForm(), "Please analyze a 21990 file first.", "Apply Patches", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_xexData == null || _originalXexData == null)
            {
                MessageBox.Show(FindForm(), "Please load a XEX file first.", "Apply Patches", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Log("");
                Log("=== Applying Patches ===");

                // Restore from original XEX data to prevent stacking modifications
                // (e.g., fog ratios being divided multiple times)
                Array.Copy(_originalXexData, _xexData, _originalXexData.Length);
                Log("Restored XEX to original state before applying patches.");

                int totalPatched = 0;

                if (_chkApplyMenuData.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplyMenuDataToXex(_xexData, log);
                    foreach (var l in log) Log(l);
                }

                if (_chkApplySkyData.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplySkyData(_xexData, log, _chkSkyToFog.Checked, _chkApplyN64FogDistances.Checked);
                    foreach (var l in log) Log(l);
                }

                if (_chkApplyMusic.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplyMusicData(_xexData, log);
                    foreach (var l in log) Log(l);
                }

                Log($"=== Patching Complete (Total: {totalPatched}) ===");

                _hasUnsavedChanges = true;
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                MessageBox.Show(FindForm(), "Patches applied successfully!\n\nRemember to save the XEX from the main toolbar.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(FindForm(), ex.ToString(), "Patching Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
