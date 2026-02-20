// =============================================================================
// SetupPatchingControl.cs - Level Setup Conversion Tab
// =============================================================================
// UserControl that provides the "Setup Patching" tab for converting N64 level
// setup files to XBLA format and patching them into XEX files.
//
// TAB FUNCTIONALITY:
// ==================
// This tab enables two main workflows:
// 1. Single Conversion: Convert one setup file using setupconv.exe
// 2. Batch Conversion: Convert all setups in a folder, then patch into XEX
//
// SINGLE CONVERSION MODE:
// =======================
// - Select Solo or Multi mode (different memory address tables)
// - Choose level from dropdown (sets memory offset automatically)
// - Select input setup file (.set or extracted N64 setup)
// - Select output path for converted .bin file
// - Run setupconv.exe with the specified parameters
//
// BATCH CONVERSION MODE:
// ======================
// - Select input directory containing all setup files
// - Output directory is auto-created (XBLA subfolder)
// - Converts all recognized setups using predefined naming patterns
// - Optionally patches results into loaded XEX
//
// XEX PATCHING OPTIONS:
// =====================
// - Patch loaded XEX: Apply converted setups to current XEX
// - Use MP pool overflow: Allow setups to spill into multiplayer region
// - Extend XEX if needed: Append data to end of XEX (experimental)
// - Force Repack: Re-convert setups to their new addresses
// - Split across two XEX: Create two XEX files when all don't fit
//
// MEMORY ADDRESS TABLES:
// ======================
// Solo mode addresses (single-player levels):
//   Archives: 0x82CA1480, Control: 0x82CB1CF8, Dam: 0x82D115F0, etc.
//
// Multi mode addresses (multiplayer levels):
//   Library: 0x82DC5CC0, Complex: 0x82DE6D38, Temple: 0x82DE19D8, etc.
//
// FILE NAMING PATTERNS:
// =====================
// The batch converter recognizes these N64 setup file name patterns:
//   UsetuparchZ = Archives, UsetupcontrolZ = Control, UsetupdamZ = Dam,
//   UsetuparkZ = Facility, UsetupcaveZ = Caverns, etc.
//
// SETUPCONV.EXE:
// ==============
// External tool that converts N64 setup format to XBLA format.
// Usage: setupconv.exe <input> <output> <memory_address>
// Must be in application directory for this control to function.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XBLA_Setup_Editor;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// Tab control for converting N64 setup files to XBLA format using setupconv.exe
    /// and patching them into XEX files. Supports single and batch conversion modes.
    /// </summary>
    public sealed class SetupPatchingControl : UserControl, IXexTab
    {
        // --- UI ---
        private readonly RadioButton _rbSolo;
        private readonly RadioButton _rbMulti;
        private readonly ComboBox _cbLevel;
        private readonly TextBox _txtOffset;

        private readonly TextBox _txtInput;
        private readonly TextBox _txtOutput;

        // Batch (Solo)
        private readonly TextBox _txtBatchInputDir;
        private readonly TextBox _txtBatchOutputDir;

        // Optional XEX patching (Solo) - uses shared XEX when available
        private readonly CheckBox _chkPatchXex;
        private readonly CheckBox _chkCompactMpFirst;
        private readonly CheckBox _chkAllowMp;
        private readonly CheckBox _chkAllowExtendXex;
        private readonly CheckBox _chkForceRepack;
        private readonly CheckBox _chkSplitTwoXex;
        private readonly CheckBox _chkPatchStan;
        private readonly TextBox _txtOutputXex1;
        private readonly TextBox _txtOutputXex2;

        private readonly TextBox _txtLog;

        // State
        private byte[]? _xexData;
        private string? _xexPath;
        private bool _hasUnsavedChanges = false;

        // Events
        public event EventHandler<XexModifiedEventArgs>? XexModified;

        // --- Offsets (memory addresses for setupconv.exe) ---
        private readonly Dictionary<string, string> _soloOffsets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Archives"] = "82CA1480",
            ["Control"] = "82CB1CF8",
            ["Facility"] = "82CC8470",
            ["Aztec"] = "82CD9988",
            ["Caverns"] = "82CEF420",
            ["Cradle"] = "82CF4B08",
            ["Egyptian"] = "82CFDBD8",
            ["Dam"] = "82D115F0",
            ["Depot"] = "82D1EA40",
            ["Frigate"] = "82D2C3E8",
            ["Jungle"] = "82D44440",
            ["Cuba"] = "82D46898",
            ["Streets"] = "82D54C40",
            ["Runway"] = "82D5C238",
            ["Bunker (1)"] = "82D659C0",
            ["Bunker (2)"] = "82D74C10",
            ["Surface (1)"] = "82D857E8",
            ["Surface (2)"] = "82D93CD0",
            ["Silo"] = "82DA7AC8",
            ["Statue"] = "82DAE8C0",
            ["Train"] = "82DC1C50",
        };

        private readonly Dictionary<string, string> _multiOffsets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Library"] = "82DC5CC0",
            ["Archives"] = "82DC8640",
            ["Facility"] = "82DCB990",
            ["Stack"] = "82DCF048",
            ["Caverns"] = "82DD1D18",
            ["Egyptian"] = "82DD3508",
            ["Dam"] = "82DD6B00",
            ["Depot"] = "82DDA038",
            ["Frigate"] = "82DDF970",
            ["Temple"] = "82DE19D8",
            ["Basement"] = "82DE39A0",
            ["Caves"] = "82DE51F8",
            ["Complex"] = "82DE6D38",
            ["Runway"] = "82DE9028",
            ["Bunker"] = "82DECF18",
        };

        private readonly string[] _soloOrder = { "Archives", "Control", "Facility", "Aztec", "Caverns", "Cradle", "Egyptian", "Dam", "Depot", "Frigate", "Jungle", "Cuba", "Streets", "Runway", "Bunker (1)", "Bunker (2)", "Surface (1)", "Surface (2)", "Silo", "Statue", "Train" };
        private readonly string[] _multiOrder = { "Library", "Archives", "Facility", "Stack", "Caverns", "Egyptian", "Dam", "Depot", "Frigate", "Temple", "Basement", "Caves", "Complex", "Runway", "Bunker" };

        private readonly Dictionary<string, string> _soloBatchBaseToLevel = new(StringComparer.OrdinalIgnoreCase)
        {
            ["UsetuparchZ"] = "Archives",
            ["UsetupcontrolZ"] = "Control",
            ["UsetuparkZ"] = "Facility",
            ["UsetupaztZ"] = "Aztec",
            ["UsetupcaveZ"] = "Caverns",
            ["UsetupcradZ"] = "Cradle",
            ["UsetupcrypZ"] = "Egyptian",
            ["UsetupdamZ"] = "Dam",
            ["UsetupdepoZ"] = "Depot",
            ["UsetupdestZ"] = "Frigate",
            ["UsetupjunZ"] = "Jungle",
            ["UsetuplenZ"] = "Cuba",
            ["UsetuppeteZ"] = "Streets",
            ["UsetuprunZ"] = "Runway",
            ["UsetupsevbunkerZ"] = "Bunker (1)",
            ["UsetupsevbZ"] = "Bunker (2)",
            ["UsetupsevxZ"] = "Surface (1)",
            ["UsetupsevxbZ"] = "Surface (2)",
            ["UsetupsiloZ"] = "Silo",
            ["UsetupstatueZ"] = "Statue",
            ["UsetuptraZ"] = "Train",
        };

        public SetupPatchingControl()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 20, Padding = new Padding(DpiHelper.Scale(this, 8)), AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 180)));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 100)));

            _rbSolo = new RadioButton { Text = "Solo", Checked = true, AutoSize = true, Dock = DockStyle.Left };
            _rbMulti = new RadioButton { Text = "Multi", AutoSize = true, Dock = DockStyle.Left };
            var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            modePanel.Controls.Add(_rbSolo);
            modePanel.Controls.Add(_rbMulti);

            _cbLevel = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _txtOffset = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtInput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtOutput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };

            var btnBrowseInput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseOutput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunSingle = new Button { Text = "Run setupconv.exe (single)", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 34) };

            _txtBatchInputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtBatchOutputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseBatchIn = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseBatchOut = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunBatchSolo = new Button { Text = "Run batch (Solo)", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 34) };

            _chkPatchXex = new CheckBox { Text = "Patch loaded XEX after batch", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkCompactMpFirst = new CheckBox { Text = "Compact BG Data region before patching", AutoSize = true, Dock = DockStyle.Left, Checked = false, Enabled = true };
            _chkPatchStan = new CheckBox { Text = "Patch STAN Data", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _chkAllowMp = new CheckBox { Text = "Use Multiplayer region overflow", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkAllowExtendXex = new CheckBox { Text = "Extend XEX if needed", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _chkForceRepack = new CheckBox { Text = "Force Repack", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkSplitTwoXex = new CheckBox { Text = "Split across two XEX files", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _txtOutputXex1 = new TextBox { Dock = DockStyle.Fill };
            var btnBrowseOutputXex1 = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            _txtOutputXex2 = new TextBox { Dock = DockStyle.Fill };
            var btnBrowseOutputXex2 = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)) };

            void AddRow(int row, string label, Control mid, Control? right = null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
                layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
                layout.Controls.Add(mid, 1, row);
                if (right != null) layout.Controls.Add(right, 2, row);
            }

            int r = 0;
            AddRow(r++, "Mode", modePanel);
            AddRow(r++, "Level", _cbLevel);
            AddRow(r++, "Memory Offset", _txtOffset);
            AddRow(r++, "Input Setup", _txtInput, btnBrowseInput);
            AddRow(r++, "Output Setup", _txtOutput, btnBrowseOutput);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            layout.Controls.Add(btnRunSingle, 0, r++);
            layout.SetColumnSpan(btnRunSingle, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 8)));
            layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, r++);
            layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 3);

            AddRow(r++, "Batch Input Dir", _txtBatchInputDir, btnBrowseBatchIn);
            AddRow(r++, "Batch Output Dir", _txtBatchOutputDir, btnBrowseBatchOut);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            layout.Controls.Add(btnRunBatchSolo, 0, r++);
            layout.SetColumnSpan(btnRunBatchSolo, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkPatchXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkCompactMpFirst, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkPatchStan, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkAllowMp, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkAllowExtendXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkForceRepack, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            layout.Controls.Add(_chkSplitTwoXex, 1, r++);
            AddRow(r++, "Output XEX #1", _txtOutputXex1, btnBrowseOutputXex1);
            AddRow(r++, "Output XEX #2", _txtOutputXex2, btnBrowseOutputXex2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(_txtLog, 0, r++);
            layout.SetColumnSpan(_txtLog, 3);

            Controls.Add(layout);

            // Events
            _rbSolo.CheckedChanged += (_, __) => RefreshLevelList();
            _rbMulti.CheckedChanged += (_, __) => RefreshLevelList();
            _cbLevel.SelectedIndexChanged += (_, __) => RefreshOffset();

            btnBrowseInput.Click += (_, __) => { using var ofd = new OpenFileDialog { Filter = "Setup files|*.set;*.bin;*.*" }; if (ofd.ShowDialog(FindForm()) == DialogResult.OK) _txtInput.Text = ofd.FileName; };
            btnBrowseOutput.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "BIN (*.bin)|*.bin" }; if (sfd.ShowDialog(FindForm()) == DialogResult.OK) _txtOutput.Text = sfd.FileName; };
            btnRunSingle.Click += (_, __) => RunSingleConversion();

            btnBrowseBatchIn.Click += (_, __) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Select batch input folder (Solo)" };
                if (fbd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _txtBatchInputDir.Text = fbd.SelectedPath;
                    string xblaDir = Path.Combine(fbd.SelectedPath, "XBLA");
                    _txtBatchOutputDir.Text = xblaDir;

                    // Auto-populate Output XEX names based on folder name
                    var folderName = Path.GetFileName(fbd.SelectedPath);
                    if (!string.IsNullOrWhiteSpace(folderName) && !string.IsNullOrWhiteSpace(_xexPath))
                    {
                        var xexDir = Path.GetDirectoryName(_xexPath) ?? "";
                        var xexBaseName = Path.GetFileNameWithoutExtension(_xexPath);
                        _txtOutputXex1.Text = Path.Combine(xexDir, $"{xexBaseName}_{folderName}1.xex");
                        _txtOutputXex2.Text = Path.Combine(xexDir, $"{xexBaseName}_{folderName}2.xex");
                    }
                }
            };

            btnBrowseBatchOut.Click += (_, __) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog(FindForm()) == DialogResult.OK) _txtBatchOutputDir.Text = fbd.SelectedPath; };
            btnBrowseOutputXex1.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "XEX files (*.xex)|*.xex" }; if (sfd.ShowDialog(FindForm()) == DialogResult.OK) _txtOutputXex1.Text = sfd.FileName; };
            btnBrowseOutputXex2.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "XEX files (*.xex)|*.xex" }; if (sfd.ShowDialog(FindForm()) == DialogResult.OK) _txtOutputXex2.Text = sfd.FileName; };

            btnRunBatchSolo.Click += (_, __) => RunBatchSolo();

            _chkPatchXex.CheckedChanged += (_, __) =>
            {
                _chkCompactMpFirst.Enabled = _chkPatchXex.Checked;
                _chkPatchStan.Enabled = _chkPatchXex.Checked;
            };

            _chkSplitTwoXex.CheckedChanged += (_, __) =>
            {
                bool split = _chkSplitTwoXex.Checked;
                _txtOutputXex1.Enabled = split;
                btnBrowseOutputXex1.Enabled = split;
                _txtOutputXex2.Enabled = split;
                btnBrowseOutputXex2.Enabled = split;
            };

            // Initially disable split output fields
            _txtOutputXex1.Enabled = _chkSplitTwoXex.Checked;
            btnBrowseOutputXex1.Enabled = _chkSplitTwoXex.Checked;
            _txtOutputXex2.Enabled = _chkSplitTwoXex.Checked;
            btnBrowseOutputXex2.Enabled = _chkSplitTwoXex.Checked;

            RefreshLevelList();
            SetupTooltips();
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_rbSolo, TooltipTexts.SetupPatching.SoloMode);
            toolTip.SetToolTip(_rbMulti, TooltipTexts.SetupPatching.MultiMode);
            toolTip.SetToolTip(_cbLevel, TooltipTexts.SetupPatching.LevelDropdown);
            toolTip.SetToolTip(_txtOffset, TooltipTexts.SetupPatching.MemoryOffset);
            toolTip.SetToolTip(_txtInput, TooltipTexts.SetupPatching.InputSetup);
            toolTip.SetToolTip(_txtOutput, TooltipTexts.SetupPatching.OutputSetup);
            toolTip.SetToolTip(_txtBatchInputDir, TooltipTexts.SetupPatching.BatchInputDir);
            toolTip.SetToolTip(_txtBatchOutputDir, TooltipTexts.SetupPatching.BatchOutputDir);
            toolTip.SetToolTip(_chkPatchXex, TooltipTexts.SetupPatching.PatchXex);
            toolTip.SetToolTip(_chkCompactMpFirst,
                "Before placing setups, automatically compact the BG Data region by removing " +
                "Library/Basement/Stack, Citadel, Caves, Complex, and Temple entries, " +
                "then fix the BG pointers in the Level ID Setup table. " +
                "This frees space in the BG Data region and applies both steps in one go.");
            toolTip.SetToolTip(_chkPatchStan,
                "Also patch STAN (clipping) data blobs into the XEX. " +
                "STAN blobs are extracted from the loaded XEX and relocated as needed. " +
                "If 'Compact BG Data region' is also enabled, STAN blobs can use that freed space too.");
            toolTip.SetToolTip(_chkAllowMp, TooltipTexts.SetupPatching.AllowMpPool);
            toolTip.SetToolTip(_chkAllowExtendXex, TooltipTexts.SetupPatching.ExtendXex);
            toolTip.SetToolTip(_chkForceRepack, TooltipTexts.SetupPatching.ForceRepack);
            toolTip.SetToolTip(_chkSplitTwoXex, TooltipTexts.SetupPatching.SplitTwoXex);
            toolTip.SetToolTip(_txtOutputXex1, TooltipTexts.SetupPatching.OutputXex1);
            toolTip.SetToolTip(_txtOutputXex2, TooltipTexts.SetupPatching.OutputXex2);
        }

        /// <summary>
        /// The name of the currently selected batch input folder, or null if none set.
        /// Used by MainForm to suggest a save filename matching the batch folder.
        /// </summary>
        public string? BatchFolderName
        {
            get
            {
                var dir = _txtBatchInputDir.Text.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrWhiteSpace(dir) ? null : Path.GetFileName(dir);
            }
        }

        #region IXexTab Implementation

        public string TabDisplayName => "Setup Patching";

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void OnXexLoaded(byte[] xexData, string path)
        {
            _xexData = xexData;
            _xexPath = path;
            _txtLog.AppendText($"XEX loaded: {path} ({xexData.Length:N0} bytes)\r\n");
        }

        public void OnXexDataUpdated(byte[] xexData)
        {
            // Lightweight update - just refresh the data reference
            _xexData = xexData;
        }

        public void OnXexUnloaded()
        {
            _xexData = null;
            _xexPath = null;
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

        private void RefreshLevelList()
        {
            _cbLevel.Items.Clear();
            var list = _rbSolo.Checked ? _soloOrder : _multiOrder;
            foreach (var name in list) _cbLevel.Items.Add(name);
            if (_cbLevel.Items.Count > 0) _cbLevel.SelectedIndex = 0;
            RefreshOffset();
        }

        private void RefreshOffset()
        {
            var level = _cbLevel.SelectedItem?.ToString() ?? string.Empty;
            string? offset = null;
            if (_rbSolo.Checked) _soloOffsets.TryGetValue(level, out offset);
            else _multiOffsets.TryGetValue(level, out offset);
            _txtOffset.Text = offset ?? string.Empty;
        }

        private void RunSingleConversion()
        {
            _txtLog.Clear();
            var input = _txtInput.Text.Trim();
            var output = _txtOutput.Text.Trim();
            var offset = _txtOffset.Text.Trim();
            var levelName = _cbLevel.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { MessageBox.Show(FindForm(), "Invalid input."); return; }
            if (string.IsNullOrWhiteSpace(output)) { MessageBox.Show(FindForm(), "Invalid output."); return; }
            if (string.IsNullOrWhiteSpace(offset)) { MessageBox.Show(FindForm(), "No offset."); return; }
            if (!TryGetSetupConvExe(out var exePath)) return;
            var ok = RunSetupConv(exePath, input, output, offset, _txtLog, out var exitCode);
            if (!ok || exitCode != 0) { MessageBox.Show(FindForm(), "Failed."); return; }

            if (_xexData == null || string.IsNullOrWhiteSpace(_xexPath))
            {
                MessageBox.Show(FindForm(), "Conversion complete. (No XEX loaded - setup not patched into XEX)");
                return;
            }

            try
            {
                var binData = File.ReadAllBytes(output);
                var levelToSize = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [levelName] = binData.Length };
                var pass1Blobs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase) { [levelName] = binData };
                var levelToInput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [levelName] = input };

                const int align = 0x10;
                const int extendChunk = 0x200000;
                var placements = XexSetupPatcher.PlanHybridPlacements(
                    _xexData, levelToSize, XexSetupPatcher.PriorityOrder,
                    allowMp: false, allowExtendXex: false, extendChunkBytes: extendChunk,
                    align: align, forceRepack: true,
                    out _, out var notPlaced);

                var outDir = Path.GetDirectoryName(output) ?? ".";
                var repackDir = Path.Combine(outDir, "_repacked");
                Directory.CreateDirectory(repackDir);
                var blobs = BuildBlobsForPlacements(placements, pass1Blobs, levelToInput, exePath, repackDir, _txtLog);

                var tempPath = Path.GetTempFileName();
                File.WriteAllBytes(tempPath, _xexData);
                XexSetupPatcher.ApplyHybrid(tempPath, tempPath, placements, blobs, allowExtendXex: false, updateMenuAndBriefing: false, out _);
                _xexData = File.ReadAllBytes(tempPath);
                File.Delete(tempPath);

                _hasUnsavedChanges = true;
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                if (notPlaced.Count > 0)
                    MessageBox.Show(FindForm(), $"Conversion complete, but {levelName} did not fit in the XEX.");
                else
                    MessageBox.Show(FindForm(), $"Conversion complete. {levelName} patched into XEX - press Save As to save.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Patch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunBatchSolo()
        {
            _txtLog.Clear();
            if (!_rbSolo.Checked) { MessageBox.Show(FindForm(), "Batch mode is Solo-only."); return; }
            var inDir = _txtBatchInputDir.Text.Trim();
            var outDir = _txtBatchOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir)) { MessageBox.Show(FindForm(), "Invalid batch input."); return; }
            if (string.IsNullOrWhiteSpace(outDir)) { MessageBox.Show(FindForm(), "Invalid batch output."); return; }
            Directory.CreateDirectory(outDir);
            if (!TryGetSetupConvExe(out var exePath)) return;

            var allFiles = Directory.GetFiles(inDir);
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in allFiles)
            {
                var name = Path.GetFileName(path);
                var baseName = Path.GetFileNameWithoutExtension(path);
                if (!byName.ContainsKey(name)) byName[name] = path;
                if (!byName.ContainsKey(baseName)) byName[baseName] = path;
            }

            var levelToInputSetup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinPathPass1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinSizePass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int okCount = 0, failCount = 0;

            foreach (var kvp in _soloBatchBaseToLevel)
            {
                var baseName = kvp.Key;
                var level = kvp.Value;
                if (!_soloOffsets.TryGetValue(level, out var originalVa)) { _txtLog.AppendText($"SKIP: No VA for {level}\r\n"); continue; }

                var outPath = Path.Combine(outDir, baseName + ".bin");

                // Cuba cannot be converted by setupconv (crashes the game).
                // Extract the bytes from the loaded XEX. On a previously-patched XEX Cuba may have
                // been moved, so resolve its current location via the SP pointer rather than always
                // using the vanilla hardcoded offset (which may now hold a different level's data).
                if (level.Equals("Cuba", StringComparison.OrdinalIgnoreCase))
                {
                    _txtLog.AppendText($"--- {level} ---\r\nVA : {originalVa} (extract from XEX)\r\n");
                    if (_xexData != null)
                    {
                        // Resolve Cuba's actual current file offset from the live SP pointer.
                        int cubaOffset = XexSetupPatcher.CubaFileOffset; // vanilla fallback
                        if (XexSetupPatcher.SpPointerOffsets.TryGetValue("Cuba", out int cubaPtrOff)
                            && cubaPtrOff + 4 <= _xexData.Length)
                        {
                            uint cubaVa = (uint)((_xexData[cubaPtrOff] << 24) | (_xexData[cubaPtrOff + 1] << 16)
                                               | (_xexData[cubaPtrOff + 2] << 8) | _xexData[cubaPtrOff + 3]);
                            if (cubaVa >= XexSetupPatcher.VaBase)
                            {
                                int resolved = (int)(cubaVa - XexSetupPatcher.VaBase);
                                if (resolved >= 0 && resolved + XexSetupPatcher.CubaSize <= _xexData.Length)
                                {
                                    cubaOffset = resolved;
                                    _txtLog.AppendText($"  SP pointer -> 0x{cubaVa:X8}  (file 0x{cubaOffset:X7})\r\n");
                                }
                            }
                        }

                        if (cubaOffset + XexSetupPatcher.CubaSize <= _xexData.Length)
                        {
                            var cubaBytes = new byte[XexSetupPatcher.CubaSize];
                            Buffer.BlockCopy(_xexData, cubaOffset, cubaBytes, 0, XexSetupPatcher.CubaSize);
                            File.WriteAllBytes(outPath, cubaBytes);
                            _txtLog.AppendText($"EXTRACT: {XexSetupPatcher.CubaSize} bytes from 0x{cubaOffset:X7} -> {outPath}\r\n\r\n");
                            okCount++;
                            levelToInputSetup[level] = string.Empty; // no N64 source - extracted from XEX
                            levelToBinPathPass1[level] = outPath;
                            levelToBinSizePass1[level] = XexSetupPatcher.CubaSize;
                        }
                        else
                        {
                            _txtLog.AppendText("SKIP Cuba: resolved offset is out of range.\r\n\r\n");
                            failCount++;
                        }
                    }
                    else
                    {
                        _txtLog.AppendText("SKIP Cuba: no XEX loaded.\r\n\r\n");
                        failCount++;
                    }
                    continue;
                }

                if (!TryResolveInputFile(byName, baseName, out var inPath)) { _txtLog.AppendText($"MISS: {baseName}\r\n"); continue; }
                _txtLog.AppendText($"--- {level} ---\r\nIN : {inPath}\r\nOUT: {outPath}\r\nVA : {originalVa}\r\n");

                var ok = RunSetupConv(exePath, inPath, outPath, originalVa, _txtLog, out var exitCode);
                if (ok && exitCode == 0 && File.Exists(outPath))
                {
                    okCount++;
                    levelToInputSetup[level] = inPath;
                    levelToBinPathPass1[level] = outPath;
                    levelToBinSizePass1[level] = (int)new FileInfo(outPath).Length;
                }
                else failCount++;
                _txtLog.AppendText("\r\n");
            }
            _txtLog.AppendText($"Batch done. Success={okCount}  Failed={failCount}\r\n");

            if (!_chkPatchXex.Checked) { MessageBox.Show(FindForm(), "Batch complete."); return; }

            if (_xexData == null || string.IsNullOrWhiteSpace(_xexPath))
            {
                MessageBox.Show(FindForm(), "No XEX loaded. Load a XEX file from the main toolbar first.");
                return;
            }

            var pass1Blobs = levelToBinPathPass1.Where(kv => File.Exists(kv.Value)).ToDictionary(kv => kv.Key, kv => File.ReadAllBytes(kv.Value), StringComparer.OrdinalIgnoreCase);

            try
            {
                // --- Optional: compact MP setup region before planning ---
                IReadOnlyList<(int Start, int EndExclusive)>? extraPoolSegs = null;
                if (_chkCompactMpFirst.Checked)
                {
                    _txtLog.AppendText("\r\n=== AUTO BG DATA COMPACTION ===\r\n");
                    var compacted = MpSetupCompactor.Compact(
                        _xexData, MpSetupCompactor.DefaultRemove,
                        out var newLayout, out var compactReport);
                    _txtLog.AppendText(string.Join("\r\n", compactReport) + "\r\n");

                    MpSetupCompactor.FixBgPointers(compacted, newLayout, out var bgReport);
                    _txtLog.AppendText(string.Join("\r\n", bgReport) + "\r\n");

                    _xexData = compacted;

                    // Expose the freed tail as an extra pool for setup placement
                    if (newLayout.Count > 0)
                    {
                        int freedStart = newLayout[newLayout.Count - 1].FileOffset + newLayout[newLayout.Count - 1].Size;
                        int freedEnd   = MpSetupCompactor.RegionEnd;
                        if (freedEnd > freedStart)
                        {
                            extraPoolSegs = new List<(int, int)> { (freedStart, freedEnd) };
                            _txtLog.AppendText($"BG Data freed tail: 0x{freedStart:X7}–0x{freedEnd:X7}  ({freedEnd - freedStart:N0} bytes available for setup placement)\r\n");
                        }
                    }

                    XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));
                    _txtLog.AppendText("=== BG DATA COMPACTION DONE ===\r\n\r\n");
                }

                bool allowMp = _chkAllowMp.Checked;
                bool allowExtend = _chkAllowExtendXex.Checked;
                bool forceRepack = _chkForceRepack.Checked;
                const int align = 0x10;
                const int extendChunk = 0x200000;

                // --- STAN blob extraction ---
                var levelToStanBlob = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                if (_chkPatchStan.Checked)
                {
                    _txtLog.AppendText("\r\n=== STAN DATA EXTRACTION ===\r\n");
                    foreach (var region in XexSetupPatcher.StanSoloRegions)
                    {
                        int off = region.OriginalFileOffset;
                        if (off + 4 > _xexData.Length) { _txtLog.AppendText($"  SKIP {region.Name}: offset out of range\r\n"); continue; }
                        int blobSize = (int)(((uint)_xexData[off] << 24) | ((uint)_xexData[off + 1] << 16) | ((uint)_xexData[off + 2] << 8) | _xexData[off + 3]);
                        if (blobSize <= 0 || off + blobSize > _xexData.Length) { _txtLog.AppendText($"  SKIP {region.Name}: invalid size 0x{blobSize:X}\r\n"); continue; }
                        var blob = new byte[blobSize];
                        Buffer.BlockCopy(_xexData, off, blob, 0, blobSize);
                        levelToStanBlob[region.Name] = blob;
                        _txtLog.AppendText($"  {region.Name,-14} 0x{off:X7}  {blobSize:N0} bytes\r\n");
                    }
                    _txtLog.AppendText($"Extracted {levelToStanBlob.Count} STAN blobs.\r\n");
                }

                if (_chkSplitTwoXex.Checked)
                {
                    var outXex1 = _txtOutputXex1.Text.Trim();
                    var outXex2 = _txtOutputXex2.Text.Trim();
                    if (string.IsNullOrWhiteSpace(outXex1)) { MessageBox.Show(FindForm(), "Invalid Output XEX #1."); return; }
                    if (string.IsNullOrWhiteSpace(outXex2)) { MessageBox.Show(FindForm(), "Invalid Output XEX #2."); return; }

                    _txtLog.AppendText("\r\n=== SPLIT PLAN ===\r\n");
                    XexSetupPatcher.PlanSplitAcrossTwoXex(_xexData, levelToBinSizePass1, allowMp, allowExtend, extendChunk, align, forceRepack, new[] { "Cuba" }, extraPoolSegs, out var p1, out var r1, out var rem, out var p2, out var r2);

                    var repackDir = Path.Combine(outDir, "_repacked");
                    Directory.CreateDirectory(repackDir);
                    var b1 = BuildBlobsForPlacements(p1, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);
                    var b2 = BuildBlobsForPlacements(p2, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);

                    // Write in-memory XEX data (which may have 21990 patches) to a temp file
                    // This ensures both outputs include any modifications made in other tabs
                    var tempXex = Path.GetTempFileName();
                    File.WriteAllBytes(tempXex, _xexData);

                    try
                    {
                        List<string> a1, a2;
                        if (_chkPatchStan.Checked && levelToStanBlob.Count > 0)
                        {
                            var stanSizes  = levelToStanBlob.ToDictionary(kv => kv.Key, kv => kv.Value.Length, StringComparer.OrdinalIgnoreCase);
                            var xex1Levels = p1.Select(pl => pl.LevelName).Where(levelToStanBlob.ContainsKey).ToList();
                            var xex2Levels = p2.Select(pl => pl.LevelName).Where(levelToStanBlob.ContainsKey).ToList();
                            var stanP1 = XexSetupPatcher.PlanStanPlacements(stanSizes, xex1Levels, align, forceRepack, extraPoolSegs, out var sr1, out _);
                            var stanP2 = XexSetupPatcher.PlanStanPlacements(stanSizes, xex2Levels, align, forceRepack, extraPoolSegs, out var sr2, out _);
                            r1.Add(""); r1.AddRange(sr1);
                            r2.Add(""); r2.AddRange(sr2);
                            XexSetupPatcher.ApplyHybrid(tempXex, outXex1, p1, b1, stanP1, levelToStanBlob, allowExtend, null, out a1);
                            XexSetupPatcher.ApplyHybrid(tempXex, outXex2, p2, b2, stanP2, levelToStanBlob, allowExtend, null, out a2);
                        }
                        else
                        {
                            XexSetupPatcher.ApplyHybrid(tempXex, outXex1, p1, b1, allowExtend, out a1);
                            XexSetupPatcher.ApplyHybrid(tempXex, outXex2, p2, b2, allowExtend, out a2);
                        }

                        var combined = new List<string>();
                        combined.AddRange(r1);
                        combined.Add("");
                        combined.Add($"=== APPLY #1: {Path.GetFileName(outXex1)} ===");
                        combined.AddRange(a1);
                        combined.Add("");
                        combined.AddRange(r2);
                        combined.Add("");
                        combined.Add($"=== APPLY #2: {Path.GetFileName(outXex2)} ===");
                        combined.AddRange(a2);

                        // Report whether the compacted MP tail was actually needed
                        if (extraPoolSegs != null)
                        {
                            var inMpTail1 = p1.Where(pl => pl.Region == XexSetupPatcher.RegionKind.CompactedMpTail).ToList();
                            var inMpTail2 = p2.Where(pl => pl.Region == XexSetupPatcher.RegionKind.CompactedMpTail).ToList();
                            var allInTail = inMpTail1.Concat(inMpTail2).ToList();
                            combined.Add("");
                            combined.Add("=== COMPACTED BG DATA REGION USAGE ===");
                            if (allInTail.Count > 0)
                            {
                                combined.Add($"  BG Data compaction was needed — {allInTail.Count} setup(s) placed in freed region:");
                                foreach (var pl in allInTail)
                                    combined.Add($"    {pl.LevelName,-16}  0x{pl.FileOffset:X7}  ({pl.Size:N0} bytes)");
                            }
                            else
                            {
                                combined.Add("  BG Data compaction was not needed — all setups fit in standard pools.");
                            }
                        }

                        ShowReport("Split Report", combined);

                        // Offer to create xdelta patches for split XEX files
                        var mainForm = FindForm() as MainForm;
                        var originalData = mainForm?.GetOriginalXexData();
                        if (originalData != null)
                        {
                            XdeltaHelper.OfferCreateSplitPatches(FindForm()!, originalData, outXex1, outXex2);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempXex))
                            File.Delete(tempXex);
                    }
                }
                else
                {
                    _txtLog.AppendText("\r\n=== SINGLE PLAN ===\r\n");
                    var p = XexSetupPatcher.PlanHybridPlacements(_xexData, levelToBinSizePass1, XexSetupPatcher.PriorityOrder, allowMp, allowExtend, extendChunk, align, forceRepack, new[] { "Cuba" }, extraPoolSegs, out var r, out var not);

                    var repackDir = Path.Combine(outDir, "_repacked");
                    Directory.CreateDirectory(repackDir);
                    var b = BuildBlobsForPlacements(p, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);

                    // Apply to current XEX data
                    var tempPath = Path.GetTempFileName();
                    File.WriteAllBytes(tempPath, _xexData);
                    List<string> a;
                    if (_chkPatchStan.Checked && levelToStanBlob.Count > 0)
                    {
                        var stanCandidates = p.Select(pl => pl.LevelName).Where(levelToStanBlob.ContainsKey).ToList();
                        var stanSizes = stanCandidates.ToDictionary(l => l, l => levelToStanBlob[l].Length, StringComparer.OrdinalIgnoreCase);
                        var stanP = XexSetupPatcher.PlanStanPlacements(stanSizes, stanCandidates, align, forceRepack, extraPoolSegs, out var stanR, out _);
                        r.AddRange(stanR);
                        XexSetupPatcher.ApplyHybrid(tempPath, tempPath, p, b, stanP, levelToStanBlob, allowExtend, null, out a);
                    }
                    else
                    {
                        XexSetupPatcher.ApplyHybrid(tempPath, tempPath, p, b, allowExtend, out a);
                    }
                    _xexData = File.ReadAllBytes(tempPath);
                    File.Delete(tempPath);

                    _hasUnsavedChanges = true;
                    XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                    var all = new List<string>();
                    all.AddRange(r);
                    all.AddRange(a);
                    if (not.Count > 0)
                    {
                        int totalBytes   = levelToBinSizePass1.Values.Sum();
                        int placedBytes  = p.Sum(pl => pl.Size);
                        int unplacedBytes = not.Where(levelToBinSizePass1.ContainsKey).Sum(l => levelToBinSizePass1[l]);
                        int extraSegsBytes = extraPoolSegs?.Sum(s => s.EndExclusive - s.Start) ?? 0;
                        int spPoolBytes  = XexSetupPatcher.MpHeadersStart - XexSetupPatcher.SharedReadOnlyEndExclusive;
                        int mpPoolBytes  = XexSetupPatcher.SetupBlocksEndExclusive - XexSetupPatcher.MpHeadersStart;
                        int poolTotal    = extraSegsBytes + spPoolBytes + (allowMp ? mpPoolBytes : 0);

                        all.Add("");
                        all.Add("=== OVERFLOW DIAGNOSTICS ===");
                        all.Add($"  Total setup bytes : {totalBytes:N0}  (0x{totalBytes:X})");
                        all.Add($"  Placed bytes      : {placedBytes:N0}  (0x{placedBytes:X})");
                        all.Add($"  Unplaced bytes    : {unplacedBytes:N0}  (0x{unplacedBytes:X})  [{not.Count} level(s)]");
                        if (extraSegsBytes > 0)
                            all.Add($"  Compacted BG Data tail : {extraSegsBytes:N0}  (0x{extraSegsBytes:X})");
                        all.Add($"  SP pool capacity       : {spPoolBytes:N0}  (0x{spPoolBytes:X})");
                        if (allowMp)
                            all.Add($"  Multiplayer pool cap.  : {mpPoolBytes:N0}  (0x{mpPoolBytes:X})");
                        all.Add($"  Total pool        : {poolTotal:N0}  (0x{poolTotal:X})");
                        int shortfall = totalBytes - poolTotal;
                        all.Add($"  Shortfall         : {shortfall:N0} bytes  (0x{shortfall:X}) more space needed");
                        all.Add("");
                        all.Add("  Levels not placed:");
                        foreach (var l in not)
                        {
                            int sz = levelToBinSizePass1.TryGetValue(l, out var s) ? s : 0;
                            all.Add($"    {l,-16} {sz:N0} bytes  (0x{sz:X})");
                        }
                    }
                    // Report whether the compacted MP tail was actually needed
                    if (extraPoolSegs != null)
                    {
                        var inMpTail = p.Where(pl => pl.Region == XexSetupPatcher.RegionKind.CompactedMpTail).ToList();
                        all.Add("");
                        all.Add("=== COMPACTED BG DATA REGION USAGE ===");
                        if (inMpTail.Count > 0)
                        {
                            all.Add($"  BG Data compaction was needed — {inMpTail.Count} setup(s) placed in freed region:");
                            foreach (var pl in inMpTail)
                                all.Add($"    {pl.LevelName,-16}  0x{pl.FileOffset:X7}  ({pl.Size:N0} bytes)");
                        }
                        else
                        {
                            all.Add("  BG Data compaction was not needed — all setups fit in standard pools.");
                        }
                    }

                    ShowReport("Single Report", all);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowReport(string title, IEnumerable<string> lines)
        {
            var form = new Form
            {
                Text = title,
                Width = DpiHelper.Scale(this, 800),
                Height = DpiHelper.Scale(this, 600)
            };
            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)),
                Text = string.Join(Environment.NewLine, lines)
            };
            form.Controls.Add(textBox);
            form.ShowDialog(FindForm());
        }

        private static Dictionary<string, byte[]> BuildBlobsForPlacements(IReadOnlyList<XexSetupPatcher.Placement> placements, IReadOnlyDictionary<string, byte[]> pass1Blobs, IReadOnlyDictionary<string, string> levelToInputSetup, string exePath, string repackDir, TextBox log)
        {
            var blobs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in placements)
            {
                if (!p.RequiresRepack) { if (pass1Blobs.TryGetValue(p.LevelName, out var b)) blobs[p.LevelName] = b; continue; }
                if (!levelToInputSetup.TryGetValue(p.LevelName, out var inputSet) || string.IsNullOrEmpty(inputSet))
                {
                    // No N64 source available (e.g. Cuba - extracted from XEX). Use pass1 blob as-is.
                    if (pass1Blobs.TryGetValue(p.LevelName, out var b)) { blobs[p.LevelName] = b; log.AppendText($"NOTE: {p.LevelName} has no source setup and cannot be repacked; vanilla bytes used.\r\n"); }
                    continue;
                }
                if (!File.Exists(inputSet)) throw new InvalidOperationException($"Missing input for {p.LevelName}");
                string outBin = Path.Combine(repackDir, SanitizeFileName(p.LevelName) + $"_{p.NewVa:X8}.bin");
                string vaHex = p.NewVa.ToString("X8");
                log.AppendText($"REPACK: {p.LevelName} VA={vaHex}\r\n");
                var ok = RunSetupConv(exePath, inputSet, outBin, vaHex, log, out var exit);
                if (!ok || exit != 0) throw new InvalidOperationException($"Repack failed: {p.LevelName}");
                blobs[p.LevelName] = File.ReadAllBytes(outBin);
            }
            return blobs;
        }

        private static string SanitizeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        private static bool TryResolveInputFile(Dictionary<string, string> byName, string baseName, out string path)
        {
            if (byName.TryGetValue(baseName, out path!)) return true;
            if (byName.TryGetValue(baseName + ".set", out path!)) return true;
            path = "";
            return false;
        }

        private bool TryGetSetupConvExe(out string exePath)
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setupconv.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show(FindForm(), "setupconv.exe missing from application directory.");
                return false;
            }
            return true;
        }

        private static bool RunSetupConv(string exe, string inp, string outp, string off, TextBox log, out int code)
        {
            code = -1;
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"\"{inp}\" \"{outp}\" {off}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                log.AppendText(p.StandardOutput.ReadToEnd() ?? "");
                log.AppendText(p.StandardError.ReadToEnd() ?? "");
                p.WaitForExit();
                code = p.ExitCode;
                return true;
            }
            catch (Exception ex)
            {
                log.AppendText("EXC: " + ex);
                return false;
            }
        }
    }
}
