// =============================================================================
// WeaponStatsControl.cs - Weapon Statistics Editor Tab
// =============================================================================
// UserControl that provides the "Weapon Stats" tab for editing all weapon
// statistics, model data, and ammo reserve capacities in GoldenEye XBLA.
//
// TAB FUNCTIONALITY:
// ==================
// Three sub-tabs organized in a TabControl:
// 1. Weapon Stats: 47 weapons × 30+ editable fields (damage, accuracy, etc.)
// 2. Weapon Models: 89 model entries (RAM addresses, display positions)
// 3. Ammo Reserve: 30 entries (max capacity, icon offset, pointers)
//
// WEAPON STATS EDITING:
// =====================
// Each weapon entry has these editable fields:
// - Combat: Damage, Penetration, Inaccuracy, Scope, Force of Impact
// - Firing: Magazine Size, Ammo Type, Fire Modes (Auto/Single)
// - Recoil: Backward, Upward, Bolt, Sway
// - AI: Volume detection for Single/Multi/Active fire, Baselines
// - Display: Screen X/Y/Z position, Aim shifts, Muzzle flash
// - Audio: Sound Effect ID, Sound Trigger Rate
// - Flags: Item bitflags, Ejected Casings RAM address
//
// N64 IMPORT FUNCTIONALITY:
// =========================
// When a 21990 file is loaded (via MainForm), this tab can automatically
// import weapon stats with two layout options:
// - XBLA Layout: Assumes 21990 uses same field order as XBLA (debugging)
// - N64 Layout: Performs field remapping (FromN64Bytes conversion)
//
// RAM ADDRESS PRESERVATION:
// =========================
// When "Preserve XBLA RAM Addrs" is checked:
// - EjectedCasingsRAM in weapon stats
// - ModelDetailsRAM, GZTextStringRAM, StatisticsRAM in weapon models
// - Pointer in ammo reserves
// These addresses are preserved from the original XEX because N64 addresses
// are invalid in the XBLA memory map.
//
// DATA GRIDS:
// ===========
// - Double-buffered for smooth scrolling (no flickering)
// - Frozen columns for Index and Weapon Name (always visible)
// - Hex values displayed with 0x prefix, parsed on edit
// - Float values displayed with 4 decimal places
//
// INTERFACE IMPLEMENTATIONS:
// ==========================
// IXexTab: Receives XEX load/unload, provides modified data
// I21990Tab: Receives 21990 load/unload, auto-imports weapon stats
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
    /// Tab control for editing weapon statistics, models, and ammo reserves.
    /// Supports direct XEX editing and import from N64 21990 configuration files.
    /// </summary>
    public sealed class WeaponStatsControl : UserControl, IXexTab, I21990Tab
    {
        // --- UI Controls ---
        private CheckBox _chkUseXblaLayout = null!;
        private CheckBox _chkPreserveRamAddrs = null!;
        private Button _btnApply = null!;
        private readonly TabControl _tabControl;
        private readonly DataGridView _dgvWeaponStats;
        private readonly DataGridView _dgvWeaponModels;
        private readonly DataGridView _dgvAmmoReserve;
        private readonly TextBox _txtLog;

        // --- State ---
        private WeaponStatsParser? _parser;
        private byte[]? _xexData;
        private string? _xexPath;
        private byte[]? _21990Data;
        private string? _21990Path;
        private bool _hasUnsavedChanges = false;

        // Events
        public event EventHandler<XexModifiedEventArgs>? XexModified;

        public WeaponStatsControl()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(8)
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            // Row 0: Options and Apply button
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var optionsPanel = CreateOptionsPanel();
            mainLayout.Controls.Add(optionsPanel, 0, 0);
            _btnApply = new Button { Text = "Apply", Dock = DockStyle.Fill, Enabled = false };
            _btnApply.Click += (_, __) => ApplyToXex();
            mainLayout.Controls.Add(_btnApply, 1, 0);

            // Row 1: Tab control with data grids
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            var tabWeaponStats = new TabPage("Weapon Stats");
            _dgvWeaponStats = CreateDataGridView();
            SetupWeaponStatsGrid();
            tabWeaponStats.Controls.Add(_dgvWeaponStats);
            _tabControl.TabPages.Add(tabWeaponStats);

            var tabWeaponModels = new TabPage("Weapon Models");
            _dgvWeaponModels = CreateDataGridView();
            SetupWeaponModelsGrid();
            tabWeaponModels.Controls.Add(_dgvWeaponModels);
            _tabControl.TabPages.Add(tabWeaponModels);

            var tabAmmoReserve = new TabPage("Ammo Reserve");
            _dgvAmmoReserve = CreateDataGridView();
            SetupAmmoReserveGrid();
            tabAmmoReserve.Controls.Add(_dgvAmmoReserve);
            _tabControl.TabPages.Add(tabAmmoReserve);

            mainLayout.Controls.Add(_tabControl, 0, 1);
            mainLayout.SetColumnSpan(_tabControl, 2);

            // Row 2: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, 2);
            mainLayout.SetColumnSpan(lblLog, 2);

            // Row 3: Log
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
            mainLayout.Controls.Add(_txtLog, 0, 3);
            mainLayout.SetColumnSpan(_txtLog, 2);

            Controls.Add(mainLayout);

            // Events
            _dgvWeaponStats.CellValueChanged += OnWeaponStatsCellChanged;
            _dgvWeaponModels.CellValueChanged += OnWeaponModelsCellChanged;
            _dgvAmmoReserve.CellValueChanged += OnAmmoReserveCellChanged;

            SetupTooltips();
        }

        private FlowLayoutPanel CreateOptionsPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _chkUseXblaLayout = new CheckBox { Text = "Use XBLA Layout", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            panel.Controls.Add(_chkUseXblaLayout);

            _chkPreserveRamAddrs = new CheckBox { Text = "Preserve XBLA RAM Addrs", Checked = true, AutoSize = true, Margin = new Padding(15, 6, 0, 0) };
            panel.Controls.Add(_chkPreserveRamAddrs);

            return panel;
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_chkUseXblaLayout, TooltipTexts.WeaponStats.UseXblaLayout);
            toolTip.SetToolTip(_chkPreserveRamAddrs, TooltipTexts.WeaponStats.PreserveRamAddrs);
        }

        private static DataGridView CreateDataGridView()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                EditMode = DataGridViewEditMode.EditOnEnter
            };

            // Enable double buffering to prevent flickering during scroll
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                dgv,
                new object[] { true });

            return dgv;
        }

        private void SetupWeaponStatsGrid()
        {
            _dgvWeaponStats.Columns.Clear();

            AddColumn(_dgvWeaponStats, "Index", "#", 35, true, frozen: true);
            AddColumn(_dgvWeaponStats, "WeaponName", "Weapon", 140, true, frozen: true);
            AddColumn(_dgvWeaponStats, "Damage", "Damage", 70);
            AddColumn(_dgvWeaponStats, "MagazineSize", "Mag Size", 65);
            AddColumn(_dgvWeaponStats, "AmmunitionType", "Ammo Type", 70);
            AddColumn(_dgvWeaponStats, "FireAutomatic", "Auto", 45);
            AddColumn(_dgvWeaponStats, "FireSingleShot", "Single", 50);
            AddColumn(_dgvWeaponStats, "Penetration", "Pen", 40);
            AddColumn(_dgvWeaponStats, "Inaccuracy", "Inaccuracy", 70);
            AddColumn(_dgvWeaponStats, "Scope", "Scope", 60);
            AddColumn(_dgvWeaponStats, "CrosshairSpeed", "Crosshair Spd", 85);
            AddColumn(_dgvWeaponStats, "WeaponAimLockOnSpeed", "Lock-On Spd", 85);
            AddColumn(_dgvWeaponStats, "Sway", "Sway", 55);
            AddColumn(_dgvWeaponStats, "RecoilBackward", "Recoil Back", 80);
            AddColumn(_dgvWeaponStats, "RecoilUpward", "Recoil Up", 75);
            AddColumn(_dgvWeaponStats, "RecoilBolt", "Recoil Bolt", 75);
            AddColumn(_dgvWeaponStats, "MuzzleFlashExtension", "Muzzle Flash", 85);
            AddColumn(_dgvWeaponStats, "OnScreenXPosition", "Screen X", 70);
            AddColumn(_dgvWeaponStats, "OnScreenYPosition", "Screen Y", 70);
            AddColumn(_dgvWeaponStats, "OnScreenZPosition", "Screen Z", 70);
            AddColumn(_dgvWeaponStats, "AimUpwardShift", "Aim Up", 60);
            AddColumn(_dgvWeaponStats, "AimDownwardShift", "Aim Down", 65);
            AddColumn(_dgvWeaponStats, "AimLeftRightShift", "Aim L/R", 60);
            AddColumn(_dgvWeaponStats, "SoundEffect", "Sound FX", 65);
            AddColumn(_dgvWeaponStats, "SoundTriggerRate", "Snd Rate", 60);
            AddColumn(_dgvWeaponStats, "VolumeToAISingleShot", "AI Single", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIMultipleShots", "AI Multi", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIActiveFire", "AI Active", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIBaseline1", "AI Base1", 65);
            AddColumn(_dgvWeaponStats, "VolumeToAIBaseline2", "AI Base2", 65);
            AddColumn(_dgvWeaponStats, "ForceOfImpact", "Impact", 60);
            AddColumn(_dgvWeaponStats, "EjectedCasingsRAM", "Casings RAM", 90);
            AddColumn(_dgvWeaponStats, "Flags", "Flags", 80);
        }

        private void SetupWeaponModelsGrid()
        {
            _dgvWeaponModels.Columns.Clear();

            AddColumn(_dgvWeaponModels, "Index", "#", 35, true, frozen: true);
            AddColumn(_dgvWeaponModels, "ModelDetailsRAM", "Model (RAM)", 100);
            AddColumn(_dgvWeaponModels, "GZTextStringRAM", "GZ Text (RAM)", 100);
            AddColumn(_dgvWeaponModels, "HasGZModel", "Has GZ (0=Y)", 80);
            AddColumn(_dgvWeaponModels, "StatisticsRAM", "Stats (RAM)", 100);
            AddColumn(_dgvWeaponModels, "NameUpperWatch", "Upper Watch", 80);
            AddColumn(_dgvWeaponModels, "NameLowerWatch", "Lower Watch", 80);
            AddColumn(_dgvWeaponModels, "WatchEquippedX", "Watch X", 70);
            AddColumn(_dgvWeaponModels, "WatchEquippedY", "Watch Y", 70);
            AddColumn(_dgvWeaponModels, "WatchEquippedZ", "Watch Z", 70);
            AddColumn(_dgvWeaponModels, "XRotation", "Rot X", 60);
            AddColumn(_dgvWeaponModels, "YRotation", "Rot Y", 60);
            AddColumn(_dgvWeaponModels, "NameWeaponOfChoice", "Choice Name", 85);
            AddColumn(_dgvWeaponModels, "NameInventoryList", "Inventory Name", 95);
            AddColumn(_dgvWeaponModels, "InventoryListX", "Inv X", 60);
            AddColumn(_dgvWeaponModels, "InventoryListY", "Inv Y", 60);
            AddColumn(_dgvWeaponModels, "InventoryListZ", "Inv Z", 60);
        }

        private void SetupAmmoReserveGrid()
        {
            _dgvAmmoReserve.Columns.Clear();

            AddColumn(_dgvAmmoReserve, "Index", "#", 35, true, frozen: true);
            AddColumn(_dgvAmmoReserve, "IconOffset", "Icon Offset", 100);
            AddColumn(_dgvAmmoReserve, "MaxReserveCapacity", "Max Reserve", 100);
            AddColumn(_dgvAmmoReserve, "Pointer", "Pointer", 100);
        }

        private static void AddColumn(DataGridView dgv, string name, string header, int width, bool readOnly = false, bool frozen = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly,
                Frozen = frozen
            };
            dgv.Columns.Add(col);
        }

        private void Log(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        #region IXexTab Implementation

        public string TabDisplayName => "Weapon Stats";

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

                _parser = WeaponStatsParser.LoadFromXex(_xexData);

                Log($"Loaded {_parser.WeaponStats.Count} weapon stats entries");
                Log($"Loaded {_parser.WeaponModels.Count} weapon model entries");
                Log($"Loaded {_parser.AmmoReserves.Count} ammo reserve entries");

                PopulateWeaponStatsGrid();
                PopulateWeaponModelsGrid();
                PopulateAmmoReserveGrid();

                _hasUnsavedChanges = false;
                _btnApply.Enabled = true;

                Log("");
                Log("Data loaded from XEX. Edit values directly in the grids.");
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
            _dgvWeaponStats.Rows.Clear();
            _dgvWeaponModels.Rows.Clear();
            _dgvAmmoReserve.Rows.Clear();
            _txtLog.Clear();
            _hasUnsavedChanges = false;
            _btnApply.Enabled = false;
        }

        public byte[]? GetModifiedXexData()
        {
            if (!_hasUnsavedChanges || _parser == null || _xexData == null)
                return null;

            var log = new List<string>();
            _parser.ApplyToXex(_xexData, log);

            _hasUnsavedChanges = false;
            return _xexData;
        }

        #endregion

        #region I21990Tab Implementation

        public void On21990Loaded(byte[] data, string path)
        {
            _21990Data = data;
            _21990Path = path;
            Log($"21990 file loaded: {Path.GetFileName(path)} ({data.Length:N0} bytes)");

            // Auto-apply weapon stats if XEX is already loaded
            if (_xexData != null)
            {
                AutoApplyWeaponStats();
            }
        }

        public void On21990Unloaded()
        {
            _21990Data = null;
            _21990Path = null;
        }

        #endregion

        private void AutoApplyWeaponStats()
        {
            if (_21990Data == null || _xexData == null)
                return;

            try
            {
                bool useXblaLayout = _chkUseXblaLayout.Checked;
                Log("");
                Log($"Auto-applying weapon stats from 21990: {_21990Path}");
                Log($"Using layout: {(useXblaLayout ? "XBLA" : "N64")}");

                var data21990 = _21990Data;
                Log($"21990 size: {data21990.Length:N0} bytes");

                if (useXblaLayout)
                {
                    _parser = WeaponStatsParser.LoadFrom21990WithXblaLayout(data21990);
                }
                else
                {
                    _parser = WeaponStatsParser.LoadFrom21990(data21990);
                }

                Log($"Imported {_parser.WeaponStats.Count} weapon stats entries");
                Log($"Imported {_parser.WeaponModels.Count} weapon model entries");
                Log($"Imported {_parser.AmmoReserves.Count} ammo reserve entries");

                if (_chkPreserveRamAddrs.Checked && _xexData != null)
                {
                    Log("");
                    Log("Preserving XBLA RAM addresses from loaded XEX...");
                    var xblaParser = WeaponStatsParser.LoadFromXex(_xexData);
                    for (int i = 0; i < _parser.WeaponStats.Count && i < xblaParser.WeaponStats.Count; i++)
                    {
                        _parser.WeaponStats[i].EjectedCasingsRAM = xblaParser.WeaponStats[i].EjectedCasingsRAM;
                    }
                    for (int i = 0; i < _parser.WeaponModels.Count && i < xblaParser.WeaponModels.Count; i++)
                    {
                        _parser.WeaponModels[i].ModelDetailsRAM = xblaParser.WeaponModels[i].ModelDetailsRAM;
                        _parser.WeaponModels[i].GZTextStringRAM = xblaParser.WeaponModels[i].GZTextStringRAM;
                        _parser.WeaponModels[i].StatisticsRAM = xblaParser.WeaponModels[i].StatisticsRAM;
                    }
                    for (int i = 0; i < _parser.AmmoReserves.Count && i < xblaParser.AmmoReserves.Count; i++)
                    {
                        _parser.AmmoReserves[i].Pointer = xblaParser.AmmoReserves[i].Pointer;
                    }
                    Log("RAM addresses preserved from XBLA.");
                }

                PopulateWeaponStatsGrid();
                PopulateWeaponModelsGrid();
                PopulateAmmoReserveGrid();

                _hasUnsavedChanges = true;

                if (_xexData != null)
                {
                    Log("");
                    Log("N64 data imported and converted to XBLA format.");
                    NotifyXexModified();
                }
                else
                {
                    Log("");
                    Log("N64 data imported and converted. Load an XEX file first to apply changes.");
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(FindForm(), ex.ToString(), "Import Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateWeaponStatsGrid()
        {
            _dgvWeaponStats.Rows.Clear();
            if (_parser == null) return;

            foreach (var entry in _parser.WeaponStats)
            {
                int rowIdx = _dgvWeaponStats.Rows.Add();
                var row = _dgvWeaponStats.Rows[rowIdx];
                row.Tag = entry;

                row.Cells["Index"].Value = entry.Index.ToString();
                row.Cells["WeaponName"].Value = entry.WeaponName;
                row.Cells["Damage"].Value = entry.Damage.ToString("F4");
                row.Cells["MagazineSize"].Value = entry.MagazineSize.ToString();
                row.Cells["AmmunitionType"].Value = entry.AmmunitionType.ToString();
                row.Cells["FireAutomatic"].Value = $"0x{entry.FireAutomatic:X2}";
                row.Cells["FireSingleShot"].Value = $"0x{entry.FireSingleShot:X2}";
                row.Cells["Penetration"].Value = entry.Penetration.ToString();
                row.Cells["Inaccuracy"].Value = entry.Inaccuracy.ToString("F4");
                row.Cells["Scope"].Value = entry.Scope.ToString("F4");
                row.Cells["CrosshairSpeed"].Value = entry.CrosshairSpeed.ToString("F4");
                row.Cells["WeaponAimLockOnSpeed"].Value = entry.WeaponAimLockOnSpeed.ToString("F4");
                row.Cells["Sway"].Value = entry.Sway.ToString("F4");
                row.Cells["RecoilBackward"].Value = entry.RecoilBackward.ToString("F4");
                row.Cells["RecoilUpward"].Value = entry.RecoilUpward.ToString("F4");
                row.Cells["RecoilBolt"].Value = entry.RecoilBolt.ToString("F4");
                row.Cells["MuzzleFlashExtension"].Value = entry.MuzzleFlashExtension.ToString("F4");
                row.Cells["OnScreenXPosition"].Value = entry.OnScreenXPosition.ToString("F4");
                row.Cells["OnScreenYPosition"].Value = entry.OnScreenYPosition.ToString("F4");
                row.Cells["OnScreenZPosition"].Value = entry.OnScreenZPosition.ToString("F4");
                row.Cells["AimUpwardShift"].Value = entry.AimUpwardShift.ToString("F4");
                row.Cells["AimDownwardShift"].Value = entry.AimDownwardShift.ToString("F4");
                row.Cells["AimLeftRightShift"].Value = entry.AimLeftRightShift.ToString("F4");
                row.Cells["SoundEffect"].Value = entry.SoundEffect.ToString();
                row.Cells["SoundTriggerRate"].Value = entry.SoundTriggerRate.ToString();
                row.Cells["VolumeToAISingleShot"].Value = entry.VolumeToAISingleShot.ToString("F4");
                row.Cells["VolumeToAIMultipleShots"].Value = entry.VolumeToAIMultipleShots.ToString("F4");
                row.Cells["VolumeToAIActiveFire"].Value = entry.VolumeToAIActiveFire.ToString("F4");
                row.Cells["VolumeToAIBaseline1"].Value = entry.VolumeToAIBaseline1.ToString("F4");
                row.Cells["VolumeToAIBaseline2"].Value = entry.VolumeToAIBaseline2.ToString("F4");
                row.Cells["ForceOfImpact"].Value = entry.ForceOfImpact.ToString("F4");
                row.Cells["EjectedCasingsRAM"].Value = $"0x{entry.EjectedCasingsRAM:X8}";
                row.Cells["Flags"].Value = $"0x{entry.Flags:X8}";
            }
        }

        private void PopulateWeaponModelsGrid()
        {
            _dgvWeaponModels.Rows.Clear();
            if (_parser == null) return;

            foreach (var entry in _parser.WeaponModels)
            {
                int rowIdx = _dgvWeaponModels.Rows.Add();
                var row = _dgvWeaponModels.Rows[rowIdx];
                row.Tag = entry;

                row.Cells["Index"].Value = entry.Index.ToString();
                row.Cells["ModelDetailsRAM"].Value = $"0x{entry.ModelDetailsRAM:X8}";
                row.Cells["GZTextStringRAM"].Value = $"0x{entry.GZTextStringRAM:X8}";
                row.Cells["HasGZModel"].Value = entry.HasGZModel.ToString();
                row.Cells["StatisticsRAM"].Value = $"0x{entry.StatisticsRAM:X8}";
                row.Cells["NameUpperWatch"].Value = entry.NameUpperWatch.ToString();
                row.Cells["NameLowerWatch"].Value = entry.NameLowerWatch.ToString();
                row.Cells["WatchEquippedX"].Value = entry.WatchEquippedX.ToString("F4");
                row.Cells["WatchEquippedY"].Value = entry.WatchEquippedY.ToString("F4");
                row.Cells["WatchEquippedZ"].Value = entry.WatchEquippedZ.ToString("F4");
                row.Cells["XRotation"].Value = entry.XRotation.ToString("F4");
                row.Cells["YRotation"].Value = entry.YRotation.ToString("F4");
                row.Cells["NameWeaponOfChoice"].Value = entry.NameWeaponOfChoice.ToString();
                row.Cells["NameInventoryList"].Value = entry.NameInventoryList.ToString();
                row.Cells["InventoryListX"].Value = entry.InventoryListX.ToString("F4");
                row.Cells["InventoryListY"].Value = entry.InventoryListY.ToString("F4");
                row.Cells["InventoryListZ"].Value = entry.InventoryListZ.ToString("F4");
            }
        }

        private void PopulateAmmoReserveGrid()
        {
            _dgvAmmoReserve.Rows.Clear();
            if (_parser == null) return;

            foreach (var entry in _parser.AmmoReserves)
            {
                int rowIdx = _dgvAmmoReserve.Rows.Add();
                var row = _dgvAmmoReserve.Rows[rowIdx];
                row.Tag = entry;

                row.Cells["Index"].Value = entry.Index.ToString();
                row.Cells["IconOffset"].Value = entry.IconOffset.ToString("F4");
                row.Cells["MaxReserveCapacity"].Value = entry.MaxReserveCapacity.ToString();
                row.Cells["Pointer"].Value = $"0x{entry.Pointer:X8}";
            }
        }

        private void OnWeaponStatsCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_parser == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = _dgvWeaponStats.Rows[e.RowIndex];
            var entry = row.Tag as WeaponStatsParser.WeaponStatsEntry;
            if (entry == null) return;

            var colName = _dgvWeaponStats.Columns[e.ColumnIndex].Name;
            var value = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";

            try
            {
                switch (colName)
                {
                    case "Damage": if (float.TryParse(value, out var damage)) entry.Damage = damage; break;
                    case "MagazineSize": if (ushort.TryParse(value, out var mag)) entry.MagazineSize = mag; break;
                    case "AmmunitionType": if (ushort.TryParse(value, out var ammoType)) entry.AmmunitionType = ammoType; break;
                    case "FireAutomatic": if (TryParseHexOrDecByte(value, out var fireAuto)) entry.FireAutomatic = fireAuto; break;
                    case "FireSingleShot": if (TryParseHexOrDecByte(value, out var fireSingle)) entry.FireSingleShot = fireSingle; break;
                    case "Penetration": if (byte.TryParse(value, out var pen)) entry.Penetration = pen; break;
                    case "Inaccuracy": if (float.TryParse(value, out var inacc)) entry.Inaccuracy = inacc; break;
                    case "Scope": if (float.TryParse(value, out var scope)) entry.Scope = scope; break;
                    case "CrosshairSpeed": if (float.TryParse(value, out var crossSpd)) entry.CrosshairSpeed = crossSpd; break;
                    case "WeaponAimLockOnSpeed": if (float.TryParse(value, out var lockOn)) entry.WeaponAimLockOnSpeed = lockOn; break;
                    case "Sway": if (float.TryParse(value, out var sway)) entry.Sway = sway; break;
                    case "RecoilBackward": if (float.TryParse(value, out var recoilBack)) entry.RecoilBackward = recoilBack; break;
                    case "RecoilUpward": if (float.TryParse(value, out var recoilUp)) entry.RecoilUpward = recoilUp; break;
                    case "RecoilBolt": if (float.TryParse(value, out var recoilBolt)) entry.RecoilBolt = recoilBolt; break;
                    case "MuzzleFlashExtension": if (float.TryParse(value, out var muzzle)) entry.MuzzleFlashExtension = muzzle; break;
                    case "OnScreenXPosition": if (float.TryParse(value, out var screenX)) entry.OnScreenXPosition = screenX; break;
                    case "OnScreenYPosition": if (float.TryParse(value, out var screenY)) entry.OnScreenYPosition = screenY; break;
                    case "OnScreenZPosition": if (float.TryParse(value, out var screenZ)) entry.OnScreenZPosition = screenZ; break;
                    case "AimUpwardShift": if (float.TryParse(value, out var aimUp)) entry.AimUpwardShift = aimUp; break;
                    case "AimDownwardShift": if (float.TryParse(value, out var aimDown)) entry.AimDownwardShift = aimDown; break;
                    case "AimLeftRightShift": if (float.TryParse(value, out var aimLR)) entry.AimLeftRightShift = aimLR; break;
                    case "SoundEffect": if (ushort.TryParse(value, out var soundFx)) entry.SoundEffect = soundFx; break;
                    case "SoundTriggerRate": if (byte.TryParse(value, out var soundRate)) entry.SoundTriggerRate = soundRate; break;
                    case "VolumeToAISingleShot": if (float.TryParse(value, out var aiSingle)) entry.VolumeToAISingleShot = aiSingle; break;
                    case "VolumeToAIMultipleShots": if (float.TryParse(value, out var aiMulti)) entry.VolumeToAIMultipleShots = aiMulti; break;
                    case "VolumeToAIActiveFire": if (float.TryParse(value, out var aiActive)) entry.VolumeToAIActiveFire = aiActive; break;
                    case "VolumeToAIBaseline1": if (float.TryParse(value, out var aiBase1)) entry.VolumeToAIBaseline1 = aiBase1; break;
                    case "VolumeToAIBaseline2": if (float.TryParse(value, out var aiBase2)) entry.VolumeToAIBaseline2 = aiBase2; break;
                    case "ForceOfImpact": if (float.TryParse(value, out var impact)) entry.ForceOfImpact = impact; break;
                    case "EjectedCasingsRAM": if (TryParseHexOrDec(value, out var casings)) entry.EjectedCasingsRAM = casings; break;
                    case "Flags": if (TryParseHexOrDec(value, out var flags)) entry.Flags = flags; break;
                }

                _hasUnsavedChanges = true;
                NotifyXexModified();
            }
            catch (Exception ex)
            {
                Log($"Error updating weapon stats: {ex.Message}");
            }
        }

        private void OnWeaponModelsCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_parser == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = _dgvWeaponModels.Rows[e.RowIndex];
            var entry = row.Tag as WeaponStatsParser.WeaponModelEntry;
            if (entry == null) return;

            var colName = _dgvWeaponModels.Columns[e.ColumnIndex].Name;
            var value = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";

            try
            {
                switch (colName)
                {
                    case "ModelDetailsRAM": if (TryParseHexOrDec(value, out var model)) entry.ModelDetailsRAM = model; break;
                    case "GZTextStringRAM": if (TryParseHexOrDec(value, out var gzText)) entry.GZTextStringRAM = gzText; break;
                    case "HasGZModel": if (uint.TryParse(value, out var hasGz)) entry.HasGZModel = hasGz; break;
                    case "StatisticsRAM": if (TryParseHexOrDec(value, out var stats)) entry.StatisticsRAM = stats; break;
                    case "NameUpperWatch": if (ushort.TryParse(value, out var upper)) entry.NameUpperWatch = upper; break;
                    case "NameLowerWatch": if (ushort.TryParse(value, out var lower)) entry.NameLowerWatch = lower; break;
                    case "WatchEquippedX": if (float.TryParse(value, out var watchX)) entry.WatchEquippedX = watchX; break;
                    case "WatchEquippedY": if (float.TryParse(value, out var watchY)) entry.WatchEquippedY = watchY; break;
                    case "WatchEquippedZ": if (float.TryParse(value, out var watchZ)) entry.WatchEquippedZ = watchZ; break;
                    case "XRotation": if (float.TryParse(value, out var rotX)) entry.XRotation = rotX; break;
                    case "YRotation": if (float.TryParse(value, out var rotY)) entry.YRotation = rotY; break;
                    case "NameWeaponOfChoice": if (ushort.TryParse(value, out var choice)) entry.NameWeaponOfChoice = choice; break;
                    case "NameInventoryList": if (ushort.TryParse(value, out var inv)) entry.NameInventoryList = inv; break;
                    case "InventoryListX": if (float.TryParse(value, out var invX)) entry.InventoryListX = invX; break;
                    case "InventoryListY": if (float.TryParse(value, out var invY)) entry.InventoryListY = invY; break;
                    case "InventoryListZ": if (float.TryParse(value, out var invZ)) entry.InventoryListZ = invZ; break;
                }

                _hasUnsavedChanges = true;
                NotifyXexModified();
            }
            catch (Exception ex)
            {
                Log($"Error updating weapon model: {ex.Message}");
            }
        }

        private void OnAmmoReserveCellChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_parser == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = _dgvAmmoReserve.Rows[e.RowIndex];
            var entry = row.Tag as WeaponStatsParser.AmmoReserveEntry;
            if (entry == null) return;

            var colName = _dgvAmmoReserve.Columns[e.ColumnIndex].Name;
            var value = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";

            try
            {
                switch (colName)
                {
                    case "IconOffset": if (float.TryParse(value, out var icon)) entry.IconOffset = icon; break;
                    case "MaxReserveCapacity": if (uint.TryParse(value, out var max)) entry.MaxReserveCapacity = max; break;
                    case "Pointer": if (TryParseHexOrDec(value, out var ptr)) entry.Pointer = ptr; break;
                }

                _hasUnsavedChanges = true;
                NotifyXexModified();
            }
            catch (Exception ex)
            {
                Log($"Error updating ammo reserve: {ex.Message}");
            }
        }

        private void ApplyToXex()
        {
            if (_xexData == null || _parser == null)
            {
                MessageBox.Show(FindForm(), "Please load a XEX file first.", "Apply", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Log("");
                Log("=== Applying Weapon Stats to XEX ===");

                var log = new List<string>();
                _parser.ApplyToXex(_xexData, log);
                foreach (var line in log) Log(line);

                _hasUnsavedChanges = true;
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                Log("=== Weapon Stats Applied ===");
                MessageBox.Show(FindForm(), "Weapon stats applied to XEX.\n\nRemember to save from the main toolbar.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(FindForm(), ex.ToString(), "Apply Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void NotifyXexModified()
        {
            if (_xexData != null && _parser != null)
            {
                var log = new List<string>();
                _parser.ApplyToXex(_xexData, log);
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));
            }
        }

        private static bool TryParseHexOrDec(string value, out uint result)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out result);
            }
            return uint.TryParse(value, out result);
        }

        private static bool TryParseHexOrDecByte(string value, out byte result)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out result);
            }
            return byte.TryParse(value, out result);
        }
    }
}
