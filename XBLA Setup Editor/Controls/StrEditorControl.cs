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
    public sealed class StrEditorControl : UserControl
    {
        private readonly DataGridView _grid;
        private Button _btnOpen = null!;
        private Button _btnSave = null!;
        private Button _btnSaveAs = null!;
        private Button _btnAddEntry = null!;
        private Button _btnRemoveLast = null!;
        private readonly Label _lblStatus;

        private StrFile? _file;
        private string? _path;
        private BindingList<StrEntry>? _binding;

        public StrEditorControl()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };

            // Row 0: Toolbar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var toolbar = CreateToolbar();
            mainLayout.Controls.Add(toolbar, 0, 0);

            // Row 1: Grid
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

            // Row 2: Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Text = "No file loaded"
            };
            mainLayout.Controls.Add(_lblStatus, 0, 2);

            Controls.Add(mainLayout);

            // Setup tooltips
            SetupTooltips();
        }

        private FlowLayoutPanel CreateToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnOpen = new Button { Text = "Open", Width = 70 };
            _btnOpen.Click += BtnOpen_Click;
            toolbar.Controls.Add(_btnOpen);

            _btnSave = new Button { Text = "Save", Width = 70 };
            _btnSave.Click += BtnSave_Click;
            toolbar.Controls.Add(_btnSave);

            _btnSaveAs = new Button { Text = "Save As", Width = 70 };
            _btnSaveAs.Click += BtnSaveAs_Click;
            toolbar.Controls.Add(_btnSaveAs);

            toolbar.Controls.Add(new Label { Text = "  ", AutoSize = true });

            _btnAddEntry = new Button { Text = "+", Width = 32 };
            _btnAddEntry.Click += BtnAddEntry_Click;
            toolbar.Controls.Add(_btnAddEntry);

            _btnRemoveLast = new Button { Text = "-", Width = 32 };
            _btnRemoveLast.Click += BtnRemoveLast_Click;
            toolbar.Controls.Add(_btnRemoveLast);

            return toolbar;
        }

        private void SetupGrid()
        {
            _grid.AutoGenerateColumns = false;
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Bank",
                DataPropertyName = nameof(StrEntry.Bank),
                Width = 60
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = nameof(StrEntry.Id),
                Width = 60
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Text",
                DataPropertyName = nameof(StrEntry.Text),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Display hex nicely for Bank/ID
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

            // Allow typing "0A" / "0x0A" / "10"
            _grid.CellParsing += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var prop = _grid.Columns[e.ColumnIndex].DataPropertyName;
                if (prop != nameof(StrEntry.Bank) && prop != nameof(StrEntry.Id))
                    return;

                if (e.Value is string str)
                {
                    str = str.Trim();
                    if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        str = str[2..];

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

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_btnOpen, TooltipTexts.StrEditor.Open);
            toolTip.SetToolTip(_btnSave, TooltipTexts.StrEditor.Save);
            toolTip.SetToolTip(_btnSaveAs, TooltipTexts.StrEditor.SaveAs);
            toolTip.SetToolTip(_btnAddEntry, TooltipTexts.StrEditor.AddEntry);
            toolTip.SetToolTip(_btnRemoveLast, TooltipTexts.StrEditor.RemoveEntry);
        }

        private void BindToGrid(StrFile file)
        {
            _file = file;
            _binding = new BindingList<StrEntry>(_file.Entries);
            _grid.DataSource = _binding;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var name = string.IsNullOrWhiteSpace(_path) ? "(no file)" : Path.GetFileName(_path);
            var count = _file?.Entries.Count ?? 0;
            _lblStatus.Text = $"{name} - {count} entries";
        }

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
                _grid.EndEdit();
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
                _grid.EndEdit();
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

        private void BtnAddEntry_Click(object? sender, EventArgs e)
        {
            if (_file == null || _binding == null)
            {
                MessageBox.Show(FindForm(), "Open a STR file first.", "Add Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Default bank/id: increment last entry
            byte bank = 0x00;
            byte id = 0x00;

            if (_binding.Count > 0)
            {
                var last = _binding[_binding.Count - 1];
                bank = last.Bank;
                id = (byte)(last.Id + 1);
                if (id == 0x00) bank = (byte)(bank + 1);
            }

            var newEntry = new StrEntry { Bank = bank, Id = id, Text = "" };
            _binding.Add(newEntry);

            int rowIndex = _binding.Count - 1;
            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[2];
            _grid.Rows[rowIndex].Selected = true;
            _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            _grid.BeginEdit(true);

            UpdateStatus();
        }

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
