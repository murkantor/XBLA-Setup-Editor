using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XBLA_Setup_Editor;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// UserControl for setup patching operations.
    /// Implements IXexTab for shared XEX state.
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
        private readonly CheckBox _chkAllowMp;
        private readonly CheckBox _chkAllowExtendXex;
        private readonly CheckBox _chkForceRepack;
        private readonly CheckBox _chkSplitTwoXex;
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
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 20, Padding = new Padding(8), AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

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
            var btnRunSingle = new Button { Text = "Run setupconv.exe (single)", Dock = DockStyle.Fill, Height = 34 };

            _txtBatchInputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtBatchOutputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseBatchIn = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseBatchOut = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunBatchSolo = new Button { Text = "Run batch (Solo)", Dock = DockStyle.Fill, Height = 34 };

            _chkPatchXex = new CheckBox { Text = "Patch loaded XEX after batch", AutoSize = true, Dock = DockStyle.Left };
            _chkAllowMp = new CheckBox { Text = "Use MP pool overflow", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkAllowExtendXex = new CheckBox { Text = "Extend XEX if needed", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _chkForceRepack = new CheckBox { Text = "Force Repack", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkSplitTwoXex = new CheckBox { Text = "Split across two XEX files", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _txtOutputXex1 = new TextBox { Dock = DockStyle.Fill };
            var btnBrowseOutputXex1 = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            _txtOutputXex2 = new TextBox { Dock = DockStyle.Fill };
            var btnBrowseOutputXex2 = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9) };

            void AddRow(int row, string label, Control mid, Control? right = null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(btnRunSingle, 0, r++);
            layout.SetColumnSpan(btnRunSingle, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, r++);
            layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 3);

            AddRow(r++, "Batch Input Dir", _txtBatchInputDir, btnBrowseBatchIn);
            AddRow(r++, "Batch Output Dir", _txtBatchOutputDir, btnBrowseBatchOut);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(btnRunBatchSolo, 0, r++);
            layout.SetColumnSpan(btnRunBatchSolo, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(_chkPatchXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(_chkAllowMp, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(_chkAllowExtendXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(_chkForceRepack, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
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
            toolTip.SetToolTip(_chkAllowMp, TooltipTexts.SetupPatching.AllowMpPool);
            toolTip.SetToolTip(_chkAllowExtendXex, TooltipTexts.SetupPatching.ExtendXex);
            toolTip.SetToolTip(_chkForceRepack, TooltipTexts.SetupPatching.ForceRepack);
            toolTip.SetToolTip(_chkSplitTwoXex, TooltipTexts.SetupPatching.SplitTwoXex);
            toolTip.SetToolTip(_txtOutputXex1, TooltipTexts.SetupPatching.OutputXex1);
            toolTip.SetToolTip(_txtOutputXex2, TooltipTexts.SetupPatching.OutputXex2);
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
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { MessageBox.Show(FindForm(), "Invalid input."); return; }
            if (string.IsNullOrWhiteSpace(output)) { MessageBox.Show(FindForm(), "Invalid output."); return; }
            if (string.IsNullOrWhiteSpace(offset)) { MessageBox.Show(FindForm(), "No offset."); return; }
            if (!TryGetSetupConvExe(out var exePath)) return;
            var ok = RunSetupConv(exePath, input, output, offset, _txtLog, out var exitCode);
            MessageBox.Show(FindForm(), ok && exitCode == 0 ? "Conversion complete." : "Failed.");
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
                if (!TryResolveInputFile(byName, baseName, out var inPath)) { _txtLog.AppendText($"MISS: {baseName}\r\n"); continue; }
                var outPath = Path.Combine(outDir, baseName + ".bin");
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
                bool allowMp = _chkAllowMp.Checked;
                bool allowExtend = _chkAllowExtendXex.Checked;
                bool forceRepack = _chkForceRepack.Checked;
                const int align = 0x10;
                const int extendChunk = 0x200000;

                if (_chkSplitTwoXex.Checked)
                {
                    var outXex1 = _txtOutputXex1.Text.Trim();
                    var outXex2 = _txtOutputXex2.Text.Trim();
                    if (string.IsNullOrWhiteSpace(outXex1)) { MessageBox.Show(FindForm(), "Invalid Output XEX #1."); return; }
                    if (string.IsNullOrWhiteSpace(outXex2)) { MessageBox.Show(FindForm(), "Invalid Output XEX #2."); return; }

                    _txtLog.AppendText("\r\n=== SPLIT PLAN ===\r\n");
                    XexSetupPatcher.PlanSplitAcrossTwoXex(_xexData, levelToBinSizePass1, allowMp, allowExtend, extendChunk, align, forceRepack, out var p1, out var r1, out var rem, out var p2, out var r2);

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
                        XexSetupPatcher.ApplyHybrid(tempXex, outXex1, p1, b1, allowExtend, out var a1);
                        XexSetupPatcher.ApplyHybrid(tempXex, outXex2, p2, b2, allowExtend, out var a2);

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
                    var p = XexSetupPatcher.PlanHybridPlacements(_xexData, levelToBinSizePass1, XexSetupPatcher.PriorityOrder, allowMp, allowExtend, extendChunk, align, forceRepack, out var r, out var not);

                    var repackDir = Path.Combine(outDir, "_repacked");
                    Directory.CreateDirectory(repackDir);
                    var b = BuildBlobsForPlacements(p, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);

                    // Apply to current XEX data
                    var tempPath = Path.GetTempFileName();
                    File.WriteAllBytes(tempPath, _xexData);
                    XexSetupPatcher.ApplyHybrid(tempPath, tempPath, p, b, allowExtend, out var a);
                    _xexData = File.ReadAllBytes(tempPath);
                    File.Delete(tempPath);

                    _hasUnsavedChanges = true;
                    XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                    var all = new List<string>();
                    all.AddRange(r);
                    all.AddRange(a);
                    if (not.Count > 0) { all.Add("NOTE: Some levels did not fit."); }
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
                Width = 800,
                Height = 600
            };
            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
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
                if (!levelToInputSetup.TryGetValue(p.LevelName, out var inputSet)) throw new InvalidOperationException($"Missing input for {p.LevelName}");
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
