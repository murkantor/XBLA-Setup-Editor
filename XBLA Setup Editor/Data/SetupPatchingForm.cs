using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public sealed class SetupPatchingForm : Form
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

        // Optional XEX patching (Solo)
        private readonly CheckBox _chkPatchXex;
        private readonly TextBox _txtInputXex;
        private readonly TextBox _txtOutputXex;

        // Hybrid options
        private readonly CheckBox _chkAllowMp;
        private readonly CheckBox _chkAllowExtendXex;

        private readonly CheckBox _chkSplitTwoXex;
        private readonly TextBox _txtOutputXex2;

        private readonly TextBox _txtLog;

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

        // Display order (Solo)
        private readonly string[] _soloOrder =
        {
            "Archives","Control","Facility","Aztec","Caverns","Cradle","Egyptian","Dam","Depot","Frigate",
            "Jungle","Cuba","Streets","Runway","Bunker (1)","Bunker (2)","Surface (1)","Surface (2)","Silo","Statue","Train"
        };

        private readonly string[] _multiOrder =
        {
            "Library","Archives","Facility","Stack","Caverns","Egyptian","Dam","Depot","Frigate","Temple","Basement","Caves","Complex","Runway","Bunker"
        };

        // --- Correct Solo batch mapping ---
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

        public SetupPatchingForm()
        {
            Text = "Setup Patching (setupconv.exe)";
            Width = 980;
            Height = 760;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 20,
                Padding = new Padding(12),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

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

            // Batch Solo UI
            _txtBatchInputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtBatchOutputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseBatchIn = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseBatchOut = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunBatchSolo = new Button { Text = "Run batch (Solo)", Dock = DockStyle.Fill, Height = 34 };

            // Optional XEX patch UI
            _chkPatchXex = new CheckBox { Text = "Patch XEX after batch (Solo)", AutoSize = true, Dock = DockStyle.Left };
            _txtInputXex = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtOutputXex = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseInputXex = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseOutputXex = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _chkAllowMp = new CheckBox { Text = "Use MP pool overflow (destructive)", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkAllowExtendXex = new CheckBox { Text = "Extend XEX if needed (adds data beyond file, updates headers)", AutoSize = true, Dock = DockStyle.Left, Checked = false };

            _chkSplitTwoXex = new CheckBox { Text = "If not enough room: split across TWO output XEX files", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _txtOutputXex2 = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseOutputXex2 = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9)
            };

            void AddRow(int row, string label, Control mid, Control? right = null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                layout.Controls.Add(new Label
                {
                    Text = label,
                    Dock = DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                }, 0, row);
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

            // Spacer
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
            layout.Controls.Add(_chkSplitTwoXex, 1, r++);

            AddRow(r++, "Input XEX", _txtInputXex, btnBrowseInputXex);
            AddRow(r++, "Output XEX #1", _txtOutputXex, btnBrowseOutputXex);
            AddRow(r++, "Output XEX #2", _txtOutputXex2, btnBrowseOutputXex2);

            // Log
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(_txtLog, 0, r++);
            layout.SetColumnSpan(_txtLog, 3);

            Controls.Add(layout);

            // Events
            _rbSolo.CheckedChanged += (_, __) => RefreshLevelList();
            _rbMulti.CheckedChanged += (_, __) => RefreshLevelList();
            _cbLevel.SelectedIndexChanged += (_, __) => RefreshOffset();

            btnBrowseInput.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select input setup file",
                    Filter = "Setup files (*.set;*.bin;*.dat;*.setup;*.*)|*.set;*.bin;*.dat;*.setup;*.*|All files (*.*)|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                    _txtInput.Text = ofd.FileName;
            };

            btnBrowseOutput.Click += (_, __) =>
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Select output setup file",
                    Filter = "BIN (*.bin)|*.bin|All files (*.*)|*.*"
                };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                    _txtOutput.Text = sfd.FileName;
            };

            btnRunSingle.Click += (_, __) => RunSingleConversion();

            btnBrowseBatchIn.Click += (_, __) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Select batch input folder (Solo)" };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                    _txtBatchInputDir.Text = fbd.SelectedPath;
            };

            btnBrowseBatchOut.Click += (_, __) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Select batch output folder (Solo)" };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                    _txtBatchOutputDir.Text = fbd.SelectedPath;
            };

            btnBrowseInputXex.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select input XEX",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                    _txtInputXex.Text = ofd.FileName;
            };

            btnBrowseOutputXex.Click += (_, __) =>
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Select output XEX #1",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                    _txtOutputXex.Text = sfd.FileName;
            };

            btnBrowseOutputXex2.Click += (_, __) =>
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Select output XEX #2",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                    _txtOutputXex2.Text = sfd.FileName;
            };

            btnRunBatchSolo.Click += (_, __) => RunBatchSolo();

            _chkSplitTwoXex.CheckedChanged += (_, __) =>
            {
                _txtOutputXex2.Enabled = _chkSplitTwoXex.Checked;
                btnBrowseOutputXex2.Enabled = _chkSplitTwoXex.Checked;
            };
            _txtOutputXex2.Enabled = _chkSplitTwoXex.Checked;
            btnBrowseOutputXex2.Enabled = _chkSplitTwoXex.Checked;

            // Init
            RefreshLevelList();
        }

        private void RefreshLevelList()
        {
            _cbLevel.Items.Clear();

            var list = _rbSolo.Checked ? _soloOrder : _multiOrder;
            foreach (var name in list)
                _cbLevel.Items.Add(name);

            if (_cbLevel.Items.Count > 0)
                _cbLevel.SelectedIndex = 0;

            RefreshOffset();
        }

        private void RefreshOffset()
        {
            var level = _cbLevel.SelectedItem?.ToString() ?? string.Empty;
            string offset = string.Empty;

            if (_rbSolo.Checked)
                _soloOffsets.TryGetValue(level, out offset);
            else
                _multiOffsets.TryGetValue(level, out offset);

            _txtOffset.Text = offset ?? string.Empty;
        }

        private void RunSingleConversion()
        {
            _txtLog.Clear();

            var input = _txtInput.Text.Trim();
            var output = _txtOutput.Text.Trim();
            var offset = _txtOffset.Text.Trim();

            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                MessageBox.Show(this, "Please choose a valid input setup file.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                MessageBox.Show(this, "Please choose an output setup path.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(offset))
            {
                MessageBox.Show(this, "No offset for the selected level.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!TryGetSetupConvExe(out var exePath))
                return;

            var ok = RunSetupConv(exePath, input, output, offset, _txtLog, out var exitCode);

            if (ok && exitCode == 0)
                MessageBox.Show(this, "Conversion complete.", "Setup Patching", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, $"setupconv.exe failed (ExitCode {exitCode}). See log.", "Setup Patching", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void RunBatchSolo()
        {
            _txtLog.Clear();

            if (!_rbSolo.Checked)
            {
                MessageBox.Show(this, "Batch mode is Solo-only (for now).", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var inDir = _txtBatchInputDir.Text.Trim();
            var outDir = _txtBatchOutputDir.Text.Trim();

            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir))
            {
                MessageBox.Show(this, "Please choose a valid batch input folder.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(outDir))
            {
                MessageBox.Show(this, "Please choose a batch output folder.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Directory.CreateDirectory(outDir);

            if (!TryGetSetupConvExe(out var exePath))
                return;

            // Index all files (extensionless-safe)
            var allFiles = Directory.GetFiles(inDir);
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in allFiles)
            {
                var name = System.IO.Path.GetFileName(path);
                var baseName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!byName.ContainsKey(name)) byName[name] = path;
                if (!byName.ContainsKey(baseName)) byName[baseName] = path;
            }

            // Convert what we can (PASS 1: original VAs)
            var levelToInputSetup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinPathPass1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinSizePass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int okCount = 0, failCount = 0;

            foreach (var kvp in _soloBatchBaseToLevel)
            {
                var baseName = kvp.Key;
                var level = kvp.Value;

                if (!_soloOffsets.TryGetValue(level, out var originalVa) || string.IsNullOrWhiteSpace(originalVa))
                {
                    _txtLog.AppendText($"SKIP: No Solo VA for level {level}\r\n");
                    continue;
                }

                if (!TryResolveInputFile(byName, baseName, out var inPath))
                {
                    _txtLog.AppendText($"MISS: {baseName} (level {level})\r\n");
                    continue;
                }

                var outPath = System.IO.Path.Combine(outDir, baseName + ".bin");

                _txtLog.AppendText($"--- {level} (pass1/original VA) ---\r\n");
                _txtLog.AppendText($"IN : {inPath}\r\n");
                _txtLog.AppendText($"OUT: {outPath}\r\n");
                _txtLog.AppendText($"VA : {originalVa}\r\n");

                var ok = RunSetupConv(exePath, inPath, outPath, originalVa, _txtLog, out var exitCode);
                if (ok && exitCode == 0 && File.Exists(outPath))
                {
                    okCount++;
                    levelToInputSetup[level] = inPath;
                    levelToBinPathPass1[level] = outPath;
                    levelToBinSizePass1[level] = (int)new FileInfo(outPath).Length;
                }
                else
                {
                    failCount++;
                }

                _txtLog.AppendText("\r\n");
            }

            _txtLog.AppendText($"Batch done. Success={okCount}  Failed={failCount}\r\n");

            if (!_chkPatchXex.Checked)
            {
                MessageBox.Show(this, "Batch conversion complete.", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var inXex = _txtInputXex.Text.Trim();
            var outXex1 = _txtOutputXex.Text.Trim();
            var outXex2 = _txtOutputXex2.Text.Trim();

            if (string.IsNullOrWhiteSpace(inXex) || !File.Exists(inXex))
            {
                MessageBox.Show(this, "Please choose a valid input XEX.", "Patch XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(outXex1))
            {
                MessageBox.Show(this, "Please choose an output XEX #1 path.", "Patch XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_chkSplitTwoXex.Checked && string.IsNullOrWhiteSpace(outXex2))
            {
                MessageBox.Show(this, "Split is enabled. Please choose an output XEX #2 path.", "Patch XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Load pass1 blobs (we will repack only those that end up moved)
            var pass1Blobs = levelToBinPathPass1
                .Where(kv => File.Exists(kv.Value))
                .ToDictionary(kv => kv.Key, kv => File.ReadAllBytes(kv.Value), StringComparer.OrdinalIgnoreCase);

            try
            {
                byte[] xexBytes = File.ReadAllBytes(inXex);

                bool allowMp = _chkAllowMp.Checked;
                bool allowExtend = _chkAllowExtendXex.Checked;

                const int align = 0x10;
                const int extendChunk = 0x200000; // 2MB experimental chunk per extend

                // PLAN split or single
                if (_chkSplitTwoXex.Checked)
                {
                    _txtLog.AppendText("\r\n=== HYBRID PLAN (Split across two XEX) ===\r\n");

                    XexSetupPatcher.PlanSplitAcrossTwoXex(
                        xex: xexBytes,
                        levelToSize: levelToBinSizePass1,
                        allowMp: allowMp,
                        allowExtendXex: allowExtend,
                        extendChunkBytes: extendChunk,
                        align: align,
                        out var placements1,
                        out var rep1,
                        out var remaining,
                        out var placements2,
                        out var rep2);

                    // Repack needed levels for XEX1 + XEX2
                    var repackDir = System.IO.Path.Combine(outDir, "_repacked");
                    Directory.CreateDirectory(repackDir);

                    var blobs1 = BuildBlobsForPlacements(
                        placements1,
                        pass1Blobs,
                        levelToInputSetup,
                        exePath,
                        repackDir,
                        _txtLog);

                    var blobs2 = BuildBlobsForPlacements(
                        placements2,
                        pass1Blobs,
                        levelToInputSetup,
                        exePath,
                        repackDir,
                        _txtLog);

                    // Apply
                    XexSetupPatcher.ApplyHybrid(inXex, outXex1, placements1, blobs1, allowExtend, out var apply1);
                    XexSetupPatcher.ApplyHybrid(inXex, outXex2, placements2, blobs2, allowExtend, out var apply2);

                    // Show reports in scrollable dialog
                    var combined = new List<string>();
                    combined.AddRange(rep1);
                    combined.Add("");
                    combined.Add("=== APPLY #1 ===");
                    combined.AddRange(apply1);
                    combined.Add("");
                    combined.AddRange(rep2);
                    combined.Add("");
                    combined.Add("=== APPLY #2 ===");
                    combined.AddRange(apply2);

                    ReportDialog.ShowReport(this, "Hybrid Patch Report (Split Two XEX)", combined);

                    return;
                }
                else
                {
                    _txtLog.AppendText("\r\n=== HYBRID PLAN (Single XEX) ===\r\n");

                    var placements = XexSetupPatcher.PlanHybridPlacements(
                        xex: xexBytes,
                        levelToSize: levelToBinSizePass1,
                        candidateLevels: XexSetupPatcher.PriorityOrder,
                        allowMp: allowMp,
                        allowExtendXex: allowExtend,
                        extendChunkBytes: extendChunk,
                        align: align,
                        out var planReport,
                        out var notPlaced);

                    var repackDir = System.IO.Path.Combine(outDir, "_repacked");
                    Directory.CreateDirectory(repackDir);

                    var blobs = BuildBlobsForPlacements(
                        placements,
                        pass1Blobs,
                        levelToInputSetup,
                        exePath,
                        repackDir,
                        _txtLog);

                    XexSetupPatcher.ApplyHybrid(inXex, outXex1, placements, blobs, allowExtend, out var applyReport);

                    var all = new List<string>();
                    all.AddRange(planReport);
                    all.AddRange(applyReport);

                    if (notPlaced.Count > 0)
                    {
                        all.Add("NOTE: Some setups could not be placed.");
                        all.Add("Enable 'Split across TWO output XEX' to auto-create XEX #2 for the remainder.");
                    }

                    ReportDialog.ShowReport(this, "Hybrid Patch Report", all);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "XEX patch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Dictionary<string, byte[]> BuildBlobsForPlacements(
            IReadOnlyList<XexSetupPatcher.Placement> placements,
            IReadOnlyDictionary<string, byte[]> pass1Blobs,
            IReadOnlyDictionary<string, string> levelToInputSetup,
            string exePath,
            string repackDir,
            TextBox log)
        {
            var blobs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in placements)
            {
                if (!p.RequiresRepack)
                {
                    if (pass1Blobs.TryGetValue(p.LevelName, out var b))
                        blobs[p.LevelName] = b;
                    continue;
                }

                if (!levelToInputSetup.TryGetValue(p.LevelName, out var inputSet) || !File.Exists(inputSet))
                    throw new InvalidOperationException($"Missing original .set input for repack: {p.LevelName}");

                string outBin2 = System.IO.Path.Combine(repackDir, SanitizeFileName(p.LevelName) + $"_{p.NewVa:X8}.bin");
                string vaHex = p.NewVa.ToString("X8");

                log.AppendText($"REPACK: {p.LevelName,-12} VA={vaHex} -> {outBin2}\r\n");
                var ok = RunSetupConv(exePath, inputSet, outBin2, vaHex, log, out var exitCode);
                if (!ok || exitCode != 0 || !File.Exists(outBin2))
                    throw new InvalidOperationException($"setupconv repack failed for {p.LevelName} (ExitCode {exitCode}).");

                blobs[p.LevelName] = File.ReadAllBytes(outBin2);
            }

            // Also include any non-repacked levels that were placed but missing from blobs (safety)
            foreach (var p in placements)
            {
                if (!blobs.ContainsKey(p.LevelName) && pass1Blobs.TryGetValue(p.LevelName, out var b))
                    blobs[p.LevelName] = b;
            }

            return blobs;
        }

        private static string SanitizeFileName(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        private static bool TryResolveInputFile(Dictionary<string, string> byName, string baseName, out string path)
        {
            if (byName.TryGetValue(baseName, out path))
                return true;

            if (byName.TryGetValue(baseName + ".set", out path))
                return true;

            path = string.Empty;
            return false;
        }

        private static bool TryGetSetupConvExe(out string exePath)
        {
            exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setupconv.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"setupconv.exe not found in:\n{exePath}", "Setup Patching",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private static bool RunSetupConv(string exePath, string input, string output, string offsetOrVaHex, TextBox log, out int exitCode)
        {
            exitCode = -1;

            string args = $"\"{input}\" \"{output}\" {offsetOrVaHex}";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = new Process { StartInfo = psi };
                p.Start();

                string stdout = p.StandardOutput.ReadToEnd() ?? string.Empty;
                string stderr = p.StandardError.ReadToEnd() ?? string.Empty;

                p.WaitForExit();
                exitCode = p.ExitCode;

                if (!string.IsNullOrWhiteSpace(stdout))
                    log.AppendText(stdout + Environment.NewLine);

                if (!string.IsNullOrWhiteSpace(stderr))
                    log.AppendText("ERR: " + stderr + Environment.NewLine);

                return true;
            }
            catch (Exception ex)
            {
                log.AppendText("EXC: " + ex + Environment.NewLine);
                return false;
            }
        }

        // -------------------------
        // Scrollable report dialog
        // -------------------------
        private sealed class ReportDialog : Form
        {
            private readonly TextBox _box;

            private ReportDialog(string title, IEnumerable<string> lines)
            {
                Text = title;
                Width = 980;
                Height = 720;
                StartPosition = FormStartPosition.CenterParent;

                _box = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    Font = new System.Drawing.Font("Consolas", 9),
                    Text = string.Join(Environment.NewLine, lines ?? Array.Empty<string>())
                };

                var btn = new Button
                {
                    Text = "Close",
                    Dock = DockStyle.Bottom,
                    Height = 36
                };
                btn.Click += (_, __) => Close();

                Controls.Add(_box);
                Controls.Add(btn);
            }

            public static void ShowReport(IWin32Window owner, string title, IEnumerable<string> lines)
            {
                using var dlg = new ReportDialog(title, lines);
                dlg.ShowDialog(owner);
            }
        }
    }
}
