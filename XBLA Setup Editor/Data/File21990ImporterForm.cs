using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public sealed class File21990ImporterForm : Form
    {
        // --- UI Controls ---
        private readonly TextBox _txt21990Path;
        private readonly TextBox _txtInputXex;
        private readonly TextBox _txtOutputXex;
        private readonly TextBox _txtLog;
        private readonly Button _btnAnalyze;
        private readonly Button _btnApplyPatches;
        private readonly CheckBox _chkBackupXex;
        private readonly CheckBox _chkApplySkyData;
        private readonly CheckBox _chkCloudToFog;
        private readonly CheckBox _chkApplyFogRatios;
        private readonly ListView _lvSkyEntries;
        private readonly ListView _lvPatchRecords;

        // --- State ---
        private File21990Parser? _parser;
        private byte[]? _xexData;

        public File21990ImporterForm()
        {
            Text = "21990 File Importer";
            Width = 1100;
            Height = 900;
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 12,
                Padding = new Padding(12)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            // Row 0: 21990 file path
            _txt21990Path = new TextBox { Dock = DockStyle.Fill };
            var btn21990Browse = new Button { Text = "Browse...", Dock = DockStyle.Fill };
            _btnAnalyze = new Button { Text = "Analyze", Dock = DockStyle.Fill };

            // Row 1: Input XEX
            _txtInputXex = new TextBox { Dock = DockStyle.Fill };
            var btnInputXexBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            // Row 2: Output XEX
            _txtOutputXex = new TextBox { Dock = DockStyle.Fill };
            var btnOutputXexBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill };

            // Options and Apply button
            _chkBackupXex = new CheckBox
            {
                Text = "Create backup before patching",
                Checked = true,
                AutoSize = true
            };

            _chkApplySkyData = new CheckBox
            {
                Text = "Apply Sky/Fog/Cloud data (XEX 0x84B860)",
                Checked = true,
                AutoSize = true
            };

            _chkCloudToFog = new CheckBox
            {
                Text = "Cloud colour → Fog colour",
                Checked = true,
                AutoSize = true
            };

            _chkApplyFogRatios = new CheckBox
            {
                Text = "Apply XBLA fog ratios",
                Checked = true,
                AutoSize = true
            };

            _btnApplyPatches = new Button
            {
                Text = "Apply Patches to XEX",
                Dock = DockStyle.Fill,
                Height = 32,
                Enabled = false
            };

            // Sky entries list
            _lvSkyEntries = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lvSkyEntries.Columns.Add("#", 30);
            _lvSkyEntries.Columns.Add("Level ID", 60);
            _lvSkyEntries.Columns.Add("Sky RGB", 85);
            _lvSkyEntries.Columns.Add("Cloud RGB", 85);
            _lvSkyEntries.Columns.Add("Water RGB", 85);
            _lvSkyEntries.Columns.Add("Far Fog", 60);
            _lvSkyEntries.Columns.Add("Near Fog", 60);
            _lvSkyEntries.Columns.Add("Cloud Ht", 60);
            _lvSkyEntries.Columns.Add("Water Ht", 60);

            // Patch records list
            _lvPatchRecords = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lvPatchRecords.Columns.Add("Offset", 80);
            _lvPatchRecords.Columns.Add("Type", 50);
            _lvPatchRecords.Columns.Add("SubType", 60);

            // Log
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9),
                WordWrap = false
            };

            // Layout rows
            int row = 0;

            // Row 0: 21990 file
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "21990 File:", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txt21990Path, 1, row);
            mainLayout.Controls.Add(btn21990Browse, 2, row);
            mainLayout.Controls.Add(_btnAnalyze, 3, row);
            row++;

            // Row 1: Input XEX
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "Input XEX:", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txtInputXex, 1, row);
            mainLayout.SetColumnSpan(_txtInputXex, 1);
            mainLayout.Controls.Add(btnInputXexBrowse, 2, row);
            row++;

            // Row 2: Output XEX
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "Output XEX:", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            mainLayout.Controls.Add(_txtOutputXex, 1, row);
            mainLayout.Controls.Add(btnOutputXexBrowse, 2, row);
            row++;

            // Row 3: Options
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            optionsPanel.Controls.Add(_chkBackupXex);
            optionsPanel.Controls.Add(_chkApplySkyData);
            optionsPanel.Controls.Add(_chkCloudToFog);
            optionsPanel.Controls.Add(_chkApplyFogRatios);
            mainLayout.Controls.Add(optionsPanel, 0, row);
            mainLayout.SetColumnSpan(optionsPanel, 3);
            mainLayout.Controls.Add(_btnApplyPatches, 3, row);
            row++;

            // Row 4: Sky entries label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblSkies = new Label { Text = "Sky Entries (from 21990 file):", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblSkies, 0, row);
            mainLayout.SetColumnSpan(lblSkies, 4);
            row++;

            // Row 5: Sky entries list
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
            mainLayout.Controls.Add(_lvSkyEntries, 0, row);
            mainLayout.SetColumnSpan(_lvSkyEntries, 4);
            row++;

            // Row 6: Patch records label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblPatches = new Label { Text = "Patch Records:", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblPatches, 0, row);
            mainLayout.SetColumnSpan(lblPatches, 4);
            row++;

            // Row 7: Patch records list
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainLayout.Controls.Add(_lvPatchRecords, 0, row);
            mainLayout.SetColumnSpan(_lvPatchRecords, 4);
            row++;

            // Row 8: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, row);
            mainLayout.SetColumnSpan(lblLog, 4);
            row++;

            // Row 9: Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.Controls.Add(_txtLog, 0, row);
            mainLayout.SetColumnSpan(_txtLog, 4);

            Controls.Add(mainLayout);

            // --- Events ---
            btn21990Browse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select 21990 file",
                    Filter = "21990 files (*.bin;*.21990)|*.bin;*.21990|All files (*.*)|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txt21990Path.Text = ofd.FileName;
                    _parser = null;
                    _btnApplyPatches.Enabled = false;
                    ClearAnalysis();
                    // Auto-analyze on file selection
                    Analyze21990File();
                }
            };

            btnInputXexBrowse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select input XEX",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
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
                using var sfd = new SaveFileDialog
                {
                    Title = "Select output XEX",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                    _txtOutputXex.Text = sfd.FileName;
            };

            _btnAnalyze.Click += (_, __) => Analyze21990File();
            _btnApplyPatches.Click += (_, __) => ApplyPatches();
        }

        private void ClearAnalysis()
        {
            _lvSkyEntries.Items.Clear();
            _lvPatchRecords.Items.Clear();
            _txtLog.Clear();
        }

        private void Log(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        private void Analyze21990File()
        {
            ClearAnalysis();

            var path = _txt21990Path.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "Please select a valid 21990 file.", "Analyze",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Log($"Loading 21990 file: {path}");
                _parser = File21990Parser.Load(path);

                Log($"File size: {_parser.RawData.Length:N0} bytes (0x{_parser.RawData.Length:X})");
                Log($"Sky entries found: {_parser.SkyEntries.Count}");
                Log($"Patch records found: {_parser.PatchRecords.Count}");
                Log($"Address pairs found: {_parser.AddressTable.Count}");
                Log("");

                // Populate sky entries list
                _lvSkyEntries.BeginUpdate();
                foreach (var sky in _parser.SkyEntries)
                {
                    var item = new ListViewItem(sky.Index.ToString());
                    item.SubItems.Add($"0x{sky.LevelId:X4}");
                    item.SubItems.Add($"{sky.SkyColourRed},{sky.SkyColourGreen},{sky.SkyColourBlue}");
                    item.SubItems.Add($"{sky.CloudsRed:F0},{sky.CloudsGreen:F0},{sky.CloudsBlue:F0}");
                    item.SubItems.Add($"{sky.WaterRed:F0},{sky.WaterGreen:F0},{sky.WaterBlue:F0}");
                    item.SubItems.Add($"{sky.FarFog:F0}");
                    item.SubItems.Add($"{sky.NearFog:F0}");
                    item.SubItems.Add($"{sky.CloudHeight:F0}");
                    item.SubItems.Add($"{sky.WaterHeight:F0}");
                    item.Tag = sky;
                    _lvSkyEntries.Items.Add(item);
                }
                _lvSkyEntries.EndUpdate();

                Log($"Populated {_lvSkyEntries.Items.Count} sky entries in list.");

                // Populate patch records list
                _lvPatchRecords.BeginUpdate();
                int patchLimit = Math.Min(100, _parser.PatchRecords.Count);
                for (int i = 0; i < patchLimit; i++)
                {
                    var p = _parser.PatchRecords[i];
                    var item = new ListViewItem($"0x{p.FileOffset:X6}");
                    item.SubItems.Add($"0x{p.Type:X2}");
                    item.SubItems.Add($"0x{p.SubType:X2}");
                    item.Tag = p;
                    _lvPatchRecords.Items.Add(item);
                }
                if (_parser.PatchRecords.Count > 100)
                {
                    var item = new ListViewItem($"... and {_parser.PatchRecords.Count - 100} more");
                    _lvPatchRecords.Items.Add(item);
                }
                _lvPatchRecords.EndUpdate();

                _btnApplyPatches.Enabled = true;
                Log("");
                Log("Analysis complete. Click 'Apply Patches to XEX' to patch.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Log(ex.StackTrace ?? "");
                MessageBox.Show(this, ex.ToString(), "Analysis Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyPatches()
        {
            if (_parser == null)
            {
                MessageBox.Show(this, "Please analyze a 21990 file first.", "Apply Patches",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var inputXex = _txtInputXex.Text.Trim();
            var outputXex = _txtOutputXex.Text.Trim();

            if (string.IsNullOrWhiteSpace(inputXex) || !File.Exists(inputXex))
            {
                MessageBox.Show(this, "Please select a valid input XEX file.", "Apply Patches",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputXex))
            {
                MessageBox.Show(this, "Please select an output XEX path.", "Apply Patches",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Log("");
                Log("=== Applying Patches ===");

                // Load XEX
                Log($"Loading XEX: {inputXex}");
                _xexData = File.ReadAllBytes(inputXex);
                Log($"XEX size: {_xexData.Length:N0} bytes (0x{_xexData.Length:X})");

                // Create backup if requested
                if (_chkBackupXex.Checked && File.Exists(outputXex))
                {
                    var backupPath = outputXex + ".backup";
                    Log($"Creating backup: {backupPath}");
                    File.Copy(outputXex, backupPath, overwrite: true);
                }

                // Make a copy of XEX data to patch
                var patchedXex = new byte[_xexData.Length];
                Array.Copy(_xexData, patchedXex, _xexData.Length);

                int totalPatched = 0;

                // Apply sky data if checked
                if (_chkApplySkyData.Checked)
                {
                    Log("");
                    var skyLog = new List<string>();
                    int skyPatched = _parser.ApplySkyData(patchedXex, skyLog, _chkCloudToFog.Checked, _chkApplyFogRatios.Checked);
                    foreach (var line in skyLog)
                        Log(line);
                    totalPatched += skyPatched;
                }

                // Save patched XEX
                Log("");
                Log($"Saving patched XEX: {outputXex}");
                File.WriteAllBytes(outputXex, patchedXex);

                Log("");
                Log($"=== Patching Complete ===");
                Log($"Total items patched: {totalPatched}");

                MessageBox.Show(this, $"Patches applied successfully!\n\nTotal items patched: {totalPatched}",
                    "Apply Patches", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Log(ex.StackTrace ?? "");
                MessageBox.Show(this, ex.ToString(), "Patching Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
