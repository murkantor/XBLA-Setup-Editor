// =============================================================================
// MainForm.cs - Main Application Window
// =============================================================================
// This is the central hub of the XBLA Setup Editor. It manages:
//   1. Shared XEX file state - loaded once, shared across all editing tabs
//   2. Shared 21990 file state - N64 config data shared across tabs that need it
//   3. Tab coordination - broadcasting file load/modification events to tabs
//   4. File save operations - collecting modifications from all tabs and saving
//   5. XDelta patch creation - generating patches for distribution
//
// Architecture:
// - XEX data is loaded into memory and shared via events (XexLoaded, XexModified)
// - Each tab implements IXexTab to participate in the shared state workflow
// - Tabs that need 21990 data additionally implement I21990Tab
// - When any tab modifies the XEX, the change is broadcast to all other tabs
// - On save, modifications from all tabs are collected and written to disk
// =============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XBLA_Setup_Editor.Controls;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Main application window with tabbed interface and shared XEX/21990 state.
    /// Coordinates data flow between all editing tabs and handles file I/O.
    /// </summary>
    public sealed class MainForm : Form
    {
        // =====================================================================
        // XEX State - The primary game executable being edited
        // =====================================================================

        /// <summary>
        /// Current in-memory XEX data with all modifications applied.
        /// This is the "working copy" that tabs read from and write to.
        /// </summary>
        private byte[]? _sharedXexData;

        /// <summary>
        /// Original unmodified XEX data, kept for creating xdelta patches.
        /// This allows comparing original vs modified to generate patches.
        /// </summary>
        private byte[]? _originalXexData;

        /// <summary>
        /// File path of the loaded XEX, used for save dialogs and display.
        /// </summary>
        private string? _sharedXexPath;

        /// <summary>
        /// Tracks whether any modifications have been made since last save.
        /// Used to warn user about unsaved changes.
        /// </summary>
        private bool _isModified;

        // =====================================================================
        // 21990 State - N64 configuration file (sky, fog, music, menu data)
        // =====================================================================

        /// <summary>
        /// Loaded 21990 file data containing N64 level configuration.
        /// Shared with tabs that implement I21990Tab.
        /// </summary>
        private byte[]? _shared21990Data;

        /// <summary>
        /// File path of the loaded 21990 file.
        /// </summary>
        private string? _shared21990Path;

        // =====================================================================
        // UI Controls - Main form layout elements
        // =====================================================================

        // XEX file selection controls
        private TextBox _txtXexPath = null!;       // Path display/input
        private Button _btnBrowseXex = null!;      // Browse for XEX file
        private Button _btnLoadXex = null!;        // Load the XEX into memory
        private Button _btnSave = null!;           // Save modified XEX

        // 21990 file selection controls
        private TextBox _txt21990Path = null!;     // Path display/input
        private Button _btnBrowse21990 = null!;    // Browse for 21990 file
        private Button _btnLoad21990 = null!;      // Load 21990 into memory

        /// <summary>
        /// Status label showing current state (loaded file, modification status).
        /// </summary>
        private readonly Label _lblStatus;

        /// <summary>
        /// Main tab control containing all editing tabs.
        /// </summary>
        private readonly TabControl _tabControl;

        // =====================================================================
        // Tab Controls - Individual editing tabs
        // =====================================================================

        /// <summary>STR/ADB string database editor (independent, doesn't use shared XEX).</summary>
        private readonly StrEditorControl _strEditorControl;

        /// <summary>Level setup conversion and XEX patching.</summary>
        private readonly SetupPatchingControl _setupPatchingControl;

        /// <summary>Import sky, fog, music data from 21990 files.</summary>
        private readonly File21990ImporterControl _file21990Control;

        /// <summary>Experimental XEX file extension (add data to XEX).</summary>
        private readonly XexExtenderControl _xexExtenderControl;

        /// <summary>MP setup region compactor (removes entries and shifts remaining ones down).</summary>
        private readonly MpSetupCompactorControl _mpSetupCompactorControl;

        /// <summary>Multiplayer weapon set editor.</summary>
        private readonly MPWeaponSetControl _mpWeaponSetControl;

        /// <summary>Weapon statistics and models editor.</summary>
        private readonly WeaponStatsControl _weaponStatsControl;

        // =====================================================================
        // Events - For notifying tabs of state changes
        // =====================================================================

        /// <summary>
        /// Fired when a XEX file is successfully loaded.
        /// All tabs implementing IXexTab will receive this event.
        /// </summary>
        public event EventHandler<XexLoadedEventArgs>? XexLoaded;

        /// <summary>
        /// Fired when a 21990 file is successfully loaded.
        /// All tabs implementing I21990Tab will receive this event.
        /// </summary>
        public event EventHandler<File21990LoadedEventArgs>? File21990Loaded;

        /// <summary>
        /// Constructs the main form, initializes all UI components and tabs.
        /// </summary>
        public MainForm()
        {
            // ---- Window Configuration ----
            Text = "XBLA Setup Editor";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            Width = DpiHelper.Scale(this, 1400);
            Height = DpiHelper.Scale(this, 900);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(DpiHelper.Scale(this, 1000), DpiHelper.Scale(this, 700));

            // ---- Easter Egg: Hidden Credits Button ----
            // Small invisible button in the upper right corner that shows credits when clicked
            var btnCredits = new Button
            {
                Text = "",
                Size = new Size(DpiHelper.Scale(this, 16), DpiHelper.Scale(this, 16)),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240),
                Cursor = Cursors.Hand
            };
            btnCredits.FlatAppearance.BorderSize = 0;
            btnCredits.Location = new Point(ClientSize.Width - btnCredits.Width - DpiHelper.Scale(this, 4), DpiHelper.Scale(this, 4));
            btnCredits.Click += (_, __) => ShowCredits();
            Controls.Add(btnCredits);
            btnCredits.BringToFront();

            // ---- Main Layout ----
            // TableLayoutPanel with 4 rows:
            // Row 0: XEX file controls
            // Row 1: 21990 file controls
            // Row 2: Status bar
            // Row 3: Tab control (fills remaining space)
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(DpiHelper.Scale(this, 8))
            };

            // Row 0: XEX file controls (fixed height)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            var xexPanel = CreateXexPanel();
            mainLayout.Controls.Add(xexPanel, 0, 0);

            // Row 1: 21990 file controls (fixed height)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            var panel21990 = Create21990Panel();
            mainLayout.Controls.Add(panel21990, 0, 1);

            // Row 2: Status bar (fixed height)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 24)));
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "No XEX loaded",
                ForeColor = Color.Gray
            };
            mainLayout.Controls.Add(_lblStatus, 0, 2);

            // Row 3: Tab control (fills remaining space)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            // ---- Create Tab Controls ----
            _strEditorControl = new StrEditorControl();
            _setupPatchingControl = new SetupPatchingControl();
            _file21990Control = new File21990ImporterControl();
            _xexExtenderControl = new XexExtenderControl();
            _mpWeaponSetControl = new MPWeaponSetControl();
            _weaponStatsControl = new WeaponStatsControl();
            _mpSetupCompactorControl = new MpSetupCompactorControl();

            // ---- Add Tabs (in display order) ----
            AddTab("STR Editor", _strEditorControl);
            AddTab("MP Weapon Sets", _mpWeaponSetControl);
            AddTab("Setup Patching", _setupPatchingControl);
            AddTab("Skies, Fog and Music", _file21990Control);
            AddTab("Weapon Stats", _weaponStatsControl);
            AddTab("XEX Extender", _xexExtenderControl);
            AddTab("MP Compactor", _mpSetupCompactorControl);

            mainLayout.Controls.Add(_tabControl, 0, 3);

            Controls.Add(mainLayout);

            // ---- Wire Up Event Handlers ----
            WireUpXexTabs();    // Connect XEX-related tabs to events
            WireUp21990Tabs();  // Connect 21990-related tabs to events

            // ---- Setup Tooltips ----
            SetupTooltips();
        }

        /// <summary>
        /// Creates the XEX file selection panel with path input, browse, load, and save buttons.
        /// </summary>
        /// <returns>FlowLayoutPanel containing XEX file controls.</returns>
        private FlowLayoutPanel CreateXexPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Label for the XEX file path
            panel.Controls.Add(new Label
            {
                Text = "XEX File:",
                AutoSize = true,
                Margin = new Padding(0, DpiHelper.Scale(this, 8), DpiHelper.Scale(this, 5), 0)
            });

            // Text box for file path (can be typed or set via Browse)
            _txtXexPath = new TextBox { Width = DpiHelper.Scale(this, 500), Height = DpiHelper.Scale(this, 23) };
            panel.Controls.Add(_txtXexPath);

            // Browse button - opens file dialog
            _btnBrowseXex = new Button { Text = "Browse...", Width = DpiHelper.Scale(this, 75), Height = DpiHelper.Scale(this, 28) };
            _btnBrowseXex.Click += BtnBrowseXex_Click;
            panel.Controls.Add(_btnBrowseXex);

            // Load button - reads XEX into memory and notifies tabs
            _btnLoadXex = new Button { Text = "Load", Width = DpiHelper.Scale(this, 60), Height = DpiHelper.Scale(this, 28) };
            _btnLoadXex.Click += BtnLoadXex_Click;
            panel.Controls.Add(_btnLoadXex);

            // Spacer between Load and Save
            panel.Controls.Add(new Label { Text = "  ", AutoSize = true, Margin = new Padding(DpiHelper.Scale(this, 10), 0, DpiHelper.Scale(this, 10), 0) });

            // Save As button - saves modified XEX to new file
            _btnSave = new Button { Text = "Save As...", Width = DpiHelper.Scale(this, 80), Height = DpiHelper.Scale(this, 28), Enabled = false };
            _btnSave.Click += BtnSave_Click;
            panel.Controls.Add(_btnSave);

            return panel;
        }

        /// <summary>
        /// Creates the 21990 file selection panel with path input, browse, and load buttons.
        /// </summary>
        /// <returns>FlowLayoutPanel containing 21990 file controls.</returns>
        private FlowLayoutPanel Create21990Panel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // Label for the 21990 file path
            panel.Controls.Add(new Label
            {
                Text = "21990 File:",
                AutoSize = true,
                Margin = new Padding(0, DpiHelper.Scale(this, 8), DpiHelper.Scale(this, 5), 0)
            });

            // Text box for file path
            _txt21990Path = new TextBox { Width = DpiHelper.Scale(this, 500), Height = DpiHelper.Scale(this, 23) };
            panel.Controls.Add(_txt21990Path);

            // Browse button
            _btnBrowse21990 = new Button { Text = "Browse...", Width = DpiHelper.Scale(this, 75), Height = DpiHelper.Scale(this, 28) };
            _btnBrowse21990.Click += BtnBrowse21990_Click;
            panel.Controls.Add(_btnBrowse21990);

            // Load button
            _btnLoad21990 = new Button { Text = "Load", Width = DpiHelper.Scale(this, 60), Height = DpiHelper.Scale(this, 28) };
            _btnLoad21990.Click += BtnLoad21990_Click;
            panel.Controls.Add(_btnLoad21990);

            return panel;
        }

        /// <summary>
        /// Adds a tab to the tab control with the given title and content control.
        /// </summary>
        /// <param name="title">Tab display title.</param>
        /// <param name="control">UserControl to display in the tab.</param>
        private void AddTab(string title, UserControl control)
        {
            var tabPage = new TabPage(title);
            control.Dock = DockStyle.Fill;
            tabPage.Controls.Add(control);
            _tabControl.TabPages.Add(tabPage);
        }

        /// <summary>
        /// Wires up all XEX-related tabs to receive XEX load and modification events.
        /// Also subscribes to XexModified events from tabs that can modify the XEX.
        /// </summary>
        private void WireUpXexTabs()
        {
            // Array of all tabs that implement IXexTab
            var xexTabs = new IXexTab[]
            {
                _setupPatchingControl,
                _file21990Control,
                _xexExtenderControl,
                _mpWeaponSetControl,
                _weaponStatsControl,
                _mpSetupCompactorControl
            };

            // Subscribe each tab to the XexLoaded event
            foreach (var tab in xexTabs)
            {
                XexLoaded += (_, e) => tab.OnXexLoaded(e.XexData, e.Path);
            }

            // Subscribe to modification events from tabs
            // When a tab modifies the XEX, we need to update shared state and notify other tabs
            _setupPatchingControl.XexModified += OnTabXexModified;
            _file21990Control.XexModified += OnTabXexModified;
            _xexExtenderControl.XexModified += OnTabXexModified;
            _mpWeaponSetControl.XexModified += OnTabXexModified;
            _weaponStatsControl.XexModified += OnTabXexModified;
            _mpSetupCompactorControl.XexModified += OnTabXexModified;
        }

        /// <summary>
        /// Wires up all 21990-related tabs to receive 21990 load events.
        /// </summary>
        private void WireUp21990Tabs()
        {
            // Array of all tabs that implement I21990Tab
            var tabs21990 = new I21990Tab[]
            {
                _file21990Control,
                _weaponStatsControl
            };

            // Subscribe each tab to the File21990Loaded event
            foreach (var tab in tabs21990)
            {
                File21990Loaded += (_, e) => tab.On21990Loaded(e.Data, e.Path);
            }
        }

        /// <summary>
        /// Sets up tooltips for all main form controls.
        /// </summary>
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

        // =====================================================================
        // Event Handlers - XEX File Operations
        // =====================================================================

        /// <summary>
        /// Opens a file dialog to browse for a XEX file.
        /// </summary>
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

        /// <summary>
        /// Loads the XEX file into memory and notifies all tabs.
        /// </summary>
        private void BtnLoadXex_Click(object? sender, EventArgs e)
        {
            var path = _txtXexPath.Text.Trim();

            // Validate input
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
                // Warn about unsaved changes
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

                // Load XEX into memory
                _sharedXexData = File.ReadAllBytes(path);

                // Keep a copy of the original for xdelta patch generation
                _originalXexData = (byte[])_sharedXexData.Clone();

                _sharedXexPath = path;
                _isModified = false;

                // Update UI
                UpdateStatus($"Loaded: {Path.GetFileName(path)} ({_sharedXexData.Length:N0} bytes)");
                _btnSave.Enabled = true;

                // Notify all tabs that XEX is loaded
                XexLoaded?.Invoke(this, new XexLoadedEventArgs(_sharedXexData, _sharedXexPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load XEX:\n{ex.Message}",
                    "Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves the modified XEX to a new file and optionally creates an xdelta patch.
        /// </summary>
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_sharedXexData == null || string.IsNullOrWhiteSpace(_sharedXexPath))
            {
                MessageBox.Show(this, "No XEX loaded.",
                    "Save XEX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Always use Save As - never overwrite the original
            // This protects users from accidentally destroying their source file
            var dir = Path.GetDirectoryName(_sharedXexPath) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(_sharedXexPath);
            var ext = Path.GetExtension(_sharedXexPath);

            // Use the batch folder name as the suffix when one is set (matches split XEX naming)
            var batchFolder = _setupPatchingControl.BatchFolderName;
            var suggestedName = !string.IsNullOrWhiteSpace(batchFolder)
                ? $"{baseName}_{batchFolder}{ext}"
                : $"{baseName}_modified{ext}";

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
                // Collect final modifications from all tabs
                CollectTabModifications();

                // Write the XEX to disk
                File.WriteAllBytes(sfd.FileName, _sharedXexData);

                _isModified = false;
                UpdateStatus($"Saved: {Path.GetFileName(sfd.FileName)}");

                MessageBox.Show(this, $"XEX saved successfully!\n\n{sfd.FileName}",
                    "Save XEX", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Offer to create xdelta patch for easy distribution
                if (_originalXexData != null)
                {
                    XdeltaHelper.OfferCreatePatch(this, _originalXexData, sfd.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save XEX:\n{ex.Message}",
                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Collects modifications from all XEX tabs and applies them to the shared data.
        /// Called just before saving to ensure all changes are captured.
        /// </summary>
        private void CollectTabModifications()
        {
            // Get modifications from each XEX tab and apply them
            var xexTabs = new IXexTab[]
            {
                _setupPatchingControl,
                _file21990Control,
                _xexExtenderControl,
                _mpWeaponSetControl,
                _weaponStatsControl,
                _mpSetupCompactorControl
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

        // =====================================================================
        // Event Handlers - 21990 File Operations
        // =====================================================================

        /// <summary>
        /// Opens a file dialog to browse for a 21990 file.
        /// </summary>
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

        /// <summary>
        /// Loads the 21990 file into memory and notifies all interested tabs.
        /// </summary>
        private void BtnLoad21990_Click(object? sender, EventArgs e)
        {
            var path = _txt21990Path.Text.Trim();

            // Validate input
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
                // Load 21990 into memory
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

        // =====================================================================
        // Tab Modification Handling
        // =====================================================================

        /// <summary>
        /// Handles XexModified events from tabs.
        /// Updates the shared state and re-broadcasts to other tabs.
        /// </summary>
        /// <param name="sender">The tab that made the modification.</param>
        /// <param name="e">Event args containing the modified data.</param>
        private void OnTabXexModified(object? sender, XexModifiedEventArgs e)
        {
            // Update shared state
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
                _weaponStatsControl,
                _mpSetupCompactorControl
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
        /// <param name="modifiedData">The modified XEX data.</param>
        /// <param name="source">Name of the source that made changes.</param>
        public void ApplyXexChanges(byte[] modifiedData, string source)
        {
            _sharedXexData = modifiedData;
            _isModified = true;
            UpdateStatus($"Modified by {source} (unsaved)");
        }

        /// <summary>
        /// Gets the currently loaded XEX data.
        /// </summary>
        /// <returns>Current XEX data, or null if not loaded.</returns>
        public byte[]? GetXexData() => _sharedXexData;

        /// <summary>
        /// Gets the original unmodified XEX data (for xdelta patches).
        /// </summary>
        /// <returns>Original XEX data, or null if not loaded.</returns>
        public byte[]? GetOriginalXexData() => _originalXexData;

        /// <summary>
        /// Gets the path of the currently loaded XEX.
        /// </summary>
        /// <returns>XEX file path, or null if not loaded.</returns>
        public string? GetXexPath() => _sharedXexPath;

        // =====================================================================
        // UI Helpers
        // =====================================================================

        /// <summary>
        /// Shows the credits/thanks dialog (hidden easter egg).
        /// </summary>
        private void ShowCredits()
        {
            var creditsForm = new Form
            {
                Text = "Credits & Thanks",
                Width = DpiHelper.Scale(this, 400),
                Height = DpiHelper.Scale(this, 350),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var txtCredits = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10 * DpiHelper.GetScaleFactor(this)),
                BackColor = Color.White,
                Text =
                    "XBLA Setup Editor\r\n" +
                    "=================\r\n\r\n" +
                    "Alpha made by Murk17, with the slop help from Claude.\r\n" +
                    "Sometimes, you gotta do things yourself.\r\n\r\n" +
                    "SPECIAL THANKS\r\n" +
                    "--------------\r\n\r\n" +
                    "Shooters Forever forum for their breakdown of the XEX file.\r\n" +
                    "This was an immense help for getting my feet off the ground.\r\n\r\n" +
                    "Wreck for pioneering that post, and helping me with some\r\n" +
                    "silly DMs about the 21990 format. Thanks for being an\r\n" +
                    "absolute legend in the GE community.\r\n\r\n" +
                    "RedVox57 for the suggestions and bug squishing for the\r\n" +
                    "simplest issues. Thanks for showing me the ropes for the\r\n" +
                    "GE Setup Editor many months ago.\r\n\r\n" +
                    "AdzyIn3D for allowing me to join in and play XBLA online\r\n" +
                    "in June 2025, I don't think I'd be here otherwise.\r\n\r\n" +
                    "AXDOOMER for guidance and direction on a handful of\r\n" +
                    "features, the other legend in the GE modding space.\r\n\r\n" +
                    "Carnivorous for your setupconv, without this I don't think\r\n" +
                    "I would have gone further than just editing hex values in\r\n" +
                    "ImHex and looking at a spreadsheet to make MP Loadouts.\r\n\r\n" +
                    "BrandonLooneyTunesFan2000. The Cradle Champ and the best\r\n" +
                    "skyboxes ever. Go sub to his channel on YT.\r\n\r\n" +
                    "And as corny as it is; You. You for looking into this tool\r\n" +
                    "and keeping the modding community alive in both a 30 year\r\n" +
                    "old game and an unreleased beta. You're the heartbeat\r\n" +
                    "that'll keep this community alive. For better, or worse.\r\n\r\n" +
                    "Finally, DravonKing. Lava you ∞.\r\n"
            };

            var btnClose = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = DpiHelper.Scale(this, 32)
            };
            btnClose.Click += (_, __) => creditsForm.Close();

            creditsForm.Controls.Add(txtCredits);
            creditsForm.Controls.Add(btnClose);
            creditsForm.ShowDialog(this);
        }

        /// <summary>
        /// Updates the status label with a message and appropriate color.
        /// </summary>
        /// <param name="message">Status message to display.</param>
        private void UpdateStatus(string message)
        {
            _lblStatus.Text = message;
            // Show orange color when there are unsaved changes
            _lblStatus.ForeColor = _isModified ? Color.DarkOrange : Color.Black;
        }

        /// <summary>
        /// Handles form load event. Shows usage agreement dialog.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Show usage agreement on startup
            const string disclaimer =
                "USAGE AGREEMENT\n\n" +
                "By using this tool, you agree to the following:\n\n" +
                "1. Do not convert, distribute, or upload setups or mods created by others without their explicit permission.\n\n" +
                "2. Do not claim another creator's work as your own.\n\n" +
                "3. Always credit the original creator when sharing or building upon their work.\n\n" +
                "Respect the modding community and the effort creators put into their work.";

            var result = MessageBox.Show(this,
                disclaimer,
                "Usage Agreement",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);

            // Close the application if user doesn't agree
            if (result != DialogResult.OK)
            {
                Close();
            }
        }

        /// <summary>
        /// Handles form closing event.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // Could add unsaved changes warning here if needed
        }
    }
}

