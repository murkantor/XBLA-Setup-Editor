using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    public sealed class MPWeaponSetEditorForm : Form
    {
        // --- UI Controls ---
        private readonly TextBox _txtXexPath;
        private readonly Button _btnLoadXex;
        private readonly Button _btnSaveXex;
        private readonly CheckBox _chkBackup;
        private readonly ListBox _lstWeaponSets;
        private readonly DataGridView _dgvWeapons;
        private readonly TextBox _txtLog;

        // --- State ---
        private MPWeaponSetParser? _parser;
        private byte[]? _xexData;
        private string? _xexPath;
        private int _selectedSetIndex = -1;

        // --- Lookup dictionaries ---
        private readonly Dictionary<int, string> _weaponNames = new();
        private readonly Dictionary<int, string> _ammoNames = new();
        private readonly Dictionary<int, string> _propNames = new();

        public MPWeaponSetEditorForm()
        {
            Text = "MP Weapon Set Editor";
            Width = 1200;
            Height = 750;
            StartPosition = FormStartPosition.CenterParent;

            BuildLookups();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                Padding = new Padding(12)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));  // Weapon set list
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Weapon details
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));  // Buttons

            // Row 0: XEX file path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var pathPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            pathPanel.Controls.Add(new Label { Text = "XEX File:", AutoSize = true, Margin = new Padding(0, 6, 5, 0) });
            _txtXexPath = new TextBox { Width = 500 };
            pathPanel.Controls.Add(_txtXexPath);
            var btnBrowse = new Button { Text = "Browse...", Width = 75 };
            pathPanel.Controls.Add(btnBrowse);
            _btnLoadXex = new Button { Text = "Load", Width = 60 };
            pathPanel.Controls.Add(_btnLoadXex);
            mainLayout.Controls.Add(pathPanel, 0, 0);
            mainLayout.SetColumnSpan(pathPanel, 2);

            // Save button and backup checkbox
            var savePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _chkBackup = new CheckBox { Text = "Backup", Checked = true, AutoSize = true };
            savePanel.Controls.Add(_chkBackup);
            _btnSaveXex = new Button { Text = "Save XEX", Dock = DockStyle.Top, Enabled = false };
            savePanel.Controls.Add(_btnSaveXex);
            mainLayout.Controls.Add(savePanel, 2, 0);

            // Row 1: Labels
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            mainLayout.Controls.Add(new Label { Text = "Weapon Sets:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 1);
            mainLayout.Controls.Add(new Label { Text = "Weapons in Set:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 1, 1);
            mainLayout.SetColumnSpan(mainLayout.GetControlFromPosition(1, 1), 2);

            // Row 2: Lists
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));

            _lstWeaponSets = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            mainLayout.Controls.Add(_lstWeaponSets, 0, 2);

            _dgvWeapons = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window
            };
            SetupWeaponsGrid();
            mainLayout.Controls.Add(_dgvWeapons, 1, 2);
            mainLayout.SetColumnSpan(_dgvWeapons, 2);

            // Row 3: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            mainLayout.Controls.Add(new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 3);
            mainLayout.SetColumnSpan(mainLayout.GetControlFromPosition(0, 3), 3);

            // Row 4: Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false
            };
            mainLayout.Controls.Add(_txtLog, 0, 4);
            mainLayout.SetColumnSpan(_txtLog, 3);

            Controls.Add(mainLayout);

            // --- Events ---
            btnBrowse.Click += (_, __) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select XEX file",
                    Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtXexPath.Text = ofd.FileName;
                }
            };

            _btnLoadXex.Click += (_, __) => LoadXex();
            _btnSaveXex.Click += (_, __) => SaveXex();
            _lstWeaponSets.SelectedIndexChanged += (_, __) => OnWeaponSetSelected();
            _dgvWeapons.CellValueChanged += OnWeaponCellChanged;
            _dgvWeapons.CurrentCellDirtyStateChanged += (_, __) =>
            {
                // Commit combo box changes immediately
                if (_dgvWeapons.IsCurrentCellDirty)
                    _dgvWeapons.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private void BuildLookups()
        {
            foreach (var (name, code) in WeaponData.Pairs)
                _weaponNames[code] = name;
            foreach (var (name, code) in AmmoTypeData.Pairs)
                _ammoNames[code] = name;
            foreach (var (name, code) in PropData.Pairs)
                _propNames[code] = name;
        }

        private void SetupWeaponsGrid()
        {
            _dgvWeapons.Columns.Clear();

            // Slot column
            var colSlot = new DataGridViewTextBoxColumn
            {
                Name = "Slot",
                HeaderText = "#",
                Width = 30,
                ReadOnly = true
            };
            _dgvWeapons.Columns.Add(colSlot);

            // Weapon dropdown
            var colWeapon = new DataGridViewComboBoxColumn
            {
                Name = "Weapon",
                HeaderText = "Weapon",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var (name, code) in WeaponData.Pairs)
                colWeapon.Items.Add(name);
            _dgvWeapons.Columns.Add(colWeapon);

            // Ammo Type dropdown
            var colAmmo = new DataGridViewComboBoxColumn
            {
                Name = "AmmoType",
                HeaderText = "Ammo Type",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var (name, code) in AmmoTypeData.Pairs)
                colAmmo.Items.Add(name);
            _dgvWeapons.Columns.Add(colAmmo);

            // Ammo Count
            var colAmmoCount = new DataGridViewTextBoxColumn
            {
                Name = "AmmoCount",
                HeaderText = "Ammo",
                Width = 50
            };
            _dgvWeapons.Columns.Add(colAmmoCount);

            // Has Prop checkbox
            var colHasProp = new DataGridViewCheckBoxColumn
            {
                Name = "HasProp",
                HeaderText = "Prop?",
                Width = 45
            };
            _dgvWeapons.Columns.Add(colHasProp);

            // Prop dropdown
            var colProp = new DataGridViewComboBoxColumn
            {
                Name = "Prop",
                HeaderText = "Prop Model",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var (name, code) in PropData.Pairs)
                colProp.Items.Add(name);
            _dgvWeapons.Columns.Add(colProp);

            // Scale
            var colScale = new DataGridViewTextBoxColumn
            {
                Name = "Scale",
                HeaderText = "Scale",
                Width = 50
            };
            _dgvWeapons.Columns.Add(colScale);
        }

        private void Log(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        private void LoadXex()
        {
            var path = _txtXexPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "Please select a valid XEX file.", "Load XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _txtLog.Clear();
                Log($"Loading XEX: {path}");

                _xexData = File.ReadAllBytes(path);
                _xexPath = path;
                Log($"XEX size: {_xexData.Length:N0} bytes");

                _parser = MPWeaponSetParser.LoadFromXex(_xexData);
                Log($"Loaded {_parser.WeaponSets.Count} weapon sets");
                Log($"Loaded {_parser.SelectList.Count} select list entries");

                // Populate weapon sets list
                _lstWeaponSets.Items.Clear();
                for (int i = 0; i < _parser.SelectList.Count; i++)
                {
                    var name = i < MPWeaponSetParser.SelectListNames.Length
                        ? MPWeaponSetParser.SelectListNames[i]
                        : $"Unknown {i}";
                    _lstWeaponSets.Items.Add($"[{i}] {name}");
                }

                _btnSaveXex.Enabled = true;
                _selectedSetIndex = -1;
                _dgvWeapons.Rows.Clear();

                Log("");
                Log("Select a weapon set to view/edit its weapons.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "Load Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnWeaponSetSelected()
        {
            if (_parser == null || _lstWeaponSets.SelectedIndex < 0)
                return;

            _selectedSetIndex = _lstWeaponSets.SelectedIndex;
            var selectEntry = _parser.SelectList[_selectedSetIndex];
            var weaponSet = _parser.GetWeaponSetForSelectEntry(selectEntry);

            _dgvWeapons.Rows.Clear();

            if (weaponSet == null)
            {
                Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Random - no weapons)");
                return;
            }

            Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Set {selectEntry.WeaponSetIndex})");

            // Populate weapons grid
            for (int i = 0; i < MPWeaponSetParser.WEAPONS_PER_SET; i++)
            {
                var weapon = weaponSet.Weapons[i];
                int rowIdx = _dgvWeapons.Rows.Add();
                var row = _dgvWeapons.Rows[rowIdx];

                row.Cells["Slot"].Value = i.ToString();
                row.Cells["Weapon"].Value = GetWeaponName(weapon.WeaponId);
                row.Cells["AmmoType"].Value = GetAmmoName(weapon.AmmoType);
                row.Cells["AmmoCount"].Value = weapon.AmmoCount.ToString();
                row.Cells["HasProp"].Value = weapon.WeaponToggle != 0;
                row.Cells["Prop"].Value = GetPropName(weapon.PropId);
                row.Cells["Scale"].Value = weapon.Scale.ToString();
            }
        }

        private void OnWeaponCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_parser == null || _selectedSetIndex < 0 || e.RowIndex < 0)
                return;

            var selectEntry = _parser.SelectList[_selectedSetIndex];
            var weaponSet = _parser.GetWeaponSetForSelectEntry(selectEntry);
            if (weaponSet == null) return;

            var weapon = weaponSet.Weapons[e.RowIndex];
            var row = _dgvWeapons.Rows[e.RowIndex];
            var colName = _dgvWeapons.Columns[e.ColumnIndex].Name;

            try
            {
                switch (colName)
                {
                    case "Weapon":
                        weapon.WeaponId = (byte)GetCodeByName(WeaponData.Pairs, row.Cells["Weapon"].Value?.ToString() ?? "");
                        break;
                    case "AmmoType":
                        weapon.AmmoType = (byte)GetCodeByName(AmmoTypeData.Pairs, row.Cells["AmmoType"].Value?.ToString() ?? "");
                        break;
                    case "AmmoCount":
                        if (byte.TryParse(row.Cells["AmmoCount"].Value?.ToString(), out var ammo))
                            weapon.AmmoCount = ammo;
                        break;
                    case "HasProp":
                        weapon.WeaponToggle = (bool)(row.Cells["HasProp"].Value ?? false) ? (byte)1 : (byte)0;
                        break;
                    case "Prop":
                        weapon.PropId = (ushort)GetCodeByName(PropData.Pairs, row.Cells["Prop"].Value?.ToString() ?? "");
                        break;
                    case "Scale":
                        if (ushort.TryParse(row.Cells["Scale"].Value?.ToString(), out var scale))
                            weapon.Scale = scale;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating weapon: {ex.Message}");
            }
        }

        private void SaveXex()
        {
            if (_parser == null || _xexData == null || string.IsNullOrWhiteSpace(_xexPath))
            {
                MessageBox.Show(this, "No XEX loaded.", "Save XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Create backup if requested
                if (_chkBackup.Checked && File.Exists(_xexPath))
                {
                    var backupPath = _xexPath + ".backup";
                    Log($"Creating backup: {backupPath}");
                    File.Copy(_xexPath, backupPath, overwrite: true);
                }

                // Apply changes to XEX data
                var log = new List<string>();
                _parser.ApplyToXex(_xexData, log);
                foreach (var line in log)
                    Log(line);

                // Save
                File.WriteAllBytes(_xexPath, _xexData);
                Log($"Saved: {_xexPath}");

                MessageBox.Show(this, "XEX saved successfully!", "Save XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "Save Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetWeaponName(int code)
        {
            return _weaponNames.TryGetValue(code, out var name) ? name : $"Unknown (0x{code:X2})";
        }

        private string GetAmmoName(int code)
        {
            return _ammoNames.TryGetValue(code, out var name) ? name : $"Unknown (0x{code:X2})";
        }

        private string GetPropName(int code)
        {
            return _propNames.TryGetValue(code, out var name) ? name : $"Unknown (0x{code:X2})";
        }

        private static int GetCodeByName((string Name, int Code)[] pairs, string name)
        {
            foreach (var pair in pairs)
            {
                if (pair.Name == name)
                    return pair.Code;
            }
            return 0;
        }
    }
}
