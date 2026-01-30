using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Controls;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Main application window with tabbed interface and shared XEX/21990 state.
    /// </summary>
    public sealed class MainForm : Form
    {
        // XEX State
        private byte[]? _sharedXexData;
        private string? _sharedXexPath;
        private bool _isModified;

        // 21990 State
        private byte[]? _shared21990Data;
        private string? _shared21990Path;

        // UI Controls
        private TextBox _txtXexPath = null!;
        private Button _btnBrowseXex = null!;
        private Button _btnLoadXex = null!;
        private Button _btnSave = null!;
        private TextBox _txt21990Path = null!;
        private Button _btnBrowse21990 = null!;
        private Button _btnLoad21990 = null!;
        private readonly Label _lblStatus;
        private readonly TabControl _tabControl;

        // Tab controls
        private readonly StrEditorControl _strEditorControl;
        private readonly SetupPatchingControl _setupPatchingControl;
        private readonly File21990ImporterControl _file21990Control;
        private readonly XexExtenderControl _xexExtenderControl;
        private readonly MPWeaponSetControl _mpWeaponSetControl;
        private readonly WeaponStatsControl _weaponStatsControl;

        // Events
        public event EventHandler<XexLoadedEventArgs>? XexLoaded;
        public event EventHandler<File21990LoadedEventArgs>? File21990Loaded;

        public MainForm()
        {
            Text = "XBLA Setup Editor";
            Width = 1400;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };

            // Row 0: XEX file controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var xexPanel = CreateXexPanel();
            mainLayout.Controls.Add(xexPanel, 0, 0);

            // Row 1: 21990 file controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var panel21990 = Create21990Panel();
            mainLayout.Controls.Add(panel21990, 0, 1);

            // Row 2: Status bar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "No XEX loaded",
                ForeColor = Color.Gray
            };
            mainLayout.Controls.Add(_lblStatus, 0, 2);

            // Row 3: Tab control
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            // Create tab controls
            _strEditorControl = new StrEditorControl();
            _setupPatchingControl = new SetupPatchingControl();
            _file21990Control = new File21990ImporterControl();
            _xexExtenderControl = new XexExtenderControl();
            _mpWeaponSetControl = new MPWeaponSetControl();
            _weaponStatsControl = new WeaponStatsControl();

            // Create tabs
            AddTab("STR Editor", _strEditorControl);
            AddTab("MP Weapon Sets", _mpWeaponSetControl);
            AddTab("Setup Patching", _setupPatchingControl);
            AddTab("Skies, Fog and Music", _file21990Control);
            AddTab("Weapon Stats", _weaponStatsControl);
            AddTab("XEX Extender", _xexExtenderControl);

            mainLayout.Controls.Add(_tabControl, 0, 3);

            Controls.Add(mainLayout);

            // Wire up XEX and 21990 tab events
            WireUpXexTabs();
            WireUp21990Tabs();

            // Setup tooltips
            SetupTooltips();
        }

        private FlowLayoutPanel CreateXexPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            panel.Controls.Add(new Label
            {
                Text = "XEX File:",
                AutoSize = true,
                Margin = new Padding(0, 8, 5, 0)
            });

            _txtXexPath = new TextBox { Width = 500 };
            panel.Controls.Add(_txtXexPath);

            _btnBrowseXex = new Button { Text = "Browse...", Width = 75 };
            _btnBrowseXex.Click += BtnBrowseXex_Click;
            panel.Controls.Add(_btnBrowseXex);

            _btnLoadXex = new Button { Text = "Load", Width = 60 };
            _btnLoadXex.Click += BtnLoadXex_Click;
            panel.Controls.Add(_btnLoadXex);

            panel.Controls.Add(new Label { Text = "  ", AutoSize = true, Margin = new Padding(10, 0, 10, 0) });

            _btnSave = new Button { Text = "Save As...", Width = 80, Enabled = false };
            _btnSave.Click += BtnSave_Click;
            panel.Controls.Add(_btnSave);

            return panel;
        }

        private FlowLayoutPanel Create21990Panel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            panel.Controls.Add(new Label
            {
                Text = "21990 File:",
                AutoSize = true,
                Margin = new Padding(0, 8, 5, 0)
            });

            _txt21990Path = new TextBox { Width = 500 };
            panel.Controls.Add(_txt21990Path);

            _btnBrowse21990 = new Button { Text = "Browse...", Width = 75 };
            _btnBrowse21990.Click += BtnBrowse21990_Click;
            panel.Controls.Add(_btnBrowse21990);

            _btnLoad21990 = new Button { Text = "Load", Width = 60 };
            _btnLoad21990.Click += BtnLoad21990_Click;
            panel.Controls.Add(_btnLoad21990);

            return panel;
        }

        private void AddTab(string title, UserControl control)
        {
            var tabPage = new TabPage(title);
            control.Dock = DockStyle.Fill;
            tabPage.Controls.Add(control);
            _tabControl.TabPages.Add(tabPage);
        }

        private void WireUpXexTabs()
        {
            // Subscribe XEX tabs to load/unload events
            var xexTabs = new IXexTab[]
            {
                _setupPatchingControl,
                _file21990Control,
                _xexExtenderControl,
                _mpWeaponSetControl,
                _weaponStatsControl
            };

            foreach (var tab in xexTabs)
            {
                XexLoaded += (_, e) => tab.OnXexLoaded(e.XexData, e.Path);
            }

            // Subscribe to modification events from tabs
            _setupPatchingControl.XexModified += OnTabXexModified;
            _file21990Control.XexModified += OnTabXexModified;
            _xexExtenderControl.XexModified += OnTabXexModified;
            _mpWeaponSetControl.XexModified += OnTabXexModified;
            _weaponStatsControl.XexModified += OnTabXexModified;
        }

        private void WireUp21990Tabs()
        {
            // Subscribe 21990 tabs to load events
            var tabs21990 = new I21990Tab[]
            {
                _file21990Control,
                _weaponStatsControl
            };

            foreach (var tab in tabs21990)
            {
                File21990Loaded += (_, e) => tab.On21990Loaded(e.Data, e.Path);
            }
        }

        private void SetupTooltips()
        {
            var toolTip = new ToolTip { AutoPopDelay = 10000 };

            toolTip.SetToolTip(_txtXexPath, TooltipTexts.MainForm.XexFilePath);
            toolTip.SetToolTip(_btnBrowseXex, TooltipTexts.MainForm.BrowseXex);
            toolTip.SetToolTip(_btnLoadXex, TooltipTexts.MainForm.LoadXex);
            toolTip.SetToolTip(_btnSave, TooltipTexts.MainForm.SaveXex);
            toolTip.SetToolTip(_txt21990Path, TooltipTexts.MainForm.File21990Path);
            toolTip.SetToolTip(_btnBrowse21990, TooltipTexts.MainForm.Browse21990);
            toolTip.SetToolTip(_btnLoad21990, TooltipTexts.MainForm.Load21990);
            toolTip.SetToolTip(_lblStatus, TooltipTexts.MainForm.Status);
        }

        private void BtnBrowseXex_Click(object? sender, EventArgs e)
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
        }

        private void BtnBrowse21990_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select 21990 file",
                Filter = "21990 files (*.bin;21990)|*.bin;21990|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _txt21990Path.Text = ofd.FileName;
            }
        }

        private void BtnLoad21990_Click(object? sender, EventArgs e)
        {
            var path = _txt21990Path.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "Please enter or browse for a 21990 file path.",
                    "Load 21990", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(this, "21990 file not found.",
                    "Load 21990", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _shared21990Data = File.ReadAllBytes(path);
                _shared21990Path = path;

                UpdateStatus($"21990 loaded: {Path.GetFileName(path)} ({_shared21990Data.Length:N0} bytes)");

                // Notify all 21990 tabs
                File21990Loaded?.Invoke(this, new File21990LoadedEventArgs(_shared21990Data, _shared21990Path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load 21990:\n{ex.Message}",
                    "Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLoadXex_Click(object? sender, EventArgs e)
        {
            var path = _txtXexPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "Please enter or browse for a XEX file path.",
                    "Load XEX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(this, "XEX file not found.",
                    "Load XEX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Check for unsaved changes
                if (_isModified)
                {
                    var result = MessageBox.Show(this,
                        "You have unsaved changes. Load a new XEX anyway?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                        return;
                }

                _sharedXexData = File.ReadAllBytes(path);
                _sharedXexPath = path;
                _isModified = false;

                UpdateStatus($"Loaded: {Path.GetFileName(path)} ({_sharedXexData.Length:N0} bytes)");
                _btnSave.Enabled = true;

                // Notify all tabs
                XexLoaded?.Invoke(this, new XexLoadedEventArgs(_sharedXexData, _sharedXexPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load XEX:\n{ex.Message}",
                    "Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_sharedXexData == null || string.IsNullOrWhiteSpace(_sharedXexPath))
            {
                MessageBox.Show(this, "No XEX loaded.",
                    "Save XEX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Always use Save As - never overwrite the original
            var dir = Path.GetDirectoryName(_sharedXexPath) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(_sharedXexPath);
            var ext = Path.GetExtension(_sharedXexPath);
            var suggestedName = $"{baseName}_modified{ext}";

            using var sfd = new SaveFileDialog
            {
                Title = "Save XEX As",
                Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*",
                InitialDirectory = dir,
                FileName = suggestedName
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                // Collect modifications from all tabs
                CollectTabModifications();

                // Save the XEX to the new location
                File.WriteAllBytes(sfd.FileName, _sharedXexData);

                _isModified = false;
                UpdateStatus($"Saved: {Path.GetFileName(sfd.FileName)}");

                MessageBox.Show(this, $"XEX saved successfully!\n\n{sfd.FileName}",
                    "Save XEX", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save XEX:\n{ex.Message}",
                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CollectTabModifications()
        {
            // Get modifications from each XEX tab and apply them
            var xexTabs = new IXexTab[]
            {
                _setupPatchingControl,
                _file21990Control,
                _xexExtenderControl,
                _mpWeaponSetControl,
                _weaponStatsControl
            };

            foreach (var tab in xexTabs)
            {
                var modifiedData = tab.GetModifiedXexData();
                if (modifiedData != null)
                {
                    _sharedXexData = modifiedData;
                }
            }
        }

        private void OnTabXexModified(object? sender, XexModifiedEventArgs e)
        {
            _sharedXexData = e.ModifiedData;
            _isModified = true;
            UpdateStatus($"Modified by {e.Source} (unsaved)");

            // Re-broadcast the modified XEX data to all other tabs using lightweight update
            // This ensures tabs like SetupPatchingControl have the latest data
            // (including 21990 patches) when doing operations like split XEX
            var xexTabs = new IXexTab[]
            {
                _setupPatchingControl,
                _file21990Control,
                _xexExtenderControl,
                _mpWeaponSetControl,
                _weaponStatsControl
            };

            foreach (var tab in xexTabs)
            {
                // Don't re-notify the tab that made the modification
                if (!ReferenceEquals(tab, sender))
                {
                    tab.OnXexDataUpdated(_sharedXexData);
                }
            }
        }

        /// <summary>
        /// Apply XEX changes from a specific source tab.
        /// Call this from tabs when they make modifications.
        /// </summary>
        public void ApplyXexChanges(byte[] modifiedData, string source)
        {
            _sharedXexData = modifiedData;
            _isModified = true;
            UpdateStatus($"Modified by {source} (unsaved)");
        }

        /// <summary>
        /// Gets the currently loaded XEX data.
        /// </summary>
        public byte[]? GetXexData() => _sharedXexData;

        /// <summary>
        /// Gets the path of the currently loaded XEX.
        /// </summary>
        public string? GetXexPath() => _sharedXexPath;

        private void UpdateStatus(string message)
        {
            _lblStatus.Text = message;
            _lblStatus.ForeColor = _isModified ? Color.DarkOrange : Color.Black;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isModified)
            {
                var result = MessageBox.Show(this,
                    "You have unsaved changes. Exit anyway?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnFormClosing(e);
        }
    }
}
