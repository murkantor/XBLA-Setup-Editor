using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    public sealed class WeaponStatsEditorForm : Form
    {
        // --- UI Controls ---
        private readonly TextBox _txtXexPath;
        private readonly Button _btnLoadXex;
        private readonly Button _btnSaveXex;
        private readonly Button _btnImport21990;
        private readonly CheckBox _chkBackup;
        private readonly CheckBox _chkUseXblaLayout;
        private readonly CheckBox _chkPreserveRamAddrs;
        private readonly TabControl _tabControl;
        private readonly DataGridView _dgvWeaponStats;
        private readonly DataGridView _dgvWeaponModels;
        private readonly DataGridView _dgvAmmoReserve;
        private readonly TextBox _txtLog;

        // --- State ---
        private WeaponStatsParser? _parser;
        private byte[]? _xexData;
        private string? _xexPath;

        public WeaponStatsEditorForm()
        {
            Text = "Weapon Stats Editor";
            Width = 1400;
            Height = 800;
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };

            // Row 0: XEX file path and controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var pathPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            pathPanel.Controls.Add(new Label { Text = "XEX File:", AutoSize = true, Margin = new Padding(0, 6, 5, 0) });
            _txtXexPath = new TextBox { Width = 500 };
            pathPanel.Controls.Add(_txtXexPath);
            var btnBrowse = new Button { Text = "Browse...", Width = 75 };
            pathPanel.Controls.Add(btnBrowse);
            _btnLoadXex = new Button { Text = "Load", Width = 60 };
            pathPanel.Controls.Add(_btnLoadXex);
            _chkBackup = new CheckBox { Text = "Backup", Checked = true, AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
            pathPanel.Controls.Add(_chkBackup);
            _btnSaveXex = new Button { Text = "Save XEX", Width = 75, Enabled = false };
            pathPanel.Controls.Add(_btnSaveXex);
            pathPanel.Controls.Add(new Label { Text = "  |  ", AutoSize = true, Margin = new Padding(5, 6, 5, 0) });
            _btnImport21990 = new Button { Text = "Import 21990...", Width = 100 };
            pathPanel.Controls.Add(_btnImport21990);
            _chkUseXblaLayout = new CheckBox { Text = "Use XBLA Layout", Checked = false, AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
            pathPanel.Controls.Add(_chkUseXblaLayout);
            _chkPreserveRamAddrs = new CheckBox { Text = "Preserve XBLA RAM Addrs", Checked = true, AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
            pathPanel.Controls.Add(_chkPreserveRamAddrs);
            mainLayout.Controls.Add(pathPanel, 0, 0);

            // Row 1: Tab control with data grids
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            // Tab 1: Weapon Stats
            var tabWeaponStats = new TabPage("Weapon Stats");
            _dgvWeaponStats = CreateDataGridView();
            SetupWeaponStatsGrid();
            tabWeaponStats.Controls.Add(_dgvWeaponStats);
            _tabControl.TabPages.Add(tabWeaponStats);

            // Tab 2: Weapon Models
            var tabWeaponModels = new TabPage("Weapon Models");
            _dgvWeaponModels = CreateDataGridView();
            SetupWeaponModelsGrid();
            tabWeaponModels.Controls.Add(_dgvWeaponModels);
            _tabControl.TabPages.Add(tabWeaponModels);

            // Tab 3: Ammo Reserve
            var tabAmmoReserve = new TabPage("Ammo Reserve");
            _dgvAmmoReserve = CreateDataGridView();
            SetupAmmoReserveGrid();
            tabAmmoReserve.Controls.Add(_dgvAmmoReserve);
            _tabControl.TabPages.Add(tabAmmoReserve);

            mainLayout.Controls.Add(_tabControl, 0, 1);

            // Row 2: Log label
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblLog = new Label { Text = "Log:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
            mainLayout.Controls.Add(lblLog, 0, 2);

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
            _btnImport21990.Click += (_, __) => Import21990();
            _dgvWeaponStats.CellValueChanged += OnWeaponStatsCellChanged;
            _dgvWeaponModels.CellValueChanged += OnWeaponModelsCellChanged;
            _dgvAmmoReserve.CellValueChanged += OnAmmoReserveCellChanged;
        }

        private static DataGridView CreateDataGridView()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
        }

        private void SetupWeaponStatsGrid()
        {
            _dgvWeaponStats.Columns.Clear();

            AddColumn(_dgvWeaponStats, "Index", "#", 35, true);
            AddColumn(_dgvWeaponStats, "WeaponName", "Weapon", 140, true);
            // Combat stats (most useful first)
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
            // Recoil
            // NOTE: RecoilSpeed (offset 0x44-0x47) removed from UI - contains timing bytes, not a float!
            // This field controls fire rate and should NOT be editable in UI
            // The bytes are automatically preserved during import/export
            AddColumn(_dgvWeaponStats, "RecoilBackward", "Recoil Back", 80);
            AddColumn(_dgvWeaponStats, "RecoilUpward", "Recoil Up", 75);
            AddColumn(_dgvWeaponStats, "RecoilBolt", "Recoil Bolt", 75);
            // Position/Visual
            AddColumn(_dgvWeaponStats, "MuzzleFlashExtension", "Muzzle Flash", 85);
            AddColumn(_dgvWeaponStats, "OnScreenXPosition", "Screen X", 70);
            AddColumn(_dgvWeaponStats, "OnScreenYPosition", "Screen Y", 70);
            AddColumn(_dgvWeaponStats, "OnScreenZPosition", "Screen Z", 70);
            AddColumn(_dgvWeaponStats, "AimUpwardShift", "Aim Up", 60);
            AddColumn(_dgvWeaponStats, "AimDownwardShift", "Aim Down", 65);
            AddColumn(_dgvWeaponStats, "AimLeftRightShift", "Aim L/R", 60);
            // Sound/AI
            AddColumn(_dgvWeaponStats, "SoundEffect", "Sound FX", 65);
            AddColumn(_dgvWeaponStats, "SoundTriggerRate", "Snd Rate", 60);
            AddColumn(_dgvWeaponStats, "VolumeToAISingleShot", "AI Single", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIMultipleShots", "AI Multi", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIActiveFire", "AI Active", 70);
            AddColumn(_dgvWeaponStats, "VolumeToAIBaseline1", "AI Base1", 65);
            AddColumn(_dgvWeaponStats, "VolumeToAIBaseline2", "AI Base2", 65);
            // Misc
            AddColumn(_dgvWeaponStats, "ForceOfImpact", "Impact", 60);
            AddColumn(_dgvWeaponStats, "EjectedCasingsRAM", "Casings RAM", 90);
            AddColumn(_dgvWeaponStats, "Flags", "Flags", 80);
        }

        private void SetupWeaponModelsGrid()
        {
            _dgvWeaponModels.Columns.Clear();

            AddColumn(_dgvWeaponModels, "Index", "#", 35, true);
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

            AddColumn(_dgvAmmoReserve, "Index", "#", 35, true);
            AddColumn(_dgvAmmoReserve, "IconOffset", "Icon Offset", 100);
            AddColumn(_dgvAmmoReserve, "MaxReserveCapacity", "Max Reserve", 100);
            AddColumn(_dgvAmmoReserve, "Pointer", "Pointer", 100);
        }

        private static void AddColumn(DataGridView dgv, string name, string header, int width, bool readOnly = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly
            };
            dgv.Columns.Add(col);
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

                _parser = WeaponStatsParser.LoadFromXex(_xexData);

                Log($"Loaded {_parser.WeaponStats.Count} weapon stats entries");
                Log($"Loaded {_parser.WeaponModels.Count} weapon model entries");
                Log($"Loaded {_parser.AmmoReserves.Count} ammo reserve entries");

                PopulateWeaponStatsGrid();
                PopulateWeaponModelsGrid();
                PopulateAmmoReserveGrid();

                _btnSaveXex.Enabled = true;

                Log("");
                Log("Data loaded from XEX. Edit values directly in the grids.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                MessageBox.Show(this, ex.ToString(), "Load Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Import21990()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select 21990 file to import",
                Filter = "21990 files (*.bin)|*.bin|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                bool useXblaLayout = _chkUseXblaLayout.Checked;
                Log("");
                Log($"Importing 21990: {ofd.FileName}");
                Log($"Using layout: {(useXblaLayout ? "XBLA" : "N64")}");

                var data21990 = File.ReadAllBytes(ofd.FileName);
                Log($"21990 size: {data21990.Length:N0} bytes");

                // Parse 21990 with selected layout
                // When "Use XBLA Layout" is checked, the 21990 file has XBLA field positions
                // When unchecked, it has N64 field positions and needs conversion
                if (useXblaLayout)
                {
                    Log("  -> Using XBLA field layout parser (FromBytes)");
                    _parser = WeaponStatsParser.LoadFrom21990WithXblaLayout(data21990);
                }
                else
                {
                    Log("  -> Using N64 field layout parser (FromN64Bytes) with conversion");
                    _parser = WeaponStatsParser.LoadFrom21990(data21990);
                }

                Log($"Imported {_parser.WeaponStats.Count} weapon stats entries {(useXblaLayout ? "(XBLA layout)" : "(N64 -> XBLA converted)")}");
                Log($"Imported {_parser.WeaponModels.Count} weapon model entries");
                Log($"Imported {_parser.AmmoReserves.Count} ammo reserve entries");

                // Preserve XBLA RAM addresses if requested and XEX is loaded
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

                // Debug: Show comparison of imported vs original XEX values
                if (_parser.WeaponStats.Count > 0 && _xexData != null)
                {
                    var xblaOriginal = WeaponStatsParser.LoadFromXex(_xexData);

                    // Compare first few weapons (index 4 = PP7, more interesting than index 0)
                    int[] indicesToShow = { 0, 4, 5 };
                    foreach (int idx in indicesToShow)
                    {
                        if (idx >= _parser.WeaponStats.Count || idx >= xblaOriginal.WeaponStats.Count)
                            continue;

                        var imp = _parser.WeaponStats[idx];
                        var orig = xblaOriginal.WeaponStats[idx];

                        Log("");
                        Log($"=== Weapon {idx}: {imp.WeaponName} - IMPORTED vs ORIGINAL XEX ===");
                        Log($"  MuzzleFlashExt:  {imp.MuzzleFlashExtension,10:F4} vs {orig.MuzzleFlashExtension,10:F4}  (0x00)");
                        Log($"  OnScreenX:       {imp.OnScreenXPosition,10:F4} vs {orig.OnScreenXPosition,10:F4}  (0x04)");
                        Log($"  OnScreenY:       {imp.OnScreenYPosition,10:F4} vs {orig.OnScreenYPosition,10:F4}  (0x08)");
                        Log($"  OnScreenZ:       {imp.OnScreenZPosition,10:F4} vs {orig.OnScreenZPosition,10:F4}  (0x0C)");
                        Log($"  AimUpward:       {imp.AimUpwardShift,10:F4} vs {orig.AimUpwardShift,10:F4}  (0x10)");
                        Log($"  AimDownward:     {imp.AimDownwardShift,10:F4} vs {orig.AimDownwardShift,10:F4}  (0x14)");
                        Log($"  AimLeftRight:    {imp.AimLeftRightShift,10:F4} vs {orig.AimLeftRightShift,10:F4}  (0x18)");
                        Log($"  AmmoType:        {imp.AmmunitionType,10} vs {orig.AmmunitionType,10}  (0x1E)");
                        Log($"  MagSize:         {imp.MagazineSize,10} vs {orig.MagazineSize,10}  (0x20)");
                        Log($"  FireAuto:           0x{imp.FireAutomatic:X2} vs        0x{orig.FireAutomatic:X2}  (0x22)");
                        Log($"  FireSingle:         0x{imp.FireSingleShot:X2} vs        0x{orig.FireSingleShot:X2}  (0x23)");
                        Log($"  Penetration:     {imp.Penetration,10} vs {orig.Penetration,10}  (0x24)");
                        Log($"  SoundTrigRate:   {imp.SoundTriggerRate,10} vs {orig.SoundTriggerRate,10}  (0x25)");
                        Log($"  SoundEffect:     {imp.SoundEffect,10} vs {orig.SoundEffect,10}  (0x26)");
                        Log($"  EjectedCasings:  0x{imp.EjectedCasingsRAM:X8} vs 0x{orig.EjectedCasingsRAM:X8}  (0x28)");
                        Log($"  Damage:          {imp.Damage,10:F4} vs {orig.Damage,10:F4}  (0x2C)");
                        Log($"  Inaccuracy:      {imp.Inaccuracy,10:F4} vs {orig.Inaccuracy,10:F4}  (0x30)");
                        Log($"  Scope:           {imp.Scope,10:F4} vs {orig.Scope,10:F4}  (0x34)");
                        Log($"  CrosshairSpd:    {imp.CrosshairSpeed,10:F4} vs {orig.CrosshairSpeed,10:F4}  (0x38)");
                        Log($"  AimLockOnSpd:    {imp.WeaponAimLockOnSpeed,10:F4} vs {orig.WeaponAimLockOnSpeed,10:F4}  (0x3C)");
                        Log($"  Sway:            {imp.Sway,10:F4} vs {orig.Sway,10:F4}  (0x40)");
                        // Show RecoilSpeed bytes in hex for debugging
                        Log($"  RecoilSpeedBytes: [{imp.RecoilSpeedByte0:X2} {imp.RecoilSpeedByte1:X2} {imp.RecoilSpeedByte2:X2} {imp.RecoilSpeedByte3:X2}] vs [{orig.RecoilSpeedByte0:X2} {orig.RecoilSpeedByte1:X2} {orig.RecoilSpeedByte2:X2} {orig.RecoilSpeedByte3:X2}]  (0x44)");
                        Log($"  RecoilBackward:  {imp.RecoilBackward,10:F4} vs {orig.RecoilBackward,10:F4}  (0x48)");
                        Log($"  RecoilUpward:    {imp.RecoilUpward,10:F4} vs {orig.RecoilUpward,10:F4}  (0x4C)");
                        Log($"  RecoilBolt:      {imp.RecoilBolt,10:F4} vs {orig.RecoilBolt,10:F4}  (0x50)");
                        Log($"  Flags:           0x{imp.Flags:X8} vs 0x{orig.Flags:X8}  (0x6C)");
                    }
                }

                PopulateWeaponStatsGrid();
                PopulateWeaponModelsGrid();
                PopulateAmmoReserveGrid();

                // Enable save only if XEX is also loaded
                if (_xexData != null)
                {
                    _btnSaveXex.Enabled = true;
                    Log("");
                    Log("N64 data imported and converted to XBLA format. You can now save to XEX.");
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
                MessageBox.Show(this, ex.ToString(), "Import Failed",
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
                // Combat stats
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
                // Recoil - NOTE: RecoilSpeed column removed, bytes preserved automatically
                row.Cells["RecoilBackward"].Value = entry.RecoilBackward.ToString("F4");
                row.Cells["RecoilUpward"].Value = entry.RecoilUpward.ToString("F4");
                row.Cells["RecoilBolt"].Value = entry.RecoilBolt.ToString("F4");
                // Position/Visual
                row.Cells["MuzzleFlashExtension"].Value = entry.MuzzleFlashExtension.ToString("F4");
                row.Cells["OnScreenXPosition"].Value = entry.OnScreenXPosition.ToString("F4");
                row.Cells["OnScreenYPosition"].Value = entry.OnScreenYPosition.ToString("F4");
                row.Cells["OnScreenZPosition"].Value = entry.OnScreenZPosition.ToString("F4");
                row.Cells["AimUpwardShift"].Value = entry.AimUpwardShift.ToString("F4");
                row.Cells["AimDownwardShift"].Value = entry.AimDownwardShift.ToString("F4");
                row.Cells["AimLeftRightShift"].Value = entry.AimLeftRightShift.ToString("F4");
                // Sound/AI
                row.Cells["SoundEffect"].Value = entry.SoundEffect.ToString();
                row.Cells["SoundTriggerRate"].Value = entry.SoundTriggerRate.ToString();
                row.Cells["VolumeToAISingleShot"].Value = entry.VolumeToAISingleShot.ToString("F4");
                row.Cells["VolumeToAIMultipleShots"].Value = entry.VolumeToAIMultipleShots.ToString("F4");
                row.Cells["VolumeToAIActiveFire"].Value = entry.VolumeToAIActiveFire.ToString("F4");
                row.Cells["VolumeToAIBaseline1"].Value = entry.VolumeToAIBaseline1.ToString("F4");
                row.Cells["VolumeToAIBaseline2"].Value = entry.VolumeToAIBaseline2.ToString("F4");
                // Misc
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
                    // Combat stats
                    case "Damage":
                        if (float.TryParse(value, out var damage)) entry.Damage = damage;
                        break;
                    case "MagazineSize":
                        if (ushort.TryParse(value, out var mag)) entry.MagazineSize = mag;
                        break;
                    case "AmmunitionType":
                        if (ushort.TryParse(value, out var ammoType)) entry.AmmunitionType = ammoType;
                        break;
                    case "FireAutomatic":
                        if (TryParseHexOrDecByte(value, out var fireAuto)) entry.FireAutomatic = fireAuto;
                        break;
                    case "FireSingleShot":
                        if (TryParseHexOrDecByte(value, out var fireSingle)) entry.FireSingleShot = fireSingle;
                        break;
                    case "Penetration":
                        if (byte.TryParse(value, out var pen)) entry.Penetration = pen;
                        break;
                    case "Inaccuracy":
                        if (float.TryParse(value, out var inacc)) entry.Inaccuracy = inacc;
                        break;
                    case "Scope":
                        if (float.TryParse(value, out var scope)) entry.Scope = scope;
                        break;
                    case "CrosshairSpeed":
                        if (float.TryParse(value, out var crossSpd)) entry.CrosshairSpeed = crossSpd;
                        break;
                    case "WeaponAimLockOnSpeed":
                        if (float.TryParse(value, out var lockOn)) entry.WeaponAimLockOnSpeed = lockOn;
                        break;
                    case "Sway":
                        if (float.TryParse(value, out var sway)) entry.Sway = sway;
                        break;
                    // Recoil - NOTE: RecoilSpeed case removed, timing bytes preserved automatically
                    case "RecoilBackward":
                        if (float.TryParse(value, out var recoilBack)) entry.RecoilBackward = recoilBack;
                        break;
                    case "RecoilUpward":
                        if (float.TryParse(value, out var recoilUp)) entry.RecoilUpward = recoilUp;
                        break;
                    case "RecoilBolt":
                        if (float.TryParse(value, out var recoilBolt)) entry.RecoilBolt = recoilBolt;
                        break;
                    // Position/Visual
                    case "MuzzleFlashExtension":
                        if (float.TryParse(value, out var muzzle)) entry.MuzzleFlashExtension = muzzle;
                        break;
                    case "OnScreenXPosition":
                        if (float.TryParse(value, out var screenX)) entry.OnScreenXPosition = screenX;
                        break;
                    case "OnScreenYPosition":
                        if (float.TryParse(value, out var screenY)) entry.OnScreenYPosition = screenY;
                        break;
                    case "OnScreenZPosition":
                        if (float.TryParse(value, out var screenZ)) entry.OnScreenZPosition = screenZ;
                        break;
                    case "AimUpwardShift":
                        if (float.TryParse(value, out var aimUp)) entry.AimUpwardShift = aimUp;
                        break;
                    case "AimDownwardShift":
                        if (float.TryParse(value, out var aimDown)) entry.AimDownwardShift = aimDown;
                        break;
                    case "AimLeftRightShift":
                        if (float.TryParse(value, out var aimLR)) entry.AimLeftRightShift = aimLR;
                        break;
                    // Sound/AI
                    case "SoundEffect":
                        if (ushort.TryParse(value, out var soundFx)) entry.SoundEffect = soundFx;
                        break;
                    case "SoundTriggerRate":
                        if (byte.TryParse(value, out var soundRate)) entry.SoundTriggerRate = soundRate;
                        break;
                    case "VolumeToAISingleShot":
                        if (float.TryParse(value, out var aiSingle)) entry.VolumeToAISingleShot = aiSingle;
                        break;
                    case "VolumeToAIMultipleShots":
                        if (float.TryParse(value, out var aiMulti)) entry.VolumeToAIMultipleShots = aiMulti;
                        break;
                    case "VolumeToAIActiveFire":
                        if (float.TryParse(value, out var aiActive)) entry.VolumeToAIActiveFire = aiActive;
                        break;
                    case "VolumeToAIBaseline1":
                        if (float.TryParse(value, out var aiBase1)) entry.VolumeToAIBaseline1 = aiBase1;
                        break;
                    case "VolumeToAIBaseline2":
                        if (float.TryParse(value, out var aiBase2)) entry.VolumeToAIBaseline2 = aiBase2;
                        break;
                    // Misc
                    case "ForceOfImpact":
                        if (float.TryParse(value, out var impact)) entry.ForceOfImpact = impact;
                        break;
                    case "EjectedCasingsRAM":
                        if (TryParseHexOrDec(value, out var casings)) entry.EjectedCasingsRAM = casings;
                        break;
                    case "Flags":
                        if (TryParseHexOrDec(value, out var flags)) entry.Flags = flags;
                        break;
                }
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
                    case "ModelDetailsRAM":
                        if (TryParseHexOrDec(value, out var model)) entry.ModelDetailsRAM = model;
                        break;
                    case "GZTextStringRAM":
                        if (TryParseHexOrDec(value, out var gzText)) entry.GZTextStringRAM = gzText;
                        break;
                    case "HasGZModel":
                        if (uint.TryParse(value, out var hasGz)) entry.HasGZModel = hasGz;
                        break;
                    case "StatisticsRAM":
                        if (TryParseHexOrDec(value, out var stats)) entry.StatisticsRAM = stats;
                        break;
                    case "NameUpperWatch":
                        if (ushort.TryParse(value, out var upper)) entry.NameUpperWatch = upper;
                        break;
                    case "NameLowerWatch":
                        if (ushort.TryParse(value, out var lower)) entry.NameLowerWatch = lower;
                        break;
                    case "WatchEquippedX":
                        if (float.TryParse(value, out var watchX)) entry.WatchEquippedX = watchX;
                        break;
                    case "WatchEquippedY":
                        if (float.TryParse(value, out var watchY)) entry.WatchEquippedY = watchY;
                        break;
                    case "WatchEquippedZ":
                        if (float.TryParse(value, out var watchZ)) entry.WatchEquippedZ = watchZ;
                        break;
                    case "XRotation":
                        if (float.TryParse(value, out var rotX)) entry.XRotation = rotX;
                        break;
                    case "YRotation":
                        if (float.TryParse(value, out var rotY)) entry.YRotation = rotY;
                        break;
                    case "NameWeaponOfChoice":
                        if (ushort.TryParse(value, out var choice)) entry.NameWeaponOfChoice = choice;
                        break;
                    case "NameInventoryList":
                        if (ushort.TryParse(value, out var inv)) entry.NameInventoryList = inv;
                        break;
                    case "InventoryListX":
                        if (float.TryParse(value, out var invX)) entry.InventoryListX = invX;
                        break;
                    case "InventoryListY":
                        if (float.TryParse(value, out var invY)) entry.InventoryListY = invY;
                        break;
                    case "InventoryListZ":
                        if (float.TryParse(value, out var invZ)) entry.InventoryListZ = invZ;
                        break;
                }
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
                    case "IconOffset":
                        if (float.TryParse(value, out var icon)) entry.IconOffset = icon;
                        break;
                    case "MaxReserveCapacity":
                        if (uint.TryParse(value, out var max)) entry.MaxReserveCapacity = max;
                        break;
                    case "Pointer":
                        if (TryParseHexOrDec(value, out var ptr)) entry.Pointer = ptr;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating ammo reserve: {ex.Message}");
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

        private static bool TryParseHexOrDecU16(string value, out ushort result)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out result);
            }
            return ushort.TryParse(value, out result);
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
    }
}