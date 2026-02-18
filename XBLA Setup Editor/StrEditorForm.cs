using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public partial class StrEditorForm : Form
    {
        private readonly DataGridView grid;
        private readonly Button btnOpen;
        private readonly Button btnSave;
        private readonly Button btnSaveAs;
        private readonly Button btnAddEntry;
        private readonly Button btnRemoveLast;

        private readonly Panel topPanel;

        private StrFile? _file;
        private string? _path;
        private BindingList<StrEntry>? _binding;

        public StrEditorForm()
        {
            // no designer controls required
            InitializeComponent();

            Text = "STR Editor";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            Width = DpiHelper.Scale(this, 925);
            Height = DpiHelper.Scale(this, 728);

            topPanel = new Panel { Dock = DockStyle.Top, Height = DpiHelper.Scale(this, 40), Padding = new Padding(DpiHelper.Scale(this, 8)) };

            btnOpen = new Button { Text = "Open", AutoSize = true, Left = DpiHelper.Scale(this, 8), Top = DpiHelper.Scale(this, 8) };
            btnSave = new Button { Text = "Save", AutoSize = true, Left = DpiHelper.Scale(this, 90), Top = DpiHelper.Scale(this, 8) };
            btnSaveAs = new Button { Text = "Save As", AutoSize = true, Left = DpiHelper.Scale(this, 170), Top = DpiHelper.Scale(this, 8) };
            btnAddEntry = new Button { Text = "+", Width = DpiHelper.Scale(this, 32), Height = DpiHelper.Scale(this, 24), Left = DpiHelper.Scale(this, 260), Top = DpiHelper.Scale(this, 8) };
            btnRemoveLast = new Button { Text = "-", Width = DpiHelper.Scale(this, 32), Height = DpiHelper.Scale(this, 24), Left = DpiHelper.Scale(this, 296), Top = DpiHelper.Scale(this, 8) };

            topPanel.Controls.Add(btnOpen);
            topPanel.Controls.Add(btnSave);
            topPanel.Controls.Add(btnSaveAs);
            topPanel.Controls.Add(btnAddEntry);
            topPanel.Controls.Add(btnRemoveLast);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false
            };

            Controls.Add(grid);
            Controls.Add(topPanel);

            SetupGrid();
            UpdateTitle();

            btnOpen.Click += btnOpen_Click;
            btnSave.Click += btnSave_Click;
            btnSaveAs.Click += btnSaveAs_Click;
            btnAddEntry.Click += btnAddEntry_Click;
            btnRemoveLast.Click += btnRemoveLast_Click;
        }

        private void SetupGrid()
        {
            grid.AutoGenerateColumns = false;
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Bank",
                DataPropertyName = nameof(StrEntry.Bank),
                Width = DpiHelper.Scale(this, 60)
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = nameof(StrEntry.Id),
                Width = DpiHelper.Scale(this, 60)
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Text",
                DataPropertyName = nameof(StrEntry.Text),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Display hex nicely for Bank/ID
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var prop = grid.Columns[e.ColumnIndex].DataPropertyName;

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
            grid.CellParsing += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var prop = grid.Columns[e.ColumnIndex].DataPropertyName;
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
                        return;
                    }
                }
            };
        }

        private void BindToGrid(StrFile file)
        {
            _file = file;
            _binding = new BindingList<StrEntry>(_file.Entries);
            grid.DataSource = _binding;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            var name = string.IsNullOrWhiteSpace(_path) ? "(no file)" : Path.GetFileName(_path);
            var count = _file?.Entries.Count ?? 0;
            Text = $"STR Editor - {name} ({count} entries)";
        }

        private void btnOpen_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "STR/ADB files (*.str;*.adb)|*.str;*.adb|All files (*.*)|*.*",
                Title = "Open STR file"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _path = ofd.FileName;
                var file = StrFile.Load(_path);
                BindToGrid(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            if (_file == null)
            {
                MessageBox.Show(this, "No STR file loaded.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_path))
            {
                btnSaveAs_Click(sender, e);
                return;
            }

            try
            {
                grid.EndEdit();
                var bytes = _file.SaveToBytes();
                File.WriteAllBytes(_path, bytes);

                MessageBox.Show(this, "Saved.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveAs_Click(object? sender, EventArgs e)
        {
            if (_file == null)
            {
                MessageBox.Show(this, "No STR file loaded.", "Save As", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "STR/ADB files (*.str;*.adb)|*.str;*.adb|All files (*.*)|*.*",
                Title = "Save STR file as..."
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                grid.EndEdit();
                var bytes = _file.SaveToBytes();
                File.WriteAllBytes(sfd.FileName, bytes);

                _path = sfd.FileName;
                MessageBox.Show(this, "Saved.", "Save As", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Save As failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddEntry_Click(object? sender, EventArgs e)
        {
            if (_file == null || _binding == null)
            {
                MessageBox.Show(this, "Open a STR file first.", "Add Entry",
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
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[rowIndex].Cells[2];
            grid.Rows[rowIndex].Selected = true;
            grid.FirstDisplayedScrollingRowIndex = rowIndex;
            grid.BeginEdit(true);

            UpdateTitle();
        }

        private void btnRemoveLast_Click(object? sender, EventArgs e)
        {
            if (_file == null || _binding == null)
            {
                MessageBox.Show(this, "Open a STR file first.", "Remove Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_binding.Count == 0)
            {
                MessageBox.Show(this, "There are no entries to remove.", "Remove Entry",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var last = _binding[_binding.Count - 1];
            var confirm = MessageBox.Show(
                this,
                $"Remove last entry?\n\nBank={last.Bank:X2}  ID={last.Id:X2}\nText=\"{last.Text}\"",
                "Confirm remove",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            grid.EndEdit();

            int newIndex = _binding.Count - 2;
            _binding.RemoveAt(_binding.Count - 1);

            if (_binding.Count > 0 && newIndex >= 0)
            {
                grid.ClearSelection();
                grid.CurrentCell = grid.Rows[newIndex].Cells[2];
                grid.Rows[newIndex].Selected = true;
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, newIndex);
            }

            UpdateTitle();
        }
    }
}