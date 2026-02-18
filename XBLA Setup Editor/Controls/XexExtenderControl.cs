// =============================================================================
// XexExtenderControl.cs - XEX Extension Tab (EXPERIMENTAL)
// =============================================================================
// UserControl that provides the "XEX Extender" tab for attempting to extend
// XEX files with additional data blocks. This feature is EXPERIMENTAL.
//
// WARNING: THIS FEATURE DOES NOT WORK RELIABLY
// =============================================
// Due to Xenia's page table validation constraints, XEX extension is severely
// limited. The maximum extension for GoldenEye XBLA is typically only ~32KB,
// which is not enough for most practical use cases.
//
// TAB FUNCTIONALITY:
// ==================
// 1. Auto-analyzes loaded XEX when it's loaded via MainForm
// 2. Displays XEX structure information (blocks, addresses, headroom)
// 3. Allows selection of data file to append
// 4. Validates extension against available headroom
// 5. Performs extension if constraints are met
//
// DISPLAYED INFORMATION:
// ======================
// - File size and image size
// - Compression type (only basic compression is supported)
// - Number of data blocks
// - Current end memory address
// - Where new data would be mapped
// - SHA1 hash (truncated for display)
//
// INTEGRATION WITH MAINFORM:
// ==========================
// Unlike the standalone XexExtenderForm, this control integrates with the
// editor's shared state management:
// - Receives XEX data via OnXexLoaded() callback
// - Modifications trigger XexModified event
// - Changes are saved when user clicks "Save XEX" in main toolbar
//
// LOG OUTPUT:
// ===========
// On startup, displays prominent warning that feature is experimental.
// Logs all analysis results and operation progress for debugging.
// =============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// EXPERIMENTAL: Tab control for extending XEX files with additional data.
    /// Limited by Xenia compatibility constraints to ~32KB for GoldenEye XBLA.
    /// </summary>
    public sealed class XexExtenderControl : UserControl, IXexTab
    {
        // Controls
        private readonly TextBox _txtDataFile;
        private readonly TextBox _txtLog;
        private readonly Button _btnAnalyze;
        private readonly Button _btnExtend;
        private readonly CheckBox _chkRecalcSha1;
        private readonly Label _lblAnalysis;

        // State
        private byte[]? _xexData;
        private string? _xexPath;
        private byte[]? _dataToAppend;
        private XexExtender.XexAnalysis? _analysis;
        private bool _hasUnsavedChanges = false;

        // Events
        public event EventHandler<XexModifiedEventArgs>? XexModified;

        public XexExtenderControl()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(DpiHelper.Scale(this, 8)),
                RowCount = 6,
                ColumnCount = 4
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 100)));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 80)));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 100)));

            int row = 0;

            // Row 0: Data file to append
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 30)));
            mainLayout.Controls.Add(new Label { Text = "Data File:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            _txtDataFile = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            mainLayout.Controls.Add(_txtDataFile, 1, row);
            mainLayout.SetColumnSpan(_txtDataFile, 2);
            var btnBrowseData = new Button { Text = "Browse...", Width = DpiHelper.Scale(this, 80), Height = DpiHelper.Scale(this, 25) };
            btnBrowseData.Click += BtnBrowseData_Click;
            mainLayout.Controls.Add(btnBrowseData, 3, row);
            row++;

            // Row 1: Options and buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 35)));
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            _chkRecalcSha1 = new CheckBox { Text = "Recalculate SHA1 hash", Checked = false, AutoSize = true };
            optionsPanel.Controls.Add(_chkRecalcSha1);
            _btnAnalyze = new Button { Text = "Analyze", Width = DpiHelper.Scale(this, 100), Height = DpiHelper.Scale(this, 28), Enabled = false };
            _btnAnalyze.Click += BtnAnalyze_Click;
            optionsPanel.Controls.Add(_btnAnalyze);
            _btnExtend = new Button { Text = "Extend XEX", Width = DpiHelper.Scale(this, 100), Height = DpiHelper.Scale(this, 28), Enabled = false };
            _btnExtend.Click += BtnExtend_Click;
            optionsPanel.Controls.Add(_btnExtend);
            mainLayout.Controls.Add(optionsPanel, 0, row);
            mainLayout.SetColumnSpan(optionsPanel, 4);
            row++;

            // Row 2: Analysis label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 130)));
            mainLayout.Controls.Add(new Label { Text = "Analysis:", Anchor = AnchorStyles.Top | AnchorStyles.Left, AutoSize = true }, 0, row);
            _lblAnalysis = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)),
                Padding = new Padding(DpiHelper.Scale(this, 5))
            };
            mainLayout.Controls.Add(_lblAnalysis, 1, row);
            mainLayout.SetColumnSpan(_lblAnalysis, 3);
            row++;

            // Row 3: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 20)));
            mainLayout.Controls.Add(new Label { Text = "Log:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
            row++;

            // Row 4: Log text box
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)),
                ReadOnly = true,
                WordWrap = false
            };
            mainLayout.Controls.Add(_txtLog, 0, row);
            mainLayout.SetColumnSpan(_txtLog, 4);

            Controls.Add(mainLayout);

            SetupTooltips();

            Log("========================================");
            Log("WARNING: XEX EXTENDER IS EXPERIMENTAL");
            Log("========================================");
            Log("");
            Log("This tool DOES NOT WORK correctly and");
            Log("WILL BREAK YOUR XEX FILE.");
            Log("");
            Log("Do not use this unless you know what");
            Log("you are doing and have backups.");
            Log("========================================");
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_txtDataFile, TooltipTexts.XexExtender.DataFile);
            toolTip.SetToolTip(_chkRecalcSha1, TooltipTexts.XexExtender.RecalcSha1);
            toolTip.SetToolTip(_btnAnalyze, TooltipTexts.XexExtender.Analyze);
            toolTip.SetToolTip(_btnExtend, TooltipTexts.XexExtender.Extend);
        }

        private void Log(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        #region IXexTab Implementation

        public string TabDisplayName => "XEX Extender";

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void OnXexLoaded(byte[] xexData, string path)
        {
            _xexData = xexData;
            _xexPath = path;

            Log("");
            Log($"XEX loaded: {path} ({xexData.Length:N0} bytes)");

            _btnAnalyze.Enabled = true;
            PerformAnalysis();
            UpdateButtonStates();
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
            _analysis = null;
            _lblAnalysis.Text = "";
            _btnAnalyze.Enabled = false;
            _btnExtend.Enabled = false;
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

        private void BtnBrowseData_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Data File to Append",
                Filter = "All Files (*.*)|*.*|Binary Files (*.bin)|*.bin"
            };

            if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _txtDataFile.Text = ofd.FileName;

                try
                {
                    _dataToAppend = File.ReadAllBytes(ofd.FileName);
                    Log($"Loaded data file: {ofd.FileName} ({_dataToAppend.Length:N0} bytes)");

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

        private void BtnAnalyze_Click(object? sender, EventArgs e)
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
            if (_xexData == null)
            {
                Log("No XEX data loaded.");
                return;
            }

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

        private void BtnExtend_Click(object? sender, EventArgs e)
        {
            if (_xexData == null || _dataToAppend == null || _analysis == null || !_analysis.IsValid)
            {
                Log("Cannot extend: Missing data or invalid XEX.");
                return;
            }

            var confirm = MessageBox.Show(
                FindForm(),
                $"Extend XEX with {_dataToAppend.Length:N0} bytes?\n\n" +
                $"New data will be at memory address: 0x{_analysis.EndMemoryAddress:X8}\n" +
                $"The changes will be saved when you click Save XEX.",
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

                if (result.Success && modifiedXex != null)
                {
                    _xexData = modifiedXex;
                    _hasUnsavedChanges = true;

                    Log("");
                    Log("=== SUCCESS ===");
                    Log($"New data is at memory address: 0x{result.NewDataMemoryAddress:X8}");
                    Log($"Remember to save the XEX from the main toolbar.");

                    // Notify main form
                    XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                    // Re-analyze
                    PerformAnalysis();

                    MessageBox.Show(
                        FindForm(),
                        $"XEX extended successfully!\n\n" +
                        $"New data at: 0x{result.NewDataMemoryAddress:X8}\n" +
                        $"File size: {modifiedXex.Length:N0} bytes\n\n" +
                        $"Remember to save the XEX from the main toolbar.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Log($"Extension failed: {result.Error}");
                    MessageBox.Show(FindForm(), $"Extension failed:\n{result.Error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                MessageBox.Show(FindForm(), $"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButtonStates()
        {
            _btnAnalyze.Enabled = _xexData != null;
            _btnExtend.Enabled = _xexData != null && _dataToAppend != null && _analysis != null && _analysis.IsValid;
        }
    }
}
