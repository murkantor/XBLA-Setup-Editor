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
        private readonly CheckBox _chkRemoveArmor;
        private readonly CheckBox _chkBeginner;
        private readonly ListBox _lstWeaponSets;
        private readonly TextBox _txtTextId;
        private readonly DataGridView _dgvWeapons;
        private readonly TextBox _txtLog;

        // 3-char custom text folder code
        private readonly TextBox _txtTextFolder3;

        // --- State ---
        private MPWeaponSetParser? _parser;
        private byte[]? _xexData;
        private string? _xexPath;
        private int _selectedSetIndex = -1;
        private bool _beginnerMode = true;

        // Guard to prevent recursive CellValueChanged while applying beginner defaults
        private bool _isApplyingBeginnerDefaults = false;

        // --- Lookup dictionaries ---
        private readonly Dictionary<int, string> _weaponNames = new();
        private readonly Dictionary<int, string> _ammoNames = new();
        private readonly Dictionary<int, string> _propNames = new();

        // --- Text folder patch constants ---
        private const int TEXT_FOLDER_OFFSET = 0x0000A3AC; // file offset in XEX
        private const int TEXT_FOLDER_LEN = 3;

        public MPWeaponSetEditorForm()
        {
            Text = "MP Weapon Set Editor";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            Width = DpiHelper.Scale(this, 1200);
            Height = DpiHelper.Scale(this, 750);
            StartPosition = FormStartPosition.CenterParent;

            BuildLookups();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                Padding = new Padding(DpiHelper.Scale(this, 12))
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 200)));  // Weapon set list
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Weapon details
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 100)));  // Buttons

            // Row 0: XEX file path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 32)));
            var pathPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            pathPanel.Controls.Add(new Label { Text = "XEX File:", AutoSize = true, Margin = new Padding(0, DpiHelper.Scale(this, 6), DpiHelper.Scale(this, 5), 0) });
            _txtXexPath = new TextBox { Width = DpiHelper.Scale(this, 500), Height = DpiHelper.Scale(this, 23) };
            pathPanel.Controls.Add(_txtXexPath);
            var btnBrowse = new Button { Text = "Browse...", Width = DpiHelper.Scale(this, 75), Height = DpiHelper.Scale(this, 26) };
            pathPanel.Controls.Add(btnBrowse);
            _btnLoadXex = new Button { Text = "Load", Width = DpiHelper.Scale(this, 60), Height = DpiHelper.Scale(this, 26) };
            pathPanel.Controls.Add(_btnLoadXex);

            _chkBackup = new CheckBox { Text = "Backup", Checked = true, AutoSize = true, Margin = new Padding(DpiHelper.Scale(this, 10), DpiHelper.Scale(this, 6), 0, 0) };
            pathPanel.Controls.Add(_chkBackup);

            // Armor removal checkbox
            _chkRemoveArmor = new CheckBox
            {
                Text = "Remove Armor",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(DpiHelper.Scale(this, 10), DpiHelper.Scale(this, 6), 0, 0),
                ForeColor = Color.DarkRed
            };
            pathPanel.Controls.Add(_chkRemoveArmor);

            // 3-char text folder code at 0x0000A3AC
            pathPanel.Controls.Add(new Label { Text = "Text Folder:", AutoSize = true, Margin = new Padding(DpiHelper.Scale(this, 10), DpiHelper.Scale(this, 6), DpiHelper.Scale(this, 5), 0) });
            _txtTextFolder3 = new TextBox
            {
                Width = DpiHelper.Scale(this, 40),
                Height = DpiHelper.Scale(this, 23),
                MaxLength = 3,
                CharacterCasing = CharacterCasing.Upper
            };
            pathPanel.Controls.Add(_txtTextFolder3);

            _btnSaveXex = new Button { Text = "Save XEX", Width = DpiHelper.Scale(this, 75), Height = DpiHelper.Scale(this, 26), Enabled = false };
            pathPanel.Controls.Add(_btnSaveXex);

            mainLayout.Controls.Add(pathPanel, 0, 0);
            mainLayout.SetColumnSpan(pathPanel, 3);

            // Row 1: Labels and Text ID
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 26)));
            mainLayout.Controls.Add(new Label { Text = "Weapon Sets:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 1);

            var textIdPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            textIdPanel.Controls.Add(new Label { Text = "Weapons in Set:      Text ID: 0x", AutoSize = true, Margin = new Padding(0, DpiHelper.Scale(this, 6), 0, 0) });

            _txtTextId = new TextBox { Width = DpiHelper.Scale(this, 60), MaxLength = 4, CharacterCasing = CharacterCasing.Upper, Enabled = false };
            _txtTextId.Leave += (_, __) => OnTextIdChanged();
            _txtTextId.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) OnTextIdChanged(); };
            textIdPanel.Controls.Add(_txtTextId);

            _chkBeginner = new CheckBox { Text = "Beginner (auto-fill ammo/prop)", AutoSize = true, Checked = true, Margin = new Padding(DpiHelper.Scale(this, 20), DpiHelper.Scale(this, 4), 0, 0) };
            _chkBeginner.CheckedChanged += (_, __) => _beginnerMode = _chkBeginner.Checked;
            textIdPanel.Controls.Add(_chkBeginner);

            mainLayout.Controls.Add(textIdPanel, 1, 1);
            mainLayout.SetColumnSpan(textIdPanel, 2);

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
                BackgroundColor = SystemColors.Window,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            SetupWeaponsGrid();
            mainLayout.Controls.Add(_dgvWeapons, 1, 2);
            mainLayout.SetColumnSpan(_dgvWeapons, 2);

            // Row 3: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 22)));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, 3);
            mainLayout.SetColumnSpan(lblLog, 3);

            // Row 4: Log
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this)),
                WordWrap = false
            };
            mainLayout.Controls.Add(_txtLog, 0, 4);
            mainLayout.SetColumnSpan(_txtLog, 3);

            Controls.Add(mainLayout);

            // Tooltips
            var toolTip = new ToolTip();
            toolTip.SetToolTip(_chkRemoveArmor,
                "Scans XEX setup region (0xC7DF38-0xDDFF5F) and overwrites armor objects (type 0x15) with zeros.\n" +
                "File size remains unchanged - objects are NOPed in place.");

            toolTip.SetToolTip(_txtTextFolder3,
                "3-letter folder code written into XEX at 0x0000A3AC.\nExample: ENG, FRA, DEU, etc.");

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
            _dgvWeapons.DataError += (_, e) =>
            {
                // Suppress combo box errors for unknown values
                e.ThrowException = false;
            };
            _dgvWeapons.CellClick += (_, e) =>
            {
                // Immediately show dropdown when clicking combo box cells
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    _dgvWeapons.BeginEdit(true);
                    if (_dgvWeapons.EditingControl is ComboBox cb)
                        cb.DroppedDown = true;
                }
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
                Width = DpiHelper.Scale(this, 30),
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
            foreach (var (name, _) in WeaponData.Pairs)
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
            foreach (var (name, _) in AmmoTypeData.Pairs)
                colAmmo.Items.Add(name);
            _dgvWeapons.Columns.Add(colAmmo);

            // Ammo Count
            var colAmmoCount = new DataGridViewTextBoxColumn
            {
                Name = "AmmoCount",
                HeaderText = "Ammo",
                Width = DpiHelper.Scale(this, 50)
            };
            _dgvWeapons.Columns.Add(colAmmoCount);

            // Has Prop checkbox
            var colHasProp = new DataGridViewCheckBoxColumn
            {
                Name = "HasProp",
                HeaderText = "Prop?",
                Width = DpiHelper.Scale(this, 45)
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
            foreach (var (name, _) in PropData.Pairs)
                colProp.Items.Add(name);
            _dgvWeapons.Columns.Add(colProp);

            // Scale
            var colScale = new DataGridViewTextBoxColumn
            {
                Name = "Scale",
                HeaderText = "Scale",
                Width = DpiHelper.Scale(this, 50)
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
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "XEX file not found.", "Load XEX",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _txtLog.Clear();
                Log($"Loading XEX: {path}");

                _xexData = File.ReadAllBytes(path);
                _xexPath = path;
                Log($"XEX size: {_xexData.Length:N0} bytes");

                // Read current 3-char text folder code
                _txtTextFolder3.Text = ReadTextFolderCode(_xexData);
                if (!string.IsNullOrWhiteSpace(_txtTextFolder3.Text))
                    Log($"Text folder code: {_txtTextFolder3.Text}");

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

            // Update Text ID field (show as 4 hex chars)
            _txtTextId.Text = (selectEntry.TextId >> 16).ToString("X4");
            _txtTextId.Enabled = true;

            _dgvWeapons.Rows.Clear();

            if (weaponSet == null)
            {
                Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Random - no weapons)");
                return;
            }

            Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Set {selectEntry.WeaponSetIndex})");

            // Get combo box columns for adding unknown values
            var weaponCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["Weapon"];
            var ammoCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["AmmoType"];
            var propCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["Prop"];

            // Populate weapons grid
            for (int i = 0; i < MPWeaponSetParser.WEAPONS_PER_SET; i++)
            {
                var weapon = weaponSet.Weapons[i];
                int rowIdx = _dgvWeapons.Rows.Add();
                var row = _dgvWeapons.Rows[rowIdx];

                var weaponName = GetWeaponName(weapon.WeaponId);
                var ammoName = GetAmmoName(weapon.AmmoType);
                var propName = GetPropName(weapon.PropId);

                EnsureComboBoxContains(weaponCol, weaponName);
                EnsureComboBoxContains(ammoCol, ammoName);
                EnsureComboBoxContains(propCol, propName);

                row.Cells["Slot"].Value = i.ToString();
                row.Cells["Weapon"].Value = weaponName;
                row.Cells["AmmoType"].Value = ammoName;
                row.Cells["AmmoCount"].Value = weapon.AmmoCount.ToString();
                row.Cells["HasProp"].Value = weapon.WeaponToggle != 0;
                row.Cells["Prop"].Value = propName;
                row.Cells["Scale"].Value = weapon.Scale.ToString();
                row.Tag = weapon;
            }
        }

        private static void EnsureComboBoxContains(DataGridViewComboBoxColumn col, string value)
        {
            if (!col.Items.Contains(value))
                col.Items.Add(value);
        }

        private void OnTextIdChanged()
        {
            if (_parser == null || _selectedSetIndex < 0)
                return;

            var text = _txtTextId.Text.Trim();
            if (uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var textIdUpper))
            {
                uint fullTextId = textIdUpper << 16;
                _parser.SelectList[_selectedSetIndex].TextId = fullTextId;
                Log($"Text ID for {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} changed to 0x{fullTextId:X8}");
            }
            else
            {
                _txtTextId.Text = (_parser.SelectList[_selectedSetIndex].TextId >> 16).ToString("X4");
            }
        }

        private void OnWeaponCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
            // Prevent re-entrant CellValueChanged caused by our own beginner autofill writes
            if (_isApplyingBeginnerDefaults)
                return;

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
                        weapon.WeaponId = (byte)GetCodeByName(
                            WeaponData.Pairs,
                            row.Cells["Weapon"].Value?.ToString() ?? ""
                        );

                        if (_beginnerMode)
                        {
                            _isApplyingBeginnerDefaults = true;
                            try
                            {
                                ApplyBeginnerDefaultsToRow(row, weapon);
                            }
                            finally
                            {
                                _isApplyingBeginnerDefaults = false;
                            }
                        }
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
                        weapon.PropId = (byte)GetCodeByName(PropData.Pairs, row.Cells["Prop"].Value?.ToString() ?? "");
                        break;

                    case "Scale":
                        if (byte.TryParse(row.Cells["Scale"].Value?.ToString(), out var scale))
                            weapon.Scale = scale;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating weapon: {ex.Message}");
            }
        }

        // Beginner Mode: use BeginnerRulesData (strings must match AmmoTypeData/PropData names)
        private void ApplyBeginnerDefaultsToRow(DataGridViewRow row, MPWeaponSetParser.WeaponEntry weapon)
        {
            var weaponName = row.Cells["Weapon"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(weaponName))
                return;

            // --- Ammo Type (from BeginnerRulesData) ---
            var ammoTypeName = GetBeginnerAmmoTypeName(weaponName); // e.g. "9mm Ammo"
            EnsureComboBoxContains((DataGridViewComboBoxColumn)_dgvWeapons.Columns["AmmoType"], ammoTypeName);

            row.Cells["AmmoType"].Value = ammoTypeName;
            weapon.AmmoType = (byte)GetCodeByName(AmmoTypeData.Pairs, ammoTypeName);

            // --- Ammo Count (from BeginnerRulesData) ---
            var ammoCountText = GetBeginnerDefaultAmmoCount(weaponName); // e.g. "50"
            row.Cells["AmmoCount"].Value = ammoCountText;

            if (byte.TryParse(ammoCountText, out var ammoCount))
                weapon.AmmoCount = ammoCount;

            // --- Prop defaults ---
            bool shouldHaveProp =
                BeginnerRulesData.PropNames.Contains(weaponName) &&
                !weaponName.Equals("Nothing (No Pickup)", StringComparison.OrdinalIgnoreCase) &&
                !weaponName.Equals("Unarmed", StringComparison.OrdinalIgnoreCase);

            if (shouldHaveProp)
            {
                row.Cells["HasProp"].Value = true;
                weapon.WeaponToggle = 1;

                var propName = weaponName;
                EnsureComboBoxContains((DataGridViewComboBoxColumn)_dgvWeapons.Columns["Prop"], propName);

                row.Cells["Prop"].Value = propName;
                weapon.PropId = (byte)GetCodeByName(PropData.Pairs, propName);
            }
            else
            {
                row.Cells["HasProp"].Value = false;
                weapon.WeaponToggle = 0;

                const string noneProp = "None";
                EnsureComboBoxContains((DataGridViewComboBoxColumn)_dgvWeapons.Columns["Prop"], noneProp);

                row.Cells["Prop"].Value = noneProp;
                weapon.PropId = (byte)GetCodeByName(PropData.Pairs, noneProp);
            }
        }

        private static string GetBeginnerAmmoTypeName(string weaponName)
        {
            if (BeginnerRulesData.WeaponToAmmoType.TryGetValue(weaponName, out var ammoType))
                return ammoType;

            return "None";
        }

        private static string GetBeginnerDefaultAmmoCount(string weaponName)
        {
            if (BeginnerRulesData.WeaponToDefaultAmmoCount.TryGetValue(weaponName, out var count))
                return count;

            return "0";
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

                // Apply weapon set changes to XEX data
                var log = new List<string>();
                _parser.ApplyToXex(_xexData, log);
                foreach (var line in log)
                    Log(line);

                // Patch 3-char custom text folder code at 0x0000A3AC
                Log("");
                var folderLog = new List<string>();
                ApplyTextFolderPatch(_xexData, _txtTextFolder3.Text, folderLog);
                foreach (var line in folderLog)
                    Log(line);

                // Armor removal
                if (_chkRemoveArmor.Checked)
                {
                    Log("");
                    Log("=== Armor Removal ===");

                    var armorLog = new List<string>();
                    var scanResult = XEXArmorRemover.ScanForArmor(_xexData, armorLog);

                    foreach (var line in armorLog)
                        Log(line);

                    if (scanResult.ArmorBlocks.Count > 0)
                    {
                        var result = MessageBox.Show(this,
                            $"Found {scanResult.ArmorBlocks.Count} armor objects ({scanResult.TotalArmorSize:N0} bytes).\n\n" +
                            $"NOP (zero out) armor objects in XEX?\n\n" +
                            $"Method: Overwrite each 0x{XEXArmorRemover.ARMOR_RECORD_SIZE:X} record with 0x00 bytes\n" +
                            $"File size: {_xexData.Length:N0} bytes (unchanged)\n" +
                            $"Armor objects: {scanResult.ArmorBlocks.Count}",
                            "Confirm Armor Removal",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            var removalLog = new List<string>();
                            _xexData = XEXArmorRemover.RemoveArmor(_xexData, scanResult, removalLog);

                            foreach (var line in removalLog)
                                Log(line);

                            Log("");
                            Log("✓ Armor removal complete!");
                        }
                        else
                        {
                            Log("Armor removal cancelled by user.");
                        }
                    }
                    else
                    {
                        Log("No armor objects found.");
                    }
                }

                File.WriteAllBytes(_xexPath, _xexData);
                Log($"Saved: {_xexPath}");
                Log($"Final size: {_xexData.Length:N0} bytes");

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
            return _weaponNames.TryGetValue(code, out var name) ? name : $"{code:X2}";
        }

        private string GetAmmoName(int code)
        {
            return _ammoNames.TryGetValue(code, out var name) ? name : $"{code:X2}";
        }

        private string GetPropName(int code)
        {
            return _propNames.TryGetValue(code, out var name) ? name : $"{code:X2}";
        }

        private static int GetCodeByName((string Name, int Code)[] pairs, string name)
        {
            foreach (var pair in pairs)
            {
                if (pair.Name == name)
                    return pair.Code;
            }

            // Try to parse as hex (e.g., "0A" or "FF")
            if (name.Length <= 4 && int.TryParse(name, System.Globalization.NumberStyles.HexNumber, null, out var code))
                return code;

            return 0;
        }

        // =========================
        // Text folder patch helpers
        // =========================

        private static string ReadTextFolderCode(byte[] xexData)
        {
            if (xexData == null || xexData.Length < TEXT_FOLDER_OFFSET + TEXT_FOLDER_LEN)
                return "";

            char[] chars = new char[TEXT_FOLDER_LEN];
            for (int i = 0; i < TEXT_FOLDER_LEN; i++)
            {
                byte b = xexData[TEXT_FOLDER_OFFSET + i];
                chars[i] = (b >= 0x20 && b <= 0x7E) ? (char)b : '?';
            }
            return new string(chars);
        }

        private static void ApplyTextFolderPatch(byte[] xexData, string? folder3, List<string> log)
        {
            log.Add("=== Text Folder Patch ===");
            log.Add($"Offset: 0x{TEXT_FOLDER_OFFSET:X6} (len {TEXT_FOLDER_LEN})");

            if (xexData == null)
            {
                log.Add("ERROR: XEX data is null.");
                return;
            }

            if (xexData.Length < TEXT_FOLDER_OFFSET + TEXT_FOLDER_LEN)
            {
                log.Add($"ERROR: XEX too small to patch text folder (size {xexData.Length:N0}).");
                return;
            }

            folder3 = (folder3 ?? "").Trim().ToUpperInvariant();

            if (folder3.Length != TEXT_FOLDER_LEN)
            {
                log.Add($"ERROR: Folder code must be exactly {TEXT_FOLDER_LEN} characters.");
                log.Add($"Current input: \"{folder3}\" (len {folder3.Length})");
                return;
            }

            for (int i = 0; i < folder3.Length; i++)
            {
                char c = folder3[i];
                if (c < 0x20 || c > 0x7E)
                {
                    log.Add("ERROR: Folder code must be ASCII printable characters.");
                    return;
                }
            }

            string before = ReadTextFolderCode(xexData);
            log.Add($"Before: \"{before}\"");
            log.Add($"After : \"{folder3}\"");

            for (int i = 0; i < TEXT_FOLDER_LEN; i++)
                xexData[TEXT_FOLDER_OFFSET + i] = (byte)folder3[i];

            log.Add("Patched text folder code successfully.");
        }
    }
}
