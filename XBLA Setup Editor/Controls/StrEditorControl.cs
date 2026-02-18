// =============================================================================
// StrEditorControl.cs - STR/ADB String Database Editor
// =============================================================================
// This control provides a graphical editor for STR/ADB string database files.
// Unlike other tabs, this control handles its own file I/O and does NOT
// participate in the shared XEX state system.
//
// FEATURES:
// =========
// - Open/Save STR and ADB files
// - Grid-based editing of string entries
// - Bank and ID displayed in hexadecimal
// - Add/remove entries with auto-incrementing Bank:ID
//
// STR FILE USAGE:
// ===============
// STR files contain all localized text for GoldenEye XBLA including:
// - Menu text and UI labels
// - Weapon names
// - Level names and descriptions
// - Objective text
// - HUD messages
//
// EDITING WORKFLOW:
// =================
// 1. Click "Open" to load a .str or .adb file
// 2. Edit Bank, ID, or Text values directly in the grid
// 3. Use "+" to add new entries (auto-increments from last entry)
// 4. Use "-" to remove the last entry
// 5. Click "Save" or "Save As" to write changes
//
// BANK:ID SYSTEM:
// ===============
// Each string is identified by a Bank:ID pair (both 0-255).
// Banks group related strings together:
// - Bank 0x00: Menu text
// - Bank 0x01: Weapon names
// - Bank 0x02: Level text
// - etc.
//
// The grid accepts hex input (e.g., "0A" or "0x0A") or decimal.
// =============================================================================

using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// UserControl for editing STR/ADB string database files.
    /// This control has its own file handling and does not use the shared XEX state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The STR editor provides a simple grid-based interface for modifying
    /// game text. It's intentionally standalone to allow editing string files
    /// independently from XEX modification workflows.
    /// </para>
    /// <para>
    /// The control uses data binding to keep the grid synchronized with the
    /// underlying StrFile data model. Changes are immediately reflected but
    /// not saved until the user explicitly saves.
    /// </para>
    /// </remarks>
    public sealed class StrEditorControl : UserControl
    {
        // =====================================================================
        // UI CONTROLS
        // =====================================================================

        /// <summary>Data grid displaying Bank, ID, and Text columns.</summary>
        private readonly DataGridView _grid;

        /// <summary>Open file button.</summary>
        private Button _btnOpen = null!;

        /// <summary>Save to current file button.</summary>
        private Button _btnSave = null!;

        /// <summary>Save to new file button.</summary>
        private Button _btnSaveAs = null!;

        /// <summary>Add entry button (+).</summary>
        private Button _btnAddEntry = null!;

        /// <summary>Remove last entry button (-).</summary>
        private Button _btnRemoveLast = null!;

        /// <summary>Status label showing file name and entry count.</summary>
        private readonly Label _lblStatus;

        // =====================================================================
        // STATE
        // =====================================================================

        /// <summary>Currently loaded STR file, null if no file loaded.</summary>
        private StrFile? _file;

        /// <summary>Path to the current file, null if unsaved.</summary>
        private string? _path;

        /// <summary>Binding list for data grid (wraps _file.Entries).</summary>
        private BindingList<StrEntry>? _binding;

        // =====================================================================
        // CONSTRUCTOR
        // =====================================================================

        /// <summary>
        /// Creates a new STR editor control with toolbar, grid, and status bar.
        /// </summary>
        public StrEditorControl()
        {
            // Main layout: toolbar at top, grid in middle, status at bottom
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(DpiHelper.Scale(this, 8))
            };

            // Row 0: Toolbar with Open, Save, Save As, +, - buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            var toolbar = CreateToolbar();
            mainLayout.Controls.Add(toolbar, 0, 0);

            // Row 1: Data grid for entries (takes remaining space)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window
            };
            SetupGrid();
            mainLayout.Controls.Add(_grid, 0, 1);

            // Row 2: Status bar showing file name and entry count
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 24)));
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Text = "No file loaded"
            };
            mainLayout.Controls.Add(_lblStatus, 0, 2);

            Controls.Add(mainLayout);

            SetupTooltips();
        }

        // =====================================================================
        // UI SETUP
        // =====================================================================

        /// <summary>
        /// Creates the toolbar with file and editing buttons.
        /// </summary>
        /// <returns>FlowLayoutPanel containing toolbar buttons.</returns>
        private FlowLayoutPanel CreateToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            // File operations
            _btnOpen = new Button { Text = "Open", Width = DpiHelper.Scale(this, 70), Height = DpiHelper.Scale(this, 28) };
            _btnOpen.Click += BtnOpen_Click;
            toolbar.Controls.Add(_btnOpen);

            _btnSave = new Button { Text = "Save", Width = DpiHelper.Scale(this, 70), Height = DpiHelper.Scale(this, 28) };
            _btnSave.Click += BtnSave_Click;
            toolbar.Controls.Add(_btnSave);

            _btnSaveAs = new Button { Text = "Save As", Width = DpiHelper.Scale(this, 70), Height = DpiHelper.Scale(this, 28) };
            _btnSaveAs.Click += BtnSaveAs_Click;
            toolbar.Controls.Add(_btnSaveAs);

            // Spacer
            toolbar.Controls.Add(new Label { Text = "  ", AutoSize = true });

            // Entry management
            _btnAddEntry = new Button { Text = "+", Width = DpiHelper.Scale(this, 32), Height = DpiHelper.Scale(this, 28) };
            _btnAddEntry.Click += BtnAddEntry_Click;
            toolbar.Controls.Add(_btnAddEntry);

            _btnRemoveLast = new Button { Text = "-", Width = DpiHelper.Scale(this, 32), Height = DpiHelper.Scale(this, 28) };
            _btnRemoveLast.Click += BtnRemoveLast_Click;
            toolbar.Controls.Add(_btnRemoveLast);

            return toolbar;
        }

        /// <summary>
        /// Configures the data grid columns and cell formatting/parsing.
        /// </summary>
        /// <remarks>
        /// The grid has three columns:
        /// - Bank: Hex-formatted byte (0-255)
        /// - ID: Hex-formatted byte (0-255)
        /// - Text: The actual string content (auto-sized)
        ///
        /// Cell formatting displays Bank/ID as hex (e.g., "0A").
        /// Cell parsing accepts hex with or without "0x" prefix.
        /// </remarks>
        private void SetupGrid()
        {
            _grid.AutoGenerateColumns = false;
            _grid.Columns.Clear();

            // Bank column
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Bank",
                DataPropertyName = nameof(StrEntry.Bank),
                Width = DpiHelper.Scale(this, 60)
            });

            // ID column
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = nameof(StrEntry.Id),
                Width = DpiHelper.Scale(this, 60)
            });

            // Text column (fills remaining space)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Text",
                DataPropertyName = nameof(StrEntry.Text),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Format Bank/ID as hexadecimal for display
            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var prop = _grid.Columns[e.ColumnIndex].DataPropertyName;

                if (prop == nameof(StrEntry.Bank) && e.Value is byte b1)
                {
                    e.Value = b1.ToString("X2");
                    e.FormattingApplied = true;
                }
                else if (prop == nameof(StrEntry.Id) && e.Value is byte b2)
                {
                    e.Value = b2.ToString("X2");
                    e.FormattingApplied = true;
                }
            };

            // Parse hex input (accepts "0A", "0x0A", or "10")
            _grid.CellParsing += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var prop = _grid.Columns[e.ColumnIndex].DataPropertyName;
                if (prop != nameof(StrEntry.Bank) && prop != nameof(StrEntry.Id))
                    return;

                if (e.Value is string str)
                {
                    str = str.Trim();

                    // Remove "0x" prefix if present
                    if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        str = str[2..];

                    // Try hex first, then decimal
                    if (byte.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out var hex))
                    {
                        e.Value = hex;
                        e.ParsingApplied = true;
                        return;
                    }

                    if (byte.TryParse(str, out var dec))
                    {
                        e.Value = dec;
                        e.ParsingApplied = true;
                    }
                }
            };
        }

        /// <summary>
        /// Configures tooltips for toolbar buttons.
        /// </summary>
        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_btnOpen, TooltipTexts.StrEditor.Open);
            toolTip.SetToolTip(_btnSave, TooltipTexts.StrEditor.Save);
            toolTip.SetToolTip(_btnSaveAs, TooltipTexts.StrEditor.SaveAs);
            toolTip.SetToolTip(_btnAddEntry, TooltipTexts.StrEditor.AddEntry);
            toolTip.SetToolTip(_btnRemoveLast, TooltipTexts.StrEditor.RemoveEntry);
        }

        // =====================================================================
        // DATA BINDING
        // =====================================================================

        /// <summary>
        /// Binds a loaded STR file to the data grid.
        /// </summary>
        /// <param name="file">The loaded STR file.</param>
        private void BindToGrid(StrFile file)
        {
            _file = file;
            _binding = new BindingList<StrEntry>(_file.Entries);
            _grid.DataSource = _binding;
            UpdateStatus();
        }

        /// <summary>
        /// Updates the status bar with current file name and entry count.
        /// </summary>
        private void UpdateStatus()
        {
            var name = string.IsNullOrWhiteSpace(_path) ? "(no file)" : Path.GetFileName(_path);
            var count = _file?.Entries.Count ?? 0;
            _lblStatus.Text = $"{name} - {count} entries";
        }

        // =====================================================================
        // BUTTON EVENT HANDLERS
        // =====================================================================

        /// <summary>
        /// Opens a file dialog to load an STR/ADB file.
        /// </summary>
        private void BtnOpen_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "STR/ADB files (*.str;*.adb)|*.str;*.adb|All files (*.*)|*.*",
                Title = "Open STR file"
            };

            if (ofd.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            try
            {
                _path = ofd.FileName;
                var file = StrFile.Load(_path);
                BindToGrid(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Open failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves changes to the current file.
        /// If no file is loaded, delegates to Save As.
        /// </summary>
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_file == null)
            {
                MessageBox.Show(FindForm(), "No STR file loaded.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_path))
            {
                BtnSaveAs_Click(sender, e);
                return;
            }

            try
            {
                _grid.EndEdit();  // Commit any pending cell edits
                var bytes = _file.SaveToBytes();
                File.WriteAllBytes(_path, bytes);

                MessageBox.Show(FindForm(), "Saved.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Save failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Opens a save dialog to save the file with a new name.
        /// </summary>
        private void BtnSaveAs_Click(object? sender, EventArgs e)
        {
            if (_file == null)
            {
                MessageBox.Show(FindForm(), "No STR file loaded.", "Save As",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "STR/ADB files (*.str;*.adb)|*.str;*.adb|All files (*.*)|*.*",
                Title = "Save STR file as..."
            };

            if (sfd.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            try
            {
                _grid.EndEdit();  // Commit any pending cell edits
                var bytes = _file.SaveToBytes();
                File.WriteAllBytes(sfd.FileName, bytes);

                _path = sfd.FileName;
                MessageBox.Show(FindForm(), "Saved.", "Save As",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Save As failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Adds a new entry with auto-incremented Bank:ID.
        /// </summary>
        /// <remarks>
        /// The new entry's ID is set to last entry's ID + 1.
        /// If ID wraps to 0, Bank is also incremented.
        /// Focus moves to the new entry's Text cell for immediate editing.
        /// </remarks>
        private void BtnAddEntry_Click(object? sender, EventArgs e)
        {
            if (_file == null || _binding == null)
            {
                MessageBox.Show(FindForm(), "Open a STR file first.", "Add Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Auto-increment from last entry
            byte bank = 0x00;
            byte id = 0x00;

            if (_binding.Count > 0)
            {
                var last = _binding[_binding.Count - 1];
                bank = last.Bank;
                id = (byte)(last.Id + 1);
                if (id == 0x00) bank = (byte)(bank + 1);  // Wrapped, increment bank
            }

            // Add new entry and select it
            var newEntry = new StrEntry { Bank = bank, Id = id, Text = "" };
            _binding.Add(newEntry);

            int rowIndex = _binding.Count - 1;
            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[2];  // Text column
            _grid.Rows[rowIndex].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            _grid.BeginEdit(true);  // Start editing immediately

            UpdateStatus();
        }

        /// <summary>
        /// Removes the last entry after confirmation.
        /// </summary>
        private void BtnRemoveLast_Click(object? sender, EventArgs e)
        {
            if (_file == null || _binding == null)
            {
                MessageBox.Show(FindForm(), "Open a STR file first.", "Remove Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_binding.Count == 0)
            {
                MessageBox.Show(FindForm(), "There are no entries to remove.", "Remove Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show confirmation with entry details
            var last = _binding[_binding.Count - 1];
            var confirm = MessageBox.Show(
                FindForm(),
                $"Remove last entry?\n\nBank={last.Bank:X2}  ID={last.Id:X2}\nText=\"{last.Text}\"",
                "Confirm remove",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            _grid.EndEdit();

            // Remove and select previous entry
            int newIndex = _binding.Count - 2;
            _binding.RemoveAt(_binding.Count - 1);

            if (_binding.Count > 0 && newIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[newIndex].Cells[2];
                _grid.Rows[newIndex].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, newIndex);
            }

            UpdateStatus();
        }
    }
}
