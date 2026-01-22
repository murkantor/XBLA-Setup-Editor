using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Form for extending XEX files with additional data
    /// </summary>
    public class XexExtenderForm : Form
    {
        // Controls
        private readonly TextBox _txtInputXex;
        private readonly TextBox _txtDataFile;
        private readonly TextBox _txtOutputXex;
        private readonly TextBox _txtLog;
        private readonly Button _btnAnalyze;
        private readonly Button _btnExtend;
        private readonly CheckBox _chkRecalcSha1;
        private readonly CheckBox _chkBackup;
        private readonly Label _lblAnalysis;
        private readonly PropertyGrid _propGrid;

        // State
        private byte[] _xexData;
        private byte[] _dataToAppend;
        private XexExtender.XexAnalysis _analysis;

        public XexExtenderForm()
        {
            // Form setup
            Text = "XEX Extender";
            Size = new Size(900, 700);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;

            // Create controls
            _txtInputXex = new TextBox { ReadOnly = true };
            _txtDataFile = new TextBox { ReadOnly = true };
            _txtOutputXex = new TextBox();

            _txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                WordWrap = false
            };

            _btnAnalyze = new Button { Text = "Analyze", Width = 100 };
            _btnExtend = new Button { Text = "Extend XEX", Width = 100, Enabled = false };

            _chkRecalcSha1 = new CheckBox
            {
                Text = "Recalculate SHA1 hash",
                Checked = false,
                AutoSize = true
            };

            _chkBackup = new CheckBox
            {
                Text = "Create backup",
                Checked = true,
                AutoSize = true
            };

            _lblAnalysis = new Label
            {
                AutoSize = false,
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9),
                Padding = new Padding(5)
            };

            // Layout
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 8,
                ColumnCount = 4
            };

            // Column styles
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            int row = 0;

            // Row 0: Input XEX
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "Input XEX:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            _txtInputXex.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_txtInputXex, 1, row);
            mainLayout.SetColumnSpan(_txtInputXex, 2);
            var btnBrowseXex = new Button { Text = "Browse...", Width = 80 };
            btnBrowseXex.Click += BtnBrowseXex_Click;
            mainLayout.Controls.Add(btnBrowseXex, 3, row);
            row++;

            // Row 1: Data file to append
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "Data File:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            _txtDataFile.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_txtDataFile, 1, row);
            mainLayout.SetColumnSpan(_txtDataFile, 2);
            var btnBrowseData = new Button { Text = "Browse...", Width = 80 };
            btnBrowseData.Click += BtnBrowseData_Click;
            mainLayout.Controls.Add(btnBrowseData, 3, row);
            row++;

            // Row 2: Output XEX
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mainLayout.Controls.Add(new Label { Text = "Output XEX:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            _txtOutputXex.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_txtOutputXex, 1, row);
            mainLayout.SetColumnSpan(_txtOutputXex, 2);
            var btnBrowseOutput = new Button { Text = "Browse...", Width = 80 };
            btnBrowseOutput.Click += BtnBrowseOutput_Click;
            mainLayout.Controls.Add(btnBrowseOutput, 3, row);
            row++;

            // Row 3: Options and buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            optionsPanel.Controls.Add(_chkBackup);
            optionsPanel.Controls.Add(_chkRecalcSha1);
            optionsPanel.Controls.Add(_btnAnalyze);
            optionsPanel.Controls.Add(_btnExtend);
            mainLayout.Controls.Add(optionsPanel, 0, row);
            mainLayout.SetColumnSpan(optionsPanel, 4);
            row++;

            // Row 4: Analysis label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            mainLayout.Controls.Add(new Label { Text = "Analysis:", Anchor = AnchorStyles.Top | AnchorStyles.Left, AutoSize = true }, 0, row);
            _lblAnalysis.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_lblAnalysis, 1, row);
            mainLayout.SetColumnSpan(_lblAnalysis, 3);
            row++;

            // Row 5: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            mainLayout.Controls.Add(new Label { Text = "Log:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            row++;

            // Row 6: Log text box (fill remaining space)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _txtLog.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_txtLog, 0, row);
            mainLayout.SetColumnSpan(_txtLog, 4);
            row++;

            // Event handlers
            _btnAnalyze.Click += BtnAnalyze_Click;
            _btnExtend.Click += BtnExtend_Click;

            Controls.Add(mainLayout);

            Log("XEX Extender ready.");
            Log("This tool allows extending XEX files with additional read-only data.");
            Log("");
            Log("Usage:");
            Log("1. Select the XEX file to extend");
            Log("2. Select the data file to append");
            Log("3. Click 'Analyze' to verify the XEX structure");
            Log("4. Click 'Extend XEX' to create the extended file");
            Log("");
            Log("Note: The appended data will be mapped at the memory address shown in the analysis.");
            Log("Your code must be patched to reference this address to use the new data.");
        }

        private void Log(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        private void BtnBrowseXex_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select XEX File";
                ofd.Filter = "XEX Files (*.xex)|*.xex|All Files (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _txtInputXex.Text = ofd.FileName;
                    _txtOutputXex.Text = Path.Combine(
                        Path.GetDirectoryName(ofd.FileName),
                        Path.GetFileNameWithoutExtension(ofd.FileName) + "_extended.xex");

                    // Auto-load and analyze
                    try
                    {
                        _xexData = File.ReadAllBytes(ofd.FileName);
                        Log($"Loaded XEX: {ofd.FileName} ({_xexData.Length:N0} bytes)");
                        PerformAnalysis();
                    }
                    catch (Exception ex)
                    {
                        Log($"Error loading XEX: {ex.Message}");
                        _xexData = null;
                        _analysis = null;
                    }

                    UpdateButtonStates();
                }
            }
        }

        private void BtnBrowseData_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Data File to Append";
                ofd.Filter = "All Files (*.*)|*.*|Binary Files (*.bin)|*.bin";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _txtDataFile.Text = ofd.FileName;

                    try
                    {
                        _dataToAppend = File.ReadAllBytes(ofd.FileName);
                        Log($"Loaded data file: {ofd.FileName} ({_dataToAppend.Length:N0} bytes)");

                        // Validate extension
                        if (_xexData != null)
                        {
                            var (isValid, message) = XexExtender.ValidateExtension(_xexData, _dataToAppend.Length);
                            Log($"Validation: {message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error loading data file: {ex.Message}");
                        _dataToAppend = null;
                    }

                    UpdateButtonStates();
                }
            }
        }

        private void BtnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Extended XEX As";
                sfd.Filter = "XEX Files (*.xex)|*.xex|All Files (*.*)|*.*";
                sfd.FileName = Path.GetFileName(_txtOutputXex.Text);

                if (!string.IsNullOrEmpty(_txtInputXex.Text))
                {
                    sfd.InitialDirectory = Path.GetDirectoryName(_txtInputXex.Text);
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _txtOutputXex.Text = sfd.FileName;
                }
            }
        }

        private void BtnAnalyze_Click(object sender, EventArgs e)
        {
            if (_xexData == null)
            {
                Log("No XEX file loaded.");
                return;
            }

            PerformAnalysis();
        }

        private void PerformAnalysis()
        {
            _analysis = XexExtender.Analyze(_xexData);

            if (_analysis.IsValid)
            {
                _lblAnalysis.Text = $"File Size: {_analysis.FileSize:N0} bytes ({_analysis.FileSize / 1024.0 / 1024.0:F2} MB)\r\n" +
                                   $"Image Size: 0x{_analysis.ImageSize:X} ({_analysis.ImageSize / 1024.0 / 1024.0:F2} MB)\r\n" +
                                   $"Compression: {(_analysis.CompressionType == 1 ? "Basic" : $"Type {_analysis.CompressionType}")}\r\n" +
                                   $"Blocks: {_analysis.Blocks.Count}\r\n" +
                                   $"Current End Address: 0x{_analysis.EndMemoryAddress:X8}\r\n" +
                                   $"New Data Would Start At: 0x{_analysis.EndMemoryAddress:X8}\r\n" +
                                   $"SHA1: {BitConverter.ToString(_analysis.CurrentSha1).Replace("-", "").Substring(0, 16)}...";

                Log("");
                Log("=== XEX Analysis ===");
                Log($"Blocks:");
                foreach (var block in _analysis.Blocks)
                {
                    Log($"  [{block.Index}] File: 0x{block.FileOffset:X}, Data: 0x{block.DataSize:X}, Zero: 0x{block.ZeroSize:X}, Mem: 0x{block.MemoryAddress:X8}");
                }
                Log($"Total data: {_analysis.TotalDataSize:N0} bytes");
                Log($"Total zero padding: {_analysis.TotalZeroSize:N0} bytes");
                Log($"Append address: 0x{_analysis.EndMemoryAddress:X8}");
            }
            else
            {
                _lblAnalysis.Text = $"Analysis failed:\r\n{_analysis.Error}";
                Log($"Analysis failed: {_analysis.Error}");
            }

            UpdateButtonStates();
        }

        private void BtnExtend_Click(object sender, EventArgs e)
        {
            if (_xexData == null || _dataToAppend == null || _analysis == null || !_analysis.IsValid)
            {
                Log("Cannot extend: Missing data or invalid XEX.");
                return;
            }

            string outputPath = _txtOutputXex.Text;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Log("No output path specified.");
                return;
            }

            // Confirm
            var confirm = MessageBox.Show(
                $"Extend XEX with {_dataToAppend.Length:N0} bytes?\n\n" +
                $"New data will be at memory address: 0x{_analysis.EndMemoryAddress:X8}\n" +
                $"Output file: {outputPath}",
                "Confirm Extension",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                Log("");
                Log("=== Extending XEX ===");

                var (modifiedXex, result) = XexExtender.Extend(_xexData, _dataToAppend, _chkRecalcSha1.Checked);

                foreach (var line in result.Log)
                {
                    Log(line);
                }

                if (result.Success)
                {
                    // Create backup if needed
                    if (_chkBackup.Checked && File.Exists(outputPath))
                    {
                        string backupPath = outputPath + ".backup";
                        File.Copy(outputPath, backupPath, true);
                        Log($"Backup created: {backupPath}");
                    }

                    // Write output
                    File.WriteAllBytes(outputPath, modifiedXex);
                    Log($"Written: {outputPath}");
                    Log("");
                    Log("=== SUCCESS ===");
                    Log($"New data is at memory address: 0x{result.NewDataMemoryAddress:X8}");
                    Log($"You can reference this address in your code patches.");

                    MessageBox.Show(
                        $"XEX extended successfully!\n\n" +
                        $"New data at: 0x{result.NewDataMemoryAddress:X8}\n" +
                        $"File size: {modifiedXex.Length:N0} bytes",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Log($"Extension failed: {result.Error}");
                    MessageBox.Show($"Extension failed:\n{result.Error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButtonStates()
        {
            _btnAnalyze.Enabled = _xexData != null;
            _btnExtend.Enabled = _xexData != null && _dataToAppend != null && _analysis != null && _analysis.IsValid;
        }
    }
}
