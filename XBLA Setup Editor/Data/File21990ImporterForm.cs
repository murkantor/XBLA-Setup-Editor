using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public sealed class File21990ImporterForm : Form
    {
        private readonly TextBox _txt21990Path;
        private readonly TextBox _txtInputXex;
        private readonly TextBox _txtOutputXex;
        private readonly TextBox _txtLog;
        private readonly Button _btnAnalyze;
        private readonly Button _btnApplyPatches;

        private readonly CheckBox _chkBackupXex;
        private readonly CheckBox _chkApplySkyData;
        private readonly CheckBox _chkSkyToFog;
        private readonly CheckBox _chkApplyN64FogDistances;
        private readonly CheckBox _chkApplyMusic;
        private readonly CheckBox _chkApplyMenuData; // NEW

        private readonly ListView _lvSkyEntries;
        private readonly ListView _lvMusicEntries;
        private readonly ListView _lvMenuEntries;

        private File21990Parser? _parser;
        private byte[]? _xexData;

        public File21990ImporterForm()
        {
            Text = "21990 File Importer (Debug Mode)";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            Width = DpiHelper.Scale(this, 1200);
            Height = DpiHelper.Scale(this, 1000);
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 16,
                Padding = new Padding(DpiHelper.Scale(this, 12))
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 90)));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 90)));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 90)));

            _txt21990Path = new TextBox { Dock = DockStyle.Fill };
            var btn21990Browse = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            _btnAnalyze = new Button { Text = "Analyze", Dock = DockStyle.Fill };

            _txtInputXex = new TextBox { Dock = DockStyle.Fill };
            var btnInputXexBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _txtOutputXex = new TextBox { Dock = DockStyle.Fill };
            var btnOutputXexBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            _chkBackupXex = new CheckBox { Text = "Create backup", Checked = true, AutoSize = true };
            _chkApplySkyData = new CheckBox { Text = "Apply Sky/Fog", Checked = true, AutoSize = true };
            _chkSkyToFog = new CheckBox { Text = "Sky→Fog Col", Checked = true, AutoSize = true };
            _chkApplyN64FogDistances = new CheckBox { Text = "N64 Fog Dist", Checked = true, AutoSize = true };
            _chkApplyMusic = new CheckBox { Text = "Apply Music", Checked = true, AutoSize = true };

            // NEW CHECKBOX
            _chkApplyMenuData = new CheckBox { Text = "Apply Folder/Icon Text", Checked = true, AutoSize = true };

            _btnApplyPatches = new Button
            {
                Text = "Apply Patches to XEX",
                Dock = DockStyle.Fill,
                Height = DpiHelper.Scale(this, 32),
                Enabled = false
            };

            _lvSkyEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvSkyEntries.Columns.Add("#", DpiHelper.Scale(this, 30));
            _lvSkyEntries.Columns.Add("Level", DpiHelper.Scale(this, 60));
            _lvSkyEntries.Columns.Add("Sky RGB", DpiHelper.Scale(this, 80));

            _lvMusicEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvMusicEntries.Columns.Add("#", DpiHelper.Scale(this, 30));
            _lvMusicEntries.Columns.Add("Level", DpiHelper.Scale(this, 60));
            _lvMusicEntries.Columns.Add("Main", DpiHelper.Scale(this, 60));

            _lvMenuEntries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvMenuEntries.Columns.Add("Level ID", DpiHelper.Scale(this, 60));
            _lvMenuEntries.Columns.Add("Name", DpiHelper.Scale(this, 80));
            _lvMenuEntries.Columns.Add("Folder Text", DpiHelper.Scale(this, 80));
            _lvMenuEntries.Columns.Add("Icon Text", DpiHelper.Scale(this, 80));

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)),
                WordWrap = false
            };

            int row = 0;
            // Row 0
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            mainLayout.Controls.Add(new Label { Text = "21990 File:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txt21990Path, 1, row);
            mainLayout.Controls.Add(btn21990Browse, 2, row);
            mainLayout.Controls.Add(_btnAnalyze, 3, row);
            row++;

            // Row 1
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            mainLayout.Controls.Add(new Label { Text = "Input XEX:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txtInputXex, 1, row);
            mainLayout.SetColumnSpan(_txtInputXex, 1);
            mainLayout.Controls.Add(btnInputXexBrowse, 2, row);
            row++;

            // Row 2
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            mainLayout.Controls.Add(new Label { Text = "Output XEX:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txtOutputXex, 1, row);
            mainLayout.Controls.Add(btnOutputXexBrowse, 2, row);
            row++;

            // Row 3: Options
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 32)));
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            optionsPanel.Controls.Add(_chkBackupXex);
            optionsPanel.Controls.Add(_chkApplyMenuData); // Added here
            optionsPanel.Controls.Add(_chkApplySkyData);
            optionsPanel.Controls.Add(_chkSkyToFog);
            optionsPanel.Controls.Add(_chkApplyN64FogDistances);
            optionsPanel.Controls.Add(_chkApplyMusic);

            mainLayout.Controls.Add(optionsPanel, 0, row);
            mainLayout.SetColumnSpan(optionsPanel, 3);
            mainLayout.Controls.Add(_btnApplyPatches, 3, row);
            row++;

            // Row 4: Menu Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 22)));
            var lblMenu = new Label { Text = "Scanned Menu Entries (Folder/Icon):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblMenu, 0, row);
            mainLayout.SetColumnSpan(lblMenu, 4);
            row++;

            // Row 5: Menu List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            mainLayout.Controls.Add(_lvMenuEntries, 0, row);
            mainLayout.SetColumnSpan(_lvMenuEntries, 4);
            row++;

            // Row 6: Sky Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 22)));
            var lblSky = new Label { Text = "Sky Entries:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblSky, 0, row);
            mainLayout.SetColumnSpan(lblSky, 4);
            row++;

            // Row 7: Sky List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            mainLayout.Controls.Add(_lvSkyEntries, 0, row);
            mainLayout.SetColumnSpan(_lvSkyEntries, 4);
            row++;

            // Row 8: Music Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 22)));
            var lblMusic = new Label { Text = "Music Entries:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblMusic, 0, row);
            mainLayout.SetColumnSpan(lblMusic, 4);
            row++;

            // Row 9: Music List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            mainLayout.Controls.Add(_lvMusicEntries, 0, row);
            mainLayout.SetColumnSpan(_lvMusicEntries, 4);
            row++;

            // Row 10: Log Label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 22)));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, row);
            mainLayout.SetColumnSpan(lblLog, 4);
            row++;

            // Row 11: Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            mainLayout.Controls.Add(_txtLog, 0, row);
            mainLayout.SetColumnSpan(_txtLog, 4);

            Controls.Add(mainLayout);

            // --- Events ---
            btn21990Browse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog { Title = "Select 21990 file", Filter = "21990 files (*.bin;*.21990)|*.bin;*.21990|All files (*.*)|*.*" };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txt21990Path.Text = ofd.FileName;
                    _parser = null;
                    _btnApplyPatches.Enabled = false;
                    ClearAnalysis();
                    Analyze21990File();
                }
            };

            btnInputXexBrowse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog { Title = "Select input XEX", Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*" };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtInputXex.Text = ofd.FileName;
                    _xexData = null;
                    if (string.IsNullOrWhiteSpace(_txtOutputXex.Text))
                    {
                        var dir = Path.GetDirectoryName(ofd.FileName) ?? "";
                        var name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        var ext = Path.GetExtension(ofd.FileName);
                        _txtOutputXex.Text = Path.Combine(dir, $"{name}_patched{ext}");
                    }
                }
            };

            btnOutputXexBrowse.Click += (_, __) =>
            {
                using var sfd = new SaveFileDialog { Title = "Select output XEX", Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*" };
                if (sfd.ShowDialog(this) == DialogResult.OK) _txtOutputXex.Text = sfd.FileName;
            };

            _btnAnalyze.Click += (_, __) => Analyze21990File();
            _btnApplyPatches.Click += (_, __) => ApplyPatches();
        }

        private void ClearAnalysis()
        {
            _lvSkyEntries.Items.Clear();
            _lvMusicEntries.Items.Clear();
            _lvMenuEntries.Items.Clear();
            _txtLog.Clear();
        }

        private void Log(string message) => _txtLog.AppendText(message + Environment.NewLine);

        private void Analyze21990File()
        {
            ClearAnalysis();
            var path = _txt21990Path.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "Please select a valid 21990 file.", "Analyze", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Log($"Loading 21990 file: {path}");
                _parser = File21990Parser.Load(path);

                Log($"File size: {_parser.RawData.Length:N0} bytes");
                Log("");

                // Populate Menu Entries (NEW)
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

                _btnApplyPatches.Enabled = true;
                Log("Analysis complete.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "Analysis Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyPatches()
        {
            if (_parser == null) return;

            var inputXex = _txtInputXex.Text.Trim();
            var outputXex = _txtOutputXex.Text.Trim();

            if (string.IsNullOrWhiteSpace(inputXex) || !File.Exists(inputXex)) { MessageBox.Show(this, "Please select a valid input XEX file.", "Apply Patches", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (string.IsNullOrWhiteSpace(outputXex)) { MessageBox.Show(this, "Please select an output XEX path.", "Apply Patches", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            try
            {
                Log("");
                Log("=== Applying Patches ===");
                _xexData = File.ReadAllBytes(inputXex);

                if (_chkBackupXex.Checked && File.Exists(outputXex))
                {
                    File.Copy(outputXex, outputXex + ".backup", overwrite: true);
                }

                var patchedXex = new byte[_xexData.Length];
                Array.Copy(_xexData, patchedXex, _xexData.Length);
                int totalPatched = 0;

                // NEW: Apply Menu Data via Signature Scan
                if (_chkApplyMenuData.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplyMenuDataToXex(patchedXex, log);
                    foreach (var l in log) Log(l);
                }

                if (_chkApplySkyData.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplySkyData(patchedXex, log, _chkSkyToFog.Checked, _chkApplyN64FogDistances.Checked);
                    foreach (var l in log) Log(l);
                }

                if (_chkApplyMusic.Checked)
                {
                    var log = new List<string>();
                    totalPatched += _parser.ApplyMusicData(patchedXex, log);
                    foreach (var l in log) Log(l);
                }

                File.WriteAllBytes(outputXex, patchedXex);
                Log($"=== Patching Complete (Total: {totalPatched}) ===");
                MessageBox.Show(this, "Patches applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "Patching Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}