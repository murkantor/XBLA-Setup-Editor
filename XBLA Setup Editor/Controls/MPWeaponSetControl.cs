// =============================================================================
// MPWeaponSetControl.cs - Multiplayer Weapon Set Editor Tab
// =============================================================================
// UserControl that provides the "MP Weapon Sets" tab for editing the 16
// multiplayer weapon set configurations in GoldenEye XBLA.
//
// TAB FUNCTIONALITY:
// ==================
// 1. Lists all 16 weapon sets (Random + 15 named sets)
// 2. Displays 8 weapon slots per set in editable DataGridView
// 3. Provides "Beginner Mode" for automatic ammo/prop configuration
// 4. Allows editing menu text IDs for set names
// 5. Includes armor removal option and text folder patching
//
// WEAPON SET EDITING:
// ===================
// Each weapon slot can be configured with:
// - Weapon: Dropdown of all 32+ weapons
// - Ammo Type: Dropdown of all ammunition types
// - Ammo Count: Starting ammunition amount (0-255)
// - Has Prop: Whether a pickup model spawns on the ground
// - Prop Model: Which 3D model to use for the pickup
// - Scale: Model scale factor
//
// BEGINNER MODE:
// ==============
// When enabled, selecting a weapon automatically fills in:
// - Correct ammo type (e.g., PP7 → 9mm Ammo)
// - Balanced ammo count (e.g., SMGs get 100, pistols get 50)
// - Appropriate prop model (same as weapon name)
// - Prop enabled/disabled based on weapon type
//
// This uses the mappings defined in BeginnerRulesData.cs.
//
// ADDITIONAL FEATURES:
// ====================
// - Remove Armor: Scans XEX for Body Armor objects and zeros them out
// - Text Folder: Patches the 3-character language folder code at 0xA3AC
// - Text ID: Hex ID for the weapon set name string (upper 16 bits)
//
// DATA FLOW:
// ==========
// 1. MainForm loads XEX and calls OnXexLoaded()
// 2. MPWeaponSetParser extracts all weapon set data
// 3. ListBox shows all 16 sets, selecting one populates the grid
// 4. Edits update the parser's data structures immediately
// 5. GetModifiedXexData() writes changes back to XEX bytes
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// Tab control for editing multiplayer weapon sets. Features beginner mode
    /// for automatic configuration and armor removal functionality.
    /// </summary>
    public sealed class MPWeaponSetControl : UserControl, IXexTab
    {
        // --- UI Controls ---
        private CheckBox _chkRemoveArmor = null!;
        private readonly CheckBox _chkBeginner;
        private readonly ListBox _lstWeaponSets;
        private readonly TextBox _txtTextId;
        private readonly DataGridView _dgvWeapons;
        private readonly TextBox _txtLog;
        private TextBox _txtTextFolder3 = null!;
        private TextBox? _txtLanId;

        // --- State ---
        private MPWeaponSetParser? _parser;
        private byte[]? _xexData;
        private string? _xexPath;
        private int _selectedSetIndex = -1;
        private bool _beginnerMode = true;
        private bool _isApplyingBeginnerDefaults = false;
        private bool _hasUnsavedChanges = false;

        // --- Lookup dictionaries ---
        private readonly Dictionary<int, string> _weaponNames = new();
        private readonly Dictionary<int, string> _ammoNames = new();
        private readonly Dictionary<int, string> _propNames = new();

        // Text folder patch constants
        internal const int TEXT_FOLDER_OFFSET = 0x0000A3AC;
        private const int TEXT_FOLDER_LEN = 3;

        // Events
        public event EventHandler<XexModifiedEventArgs>? XexModified;

        public MPWeaponSetControl()
        {
            BuildLookups();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                Padding = new Padding(DpiHelper.Scale(this, 8))
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 200)));  // Weapon set list
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Weapon details
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(this, 100)));  // Buttons

            // Row 0: Options
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 32)));
            var optionsPanel = CreateOptionsPanel();
            mainLayout.Controls.Add(optionsPanel, 0, 0);
            mainLayout.SetColumnSpan(optionsPanel, 3);

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

            // Events
            _lstWeaponSets.SelectedIndexChanged += (_, __) => OnWeaponSetSelected();
            _dgvWeapons.CellValueChanged += OnWeaponCellChanged;
            _dgvWeapons.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (_dgvWeapons.IsCurrentCellDirty)
                    _dgvWeapons.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _dgvWeapons.DataError += (_, e) => e.ThrowException = false;
            _dgvWeapons.CellClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    _dgvWeapons.BeginEdit(true);
                    if (_dgvWeapons.EditingControl is ComboBox cb)
                        cb.DroppedDown = true;
                }
            };

            SetupTooltips();
        }

        private FlowLayoutPanel CreateOptionsPanel()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            _chkRemoveArmor = new CheckBox
            {
                Text = "Remove Armor",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, DpiHelper.Scale(this, 6), DpiHelper.Scale(this, 15), 0),
                ForeColor = Color.DarkRed
            };
            panel.Controls.Add(_chkRemoveArmor);

            panel.Controls.Add(new Label { Text = "Text Folder:", AutoSize = true, Margin = new Padding(DpiHelper.Scale(this, 10), DpiHelper.Scale(this, 6), DpiHelper.Scale(this, 5), 0) });
            _txtTextFolder3 = new TextBox
            {
                Width = DpiHelper.Scale(this, 40),
                MaxLength = 3,
                CharacterCasing = CharacterCasing.Upper
            };
            panel.Controls.Add(_txtTextFolder3);

            panel.Controls.Add(new Label { Text = "LAN ID:", AutoSize = true, Margin = new Padding(DpiHelper.Scale(this, 10), DpiHelper.Scale(this, 6), DpiHelper.Scale(this, 5), 0) });
            _txtLanId = new TextBox
            {
                Width = DpiHelper.Scale(this, 310),
                ReadOnly = true,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this))
            };
            panel.Controls.Add(_txtLanId);

            return panel;
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_chkRemoveArmor, TooltipTexts.MpWeaponSets.RemoveArmor);
            toolTip.SetToolTip(_txtTextFolder3, TooltipTexts.MpWeaponSets.TextFolder);
            toolTip.SetToolTip(_lstWeaponSets, TooltipTexts.MpWeaponSets.WeaponSetsList);
            toolTip.SetToolTip(_txtTextId, TooltipTexts.MpWeaponSets.TextId);
            toolTip.SetToolTip(_chkBeginner, TooltipTexts.MpWeaponSets.BeginnerMode);
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

            var colSlot = new DataGridViewTextBoxColumn
            {
                Name = "Slot",
                HeaderText = "#",
                Width = DpiHelper.Scale(this, 30),
                ReadOnly = true
            };
            _dgvWeapons.Columns.Add(colSlot);

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

            var colAmmoCount = new DataGridViewTextBoxColumn
            {
                Name = "AmmoCount",
                HeaderText = "Ammo",
                Width = DpiHelper.Scale(this, 50)
            };
            _dgvWeapons.Columns.Add(colAmmoCount);

            var colHasProp = new DataGridViewCheckBoxColumn
            {
                Name = "HasProp",
                HeaderText = "Prop?",
                Width = DpiHelper.Scale(this, 45)
            };
            _dgvWeapons.Columns.Add(colHasProp);

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

        #region IXexTab Implementation

        public string TabDisplayName => "Multiplayer Weapon Sets";

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void OnXexLoaded(byte[] xexData, string path)
        {
            try
            {
                _txtLog.Clear();
                Log($"Loading XEX: {path}");

                _xexData = xexData;
                _xexPath = path;
                Log($"XEX size: {_xexData.Length:N0} bytes");

                // Read current 3-char text folder code
                _txtTextFolder3.Text = ReadTextFolderCode(_xexData);
                if (!string.IsNullOrWhiteSpace(_txtTextFolder3.Text))
                    Log($"Text folder code: {_txtTextFolder3.Text}");

                _parser = MPWeaponSetParser.LoadFromXex(_xexData);
                Log($"Loaded {_parser.WeaponSets.Count} weapon sets");
                Log($"Loaded {_parser.SelectList.Count} select list entries");

                var currentLanId = LanIdHelper.Read(_xexData);
                if (_txtLanId != null) _txtLanId.Text = LanIdHelper.ToHex(currentLanId);
                Log($"LAN ID (current): {LanIdHelper.ToHex(currentLanId)}");

                _lstWeaponSets.Items.Clear();
                for (int i = 0; i < _parser.SelectList.Count; i++)
                {
                    var name = i < MPWeaponSetParser.SelectListNames.Length
                        ? MPWeaponSetParser.SelectListNames[i]
                        : $"Unknown {i}";
                    _lstWeaponSets.Items.Add($"[{i}] {name}");
                }

                _selectedSetIndex = -1;
                _dgvWeapons.Rows.Clear();
                _hasUnsavedChanges = false;

                Log("");
                Log("Select a weapon set to view/edit its weapons.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(FindForm(), ex.ToString(), "Load Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void OnXexDataUpdated(byte[] xexData)
        {
            // Lightweight update - just refresh the data reference
            _xexData = xexData;
        }

        public void OnXexUnloaded()
        {
            _parser = null;
            _xexData = null;
            _xexPath = null;
            _lstWeaponSets.Items.Clear();
            _dgvWeapons.Rows.Clear();
            _txtTextId.Text = "";
            _txtTextId.Enabled = false;
            _txtTextFolder3.Text = "";
            if (_txtLanId != null) _txtLanId.Text = "";
            _txtLog.Clear();
            _hasUnsavedChanges = false;
        }

        public byte[]? GetModifiedXexData()
        {
            if (!_hasUnsavedChanges || _parser == null || _xexData == null)
                return null;

            // Apply changes to XEX data
            var log = new List<string>();
            _parser.ApplyToXex(_xexData, log);

            // Apply text folder patch
            var folderLog = new List<string>();
            ApplyTextFolderPatch(_xexData, _txtTextFolder3.Text, folderLog);

            // Handle armor removal if checked
            if (_chkRemoveArmor.Checked)
            {
                var armorLog = new List<string>();
                var scanResult = XEXArmorRemover.ScanForArmor(_xexData, armorLog);
                if (scanResult.ArmorBlocks.Count > 0)
                {
                    var removalLog = new List<string>();
                    _xexData = XEXArmorRemover.RemoveArmor(_xexData, scanResult, removalLog);
                }
            }

            // Compute and write LAN ID from all applied changes
            var hash = LanIdHelper.Compute(_xexData);
            LanIdHelper.Write(_xexData, hash);
            if (_txtLanId != null) _txtLanId.Text = LanIdHelper.ToHex(hash);
            Log($"LAN ID written: {LanIdHelper.ToHex(hash)}");

            _hasUnsavedChanges = false;
            return _xexData;
        }

        #endregion

        private void OnWeaponSetSelected()
        {
            if (_parser == null || _lstWeaponSets.SelectedIndex < 0)
                return;

            _selectedSetIndex = _lstWeaponSets.SelectedIndex;
            var selectEntry = _parser.SelectList[_selectedSetIndex];
            var weaponSet = _parser.GetWeaponSetForSelectEntry(selectEntry);

            _txtTextId.Text = (selectEntry.TextId >> 16).ToString("X4");
            _txtTextId.Enabled = true;

            _dgvWeapons.Rows.Clear();

            if (weaponSet == null)
            {
                Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Random - no weapons)");
                return;
            }

            Log($"Selected: {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} (Set {selectEntry.WeaponSetIndex})");

            var weaponCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["Weapon"];
            var ammoCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["AmmoType"];
            var propCol = (DataGridViewComboBoxColumn)_dgvWeapons.Columns["Prop"];

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
                _hasUnsavedChanges = true;
                Log($"Text ID for {MPWeaponSetParser.SelectListNames[_selectedSetIndex]} changed to 0x{fullTextId:X8}");
                NotifyXexModified();
            }
            else
            {
                _txtTextId.Text = (_parser.SelectList[_selectedSetIndex].TextId >> 16).ToString("X4");
            }
        }

        private void OnWeaponCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
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

                _hasUnsavedChanges = true;
                NotifyXexModified();
            }
            catch (Exception ex)
            {
                Log($"Error updating weapon: {ex.Message}");
            }
        }

        private void ApplyBeginnerDefaultsToRow(DataGridViewRow row, MPWeaponSetParser.WeaponEntry weapon)
        {
            var weaponName = row.Cells["Weapon"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(weaponName))
                return;

            var ammoTypeName = GetBeginnerAmmoTypeName(weaponName);
            EnsureComboBoxContains((DataGridViewComboBoxColumn)_dgvWeapons.Columns["AmmoType"], ammoTypeName);

            row.Cells["AmmoType"].Value = ammoTypeName;
            weapon.AmmoType = (byte)GetCodeByName(AmmoTypeData.Pairs, ammoTypeName);

            var ammoCountText = GetBeginnerDefaultAmmoCount(weaponName);
            row.Cells["AmmoCount"].Value = ammoCountText;

            if (byte.TryParse(ammoCountText, out var ammoCount))
                weapon.AmmoCount = ammoCount;

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

        private void NotifyXexModified()
        {
            if (_xexData != null)
            {
                // Apply current changes to the data
                var log = new List<string>();
                _parser?.ApplyToXex(_xexData, log);
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));
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

            if (name.Length <= 4 && int.TryParse(name, System.Globalization.NumberStyles.HexNumber, null, out var code))
                return code;

            return 0;
        }

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
            if (xexData == null || xexData.Length < TEXT_FOLDER_OFFSET + TEXT_FOLDER_LEN)
                return;

            folder3 = (folder3 ?? "").Trim().ToUpperInvariant();

            if (folder3.Length != TEXT_FOLDER_LEN)
                return;

            for (int i = 0; i < folder3.Length; i++)
            {
                char c = folder3[i];
                if (c < 0x20 || c > 0x7E)
                    return;
            }

            for (int i = 0; i < TEXT_FOLDER_LEN; i++)
                xexData[TEXT_FOLDER_OFFSET + i] = (byte)folder3[i];
        }
    }
}
