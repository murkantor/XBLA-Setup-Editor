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
        private readonly CheckBox _chkForceRepack;

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

        public SetupPatchingForm()
        {
            Text = "Setup Patching (setupconv.exe)";
            Width = 980;
            Height = 800;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 22, Padding = new Padding(12), AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

            _rbSolo = new RadioButton { Text = "Solo", Checked = true, AutoSize = true, Dock = DockStyle.Left };
            _rbMulti = new RadioButton { Text = "Multi", AutoSize = true, Dock = DockStyle.Left };
            var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            modePanel.Controls.Add(_rbSolo); modePanel.Controls.Add(_rbMulti);

            _cbLevel = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _txtOffset = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtInput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtOutput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };

            var btnBrowseInput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseOutput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunSingle = new Button { Text = "Run setupconv.exe (single)", Dock = DockStyle.Fill, Height = 34 };

            // Batch UI
            _txtBatchInputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtBatchOutputDir = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseBatchIn = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseBatchOut = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnRunBatchSolo = new Button { Text = "Run batch (Solo)", Dock = DockStyle.Fill, Height = 34 };

            // XEX Patch UI
            _chkPatchXex = new CheckBox { Text = "Patch XEX after batch (Solo)", AutoSize = true, Dock = DockStyle.Left };
            _txtInputXex = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            _txtOutputXex = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseInputXex = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            var btnBrowseOutputXex = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _chkAllowMp = new CheckBox { Text = "Use MP pool overflow (destructive)", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkAllowExtendXex = new CheckBox { Text = "Extend XEX if needed (adds data beyond file, updates headers)", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _chkForceRepack = new CheckBox { Text = "Force Repack (Defrag memory - Optimizes space)", AutoSize = true, Dock = DockStyle.Left, Checked = true };
            _chkSplitTwoXex = new CheckBox { Text = "If not enough room: split across TWO output XEX files", AutoSize = true, Dock = DockStyle.Left, Checked = false };
            _txtOutputXex2 = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            var btnBrowseOutputXex2 = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new System.Drawing.Font("Consolas", 9) };

            void AddRow(int row, string label, Control mid, Control? right = null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
                layout.Controls.Add(mid, 1, row);
                if (right != null) layout.Controls.Add(right, 2, row);
            }

            int r = 0;
            AddRow(r++, "Mode", modePanel);
            AddRow(r++, "Level", _cbLevel);
            AddRow(r++, "Memory Offset", _txtOffset);
            AddRow(r++, "Input Setup", _txtInput, btnBrowseInput);
            AddRow(r++, "Output Setup", _txtOutput, btnBrowseOutput);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.Controls.Add(btnRunSingle, 0, r++); layout.SetColumnSpan(btnRunSingle, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8)); layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, r++); layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 3);

            AddRow(r++, "Batch Input Dir", _txtBatchInputDir, btnBrowseBatchIn);
            AddRow(r++, "Batch Output Dir", _txtBatchOutputDir, btnBrowseBatchOut);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.Controls.Add(btnRunBatchSolo, 0, r++); layout.SetColumnSpan(btnRunBatchSolo, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.Controls.Add(_chkPatchXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.Controls.Add(_chkAllowMp, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.Controls.Add(_chkAllowExtendXex, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.Controls.Add(_chkForceRepack, 1, r++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); layout.Controls.Add(_chkSplitTwoXex, 1, r++);
            AddRow(r++, "Input XEX", _txtInputXex, btnBrowseInputXex);
            AddRow(r++, "Output XEX #1", _txtOutputXex, btnBrowseOutputXex);
            AddRow(r++, "Output XEX #2", _txtOutputXex2, btnBrowseOutputXex2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.Controls.Add(_txtLog, 0, r++); layout.SetColumnSpan(_txtLog, 3);

            Controls.Add(layout);

            // --- EVENTS ---
            _rbSolo.CheckedChanged += (_, __) => RefreshLevelList();
            _rbMulti.CheckedChanged += (_, __) => RefreshLevelList();
            _cbLevel.SelectedIndexChanged += (_, __) => RefreshOffset();

            btnBrowseInput.Click += (_, __) => { using var ofd = new OpenFileDialog { Filter = "Setup files|*.set;*.bin;*.*" }; if (ofd.ShowDialog(this) == DialogResult.OK) _txtInput.Text = ofd.FileName; };
            btnBrowseOutput.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "BIN (*.bin)|*.bin" }; if (sfd.ShowDialog(this) == DialogResult.OK) _txtOutput.Text = sfd.FileName; };
            btnRunSingle.Click += (_, __) => RunSingleConversion();

            // --- SMART BATCH FOLDER SELECTION ---
            btnBrowseBatchIn.Click += (_, __) =>
            {
                using var fbd = new FolderBrowserDialog { Description = "Select batch input folder (Solo)" };
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtBatchInputDir.Text = fbd.SelectedPath;
                    string xblaDir = Path.Combine(fbd.SelectedPath, "XBLA");
                    _txtBatchOutputDir.Text = xblaDir;
                }
            };

            btnBrowseBatchOut.Click += (_, __) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog(this) == DialogResult.OK) _txtBatchOutputDir.Text = fbd.SelectedPath; };

            // --- SMART XEX SELECTION ---
            btnBrowseInputXex.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog { Filter = "XEX files (*.xex)|*.xex" };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtInputXex.Text = ofd.FileName;

                    string dir = Path.GetDirectoryName(ofd.FileName) ?? "";
                    string baseName = Path.GetFileNameWithoutExtension(ofd.FileName);
                    string modName = "Mod";

                    // Attempt to extract mod name from batch dir
                    if (!string.IsNullOrWhiteSpace(_txtBatchInputDir.Text))
                        modName = new DirectoryInfo(_txtBatchInputDir.Text).Name;

                    _txtOutputXex.Text = Path.Combine(dir, $"{baseName}_{modName}.xex");
                    _txtOutputXex2.Text = Path.Combine(dir, $"{baseName}_{modName}2.xex");

                    if (_chkSplitTwoXex.Checked)
                        _txtOutputXex.Text = Path.Combine(dir, $"{baseName}_{modName}1.xex");
                }
            };

            btnBrowseOutputXex.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "XEX files (*.xex)|*.xex" }; if (sfd.ShowDialog(this) == DialogResult.OK) _txtOutputXex.Text = sfd.FileName; };
            btnBrowseOutputXex2.Click += (_, __) => { using var sfd = new SaveFileDialog { Filter = "XEX files (*.xex)|*.xex" }; if (sfd.ShowDialog(this) == DialogResult.OK) _txtOutputXex2.Text = sfd.FileName; };

            btnRunBatchSolo.Click += (_, __) => RunBatchSolo();

            _chkSplitTwoXex.CheckedChanged += (_, __) =>
            {
                bool split = _chkSplitTwoXex.Checked;
                _txtOutputXex2.Enabled = split;
                btnBrowseOutputXex2.Enabled = split;

                if (!string.IsNullOrWhiteSpace(_txtInputXex.Text))
                {
                    string dir = Path.GetDirectoryName(_txtInputXex.Text) ?? "";
                    string baseName = Path.GetFileNameWithoutExtension(_txtInputXex.Text);
                    string modName = !string.IsNullOrWhiteSpace(_txtBatchInputDir.Text) ? new DirectoryInfo(_txtBatchInputDir.Text).Name : "Mod";

                    if (split)
                    {
                        _txtOutputXex.Text = Path.Combine(dir, $"{baseName}_{modName}1.xex");
                        _txtOutputXex2.Text = Path.Combine(dir, $"{baseName}_{modName}2.xex");
                    }
                    else
                    {
                        _txtOutputXex.Text = Path.Combine(dir, $"{baseName}_{modName}.xex");
                    }
                }
            };

            _txtOutputXex2.Enabled = _chkSplitTwoXex.Checked;
            btnBrowseOutputXex2.Enabled = _chkSplitTwoXex.Checked;

            RefreshLevelList();
        }

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
            var input = _txtInput.Text.Trim(); var output = _txtOutput.Text.Trim(); var offset = _txtOffset.Text.Trim();
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { MessageBox.Show("Invalid input."); return; }
            if (string.IsNullOrWhiteSpace(output)) { MessageBox.Show("Invalid output."); return; }
            if (string.IsNullOrWhiteSpace(offset)) { MessageBox.Show("No offset."); return; }
            if (!TryGetSetupConvExe(out var exePath)) return;
            var ok = RunSetupConv(exePath, input, output, offset, _txtLog, out var exitCode);
            MessageBox.Show(ok && exitCode == 0 ? "Conversion complete." : "Failed.");
        }

        private void RunBatchSolo()
        {
            _txtLog.Clear();
            if (!_rbSolo.Checked) { MessageBox.Show("Batch mode is Solo-only."); return; }
            var inDir = _txtBatchInputDir.Text.Trim(); var outDir = _txtBatchOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir)) { MessageBox.Show("Invalid batch input."); return; }
            if (string.IsNullOrWhiteSpace(outDir)) { MessageBox.Show("Invalid batch output."); return; }
            Directory.CreateDirectory(outDir);
            if (!TryGetSetupConvExe(out var exePath)) return;

            var allFiles = Directory.GetFiles(inDir);
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in allFiles)
            {
                var name = Path.GetFileName(path); var baseName = Path.GetFileNameWithoutExtension(path);
                if (!byName.ContainsKey(name)) byName[name] = path;
                if (!byName.ContainsKey(baseName)) byName[baseName] = path;
            }

            var levelToInputSetup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinPathPass1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var levelToBinSizePass1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int okCount = 0, failCount = 0;

            foreach (var kvp in _soloBatchBaseToLevel)
            {
                var baseName = kvp.Key; var level = kvp.Value;
                if (!_soloOffsets.TryGetValue(level, out var originalVa)) { _txtLog.AppendText($"SKIP: No VA for {level}\r\n"); continue; }
                if (!TryResolveInputFile(byName, baseName, out var inPath)) { _txtLog.AppendText($"MISS: {baseName}\r\n"); continue; }
                var outPath = Path.Combine(outDir, baseName + ".bin");
                _txtLog.AppendText($"--- {level} ---\r\nIN : {inPath}\r\nOUT: {outPath}\r\nVA : {originalVa}\r\n");

                var ok = RunSetupConv(exePath, inPath, outPath, originalVa, _txtLog, out var exitCode);
                if (ok && exitCode == 0 && File.Exists(outPath))
                {
                    okCount++; levelToInputSetup[level] = inPath;
                    levelToBinPathPass1[level] = outPath; levelToBinSizePass1[level] = (int)new FileInfo(outPath).Length;
                }
                else failCount++;
                _txtLog.AppendText("\r\n");
            }
            _txtLog.AppendText($"Batch done. Success={okCount}  Failed={failCount}\r\n");

            if (!_chkPatchXex.Checked) { MessageBox.Show("Batch complete."); return; }

            var inXex = _txtInputXex.Text.Trim(); var outXex1 = _txtOutputXex.Text.Trim(); var outXex2 = _txtOutputXex2.Text.Trim();
            if (string.IsNullOrWhiteSpace(inXex) || !File.Exists(inXex)) { MessageBox.Show("Invalid Input XEX."); return; }
            if (string.IsNullOrWhiteSpace(outXex1)) { MessageBox.Show("Invalid Output XEX."); return; }
            if (_chkSplitTwoXex.Checked && string.IsNullOrWhiteSpace(outXex2)) { MessageBox.Show("Invalid Output XEX #2."); return; }

            var pass1Blobs = levelToBinPathPass1.Where(kv => File.Exists(kv.Value)).ToDictionary(kv => kv.Key, kv => File.ReadAllBytes(kv.Value), StringComparer.OrdinalIgnoreCase);

            try
            {
                byte[] xexBytes = File.ReadAllBytes(inXex);
                bool allowMp = _chkAllowMp.Checked;
                bool allowExtend = _chkAllowExtendXex.Checked;
                bool forceRepack = _chkForceRepack.Checked;
                const int align = 0x10; const int extendChunk = 0x200000;

                if (_chkSplitTwoXex.Checked)
                {
                    _txtLog.AppendText("\r\n=== SPLIT PLAN ===\r\n");
                    XexSetupPatcher.PlanSplitAcrossTwoXex(xexBytes, levelToBinSizePass1, allowMp, allowExtend, extendChunk, align, forceRepack, out var p1, out var r1, out var rem, out var p2, out var r2);

                    var repackDir = Path.Combine(outDir, "_repacked"); Directory.CreateDirectory(repackDir);
                    var b1 = BuildBlobsForPlacements(p1, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);
                    var b2 = BuildBlobsForPlacements(p2, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);

                    XexSetupPatcher.ApplyHybrid(inXex, outXex1, p1, b1, allowExtend, out var a1);
                    XexSetupPatcher.ApplyHybrid(inXex, outXex2, p2, b2, allowExtend, out var a2);

                    var combined = new List<string>(); combined.AddRange(r1); combined.Add(""); combined.Add("=== APPLY #1 ==="); combined.AddRange(a1);
                    combined.Add(""); combined.AddRange(r2); combined.Add(""); combined.Add("=== APPLY #2 ==="); combined.AddRange(a2);
                    ReportDialog.ShowReport(this, "Split Report", combined);
                }
                else
                {
                    _txtLog.AppendText("\r\n=== SINGLE PLAN ===\r\n");
                    var p = XexSetupPatcher.PlanHybridPlacements(xexBytes, levelToBinSizePass1, XexSetupPatcher.PriorityOrder, allowMp, allowExtend, extendChunk, align, forceRepack, out var r, out var not);

                    var repackDir = Path.Combine(outDir, "_repacked"); Directory.CreateDirectory(repackDir);
                    var b = BuildBlobsForPlacements(p, pass1Blobs, levelToInputSetup, exePath, repackDir, _txtLog);

                    XexSetupPatcher.ApplyHybrid(inXex, outXex1, p, b, allowExtend, out var a);

                    var all = new List<string>(); all.AddRange(r); all.AddRange(a);
                    if (not.Count > 0) { all.Add("NOTE: Some levels fit not."); }
                    ReportDialog.ShowReport(this, "Single Report", all);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

        private static string SanitizeFileName(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }
        private static bool TryResolveInputFile(Dictionary<string, string> byName, string baseName, out string path)
        {
            if (byName.TryGetValue(baseName, out path!)) return true;
            if (byName.TryGetValue(baseName + ".set", out path!)) return true;
            path = ""; return false;
        }
        private static bool TryGetSetupConvExe(out string exePath)
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setupconv.exe");
            if (!File.Exists(exePath)) { MessageBox.Show("setupconv.exe missing."); return false; }
            return true;
        }
        private static bool RunSetupConv(string exe, string inp, string outp, string off, TextBox log, out int code)
        {
            code = -1;
            try
            {
                using var p = new Process { StartInfo = new ProcessStartInfo { FileName = exe, Arguments = $"\"{inp}\" \"{outp}\" {off}", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
                p.Start();
                log.AppendText(p.StandardOutput.ReadToEnd() ?? "");
                log.AppendText(p.StandardError.ReadToEnd() ?? "");
                p.WaitForExit(); code = p.ExitCode; return true;
            }
            catch (Exception ex) { log.AppendText("EXC: " + ex); return false; }
        }

        private sealed class ReportDialog : Form
        {
            public ReportDialog(string t, IEnumerable<string> l)
            {
                Text = t; Width = 800; Height = 600;
                var b = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, Font = new System.Drawing.Font("Consolas", 9), Text = string.Join(Environment.NewLine, l) };
                Controls.Add(b);
            }
            public static void ShowReport(IWin32Window o, string t, IEnumerable<string> l) => new ReportDialog(t, l).ShowDialog(o);
        }
    }
}