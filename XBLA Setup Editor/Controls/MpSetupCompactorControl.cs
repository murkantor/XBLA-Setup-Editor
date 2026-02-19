using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor.Controls
{
    /// <summary>
    /// Tab that compacts the MP setup region of the XEX by removing the five
    /// entries that are no longer needed (Library/Basement/Stack, Citadel,
    /// Caves, Complex, Temple) and shifting the remaining entries down.
    /// </summary>
    public sealed class MpSetupCompactorControl : UserControl, IXexTab
    {
        // --- UI ---
        private readonly CheckedListBox _clbEntries;
        private readonly Button _btnCompact;
        private readonly Button _btnFixBg;
        private readonly TextBox _txtLog;
        private readonly Label _lblStatus;

        // --- State ---
        private byte[]? _xexData;
        private bool _hasUnsavedChanges;
        private IReadOnlyList<MpSetupCompactor.MpSetupEntry>? _newLayout;

        public event EventHandler<XexModifiedEventArgs>? XexModified;

        public MpSetupCompactorControl()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(DpiHelper.Scale(this, 8))
            };

            // Row 0: instruction label
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lblInfo = new Label
            {
                Text =
                    "Select the MP setup entries to remove, then click Compact. " +
                    "The remaining entries will be shifted down to fill the gaps, " +
                    "freeing space at the end of the region.\r\n" +
                    "BG pointer fixup is a separate step done after compaction.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, DpiHelper.Scale(this, 4))
            };
            layout.Controls.Add(lblInfo, 0, 0);

            // Row 1: checked list box of entries
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 200)));
            _clbEntries = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this))
            };
            foreach (var entry in MpSetupCompactor.KnownLayout)
            {
                bool checkedByDefault = MpSetupCompactor.DefaultRemove
                    .Any(d => d.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));
                _clbEntries.Items.Add(
                    $"{entry.Name,-28}  0x{entry.FileOffset:X7}  size 0x{entry.Size:X5}  ({entry.Size:N0} bytes)",
                    checkedByDefault);
            }
            layout.Controls.Add(_clbEntries, 0, 1);

            // Row 2: status + button
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 36)));
            var buttonRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "No XEX loaded.",
                ForeColor = Color.Gray
            };
            buttonRow.Controls.Add(_lblStatus, 0, 0);

            _btnCompact = new Button
            {
                Text = "Compact MP Setups",
                AutoSize = true,
                Enabled = false,
                Height = DpiHelper.Scale(this, 28)
            };
            _btnCompact.Click += BtnCompact_Click;
            buttonRow.Controls.Add(_btnCompact, 1, 0);

            _btnFixBg = new Button
            {
                Text = "Fix BG Pointers",
                AutoSize = true,
                Enabled = false,
                Height = DpiHelper.Scale(this, 28),
                Margin = new Padding(DpiHelper.Scale(this, 4), 0, 0, 0)
            };
            _btnFixBg.Click += BtnFixBg_Click;
            buttonRow.Controls.Add(_btnFixBg, 2, 0);
            layout.Controls.Add(buttonRow, 0, 2);

            // Row 3: log
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9 * DpiHelper.GetScaleFactor(this))
            };
            layout.Controls.Add(_txtLog, 0, 3);

            Controls.Add(layout);
        }

        // =====================================================================
        // IXexTab
        // =====================================================================

        public string TabDisplayName => "MP Compactor";
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public void OnXexLoaded(byte[] xexData, string path)
        {
            _xexData = xexData;
            _hasUnsavedChanges = false;
            _newLayout = null;
            _btnCompact.Enabled = true;
            _btnFixBg.Enabled = false;
            _lblStatus.Text = $"Loaded: {System.IO.Path.GetFileName(path)}";
            _lblStatus.ForeColor = Color.Black;
            _txtLog.Clear();
        }

        public void OnXexDataUpdated(byte[] xexData)
        {
            _xexData = xexData;
        }

        public void OnXexUnloaded()
        {
            _xexData = null;
            _hasUnsavedChanges = false;
            _newLayout = null;
            _btnCompact.Enabled = false;
            _btnFixBg.Enabled = false;
            _lblStatus.Text = "No XEX loaded.";
            _lblStatus.ForeColor = Color.Gray;
        }

        public byte[]? GetModifiedXexData()
        {
            if (!_hasUnsavedChanges) return null;
            _hasUnsavedChanges = false;
            return _xexData;
        }

        // =====================================================================
        // Compaction
        // =====================================================================

        private void BtnCompact_Click(object? sender, EventArgs e)
        {
            if (_xexData == null) { MessageBox.Show(FindForm(), "No XEX loaded."); return; }

            // Collect which entries are checked for removal
            var toRemove = new List<string>();
            for (int i = 0; i < _clbEntries.Items.Count; i++)
            {
                if (_clbEntries.GetItemChecked(i))
                    toRemove.Add(MpSetupCompactor.KnownLayout[i].Name);
            }

            if (toRemove.Count == 0)
            {
                MessageBox.Show(FindForm(), "No entries selected for removal.");
                return;
            }

            int totalRemoved = MpSetupCompactor.KnownLayout
                .Where(e => toRemove.Contains(e.Name, StringComparer.OrdinalIgnoreCase))
                .Sum(e => e.Size);

            var confirm = MessageBox.Show(FindForm(),
                $"Remove {toRemove.Count} entries ({totalRemoved:N0} bytes) and compact the MP setup region?\n\n" +
                $"This modifies the XEX in memory. Save As afterwards to write to disk.",
                "Confirm Compaction",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var newXex = MpSetupCompactor.Compact(_xexData, toRemove, out var newLayout, out var report);

                _txtLog.Text = string.Join("\r\n", report);

                _xexData = newXex;
                _newLayout = newLayout;
                _hasUnsavedChanges = true;
                _btnFixBg.Enabled = true;
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                int freed = MpSetupCompactor.RegionEnd - (newLayout.Count > 0
                    ? newLayout[newLayout.Count - 1].FileOffset + newLayout[newLayout.Count - 1].Size
                    : MpSetupCompactor.RegionStart);

                _lblStatus.Text = $"Compacted — {freed:N0} bytes freed. Run Fix BG Pointers next. Unsaved.";
                _lblStatus.ForeColor = Color.DarkOrange;
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "Compaction Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnFixBg_Click(object? sender, EventArgs e)
        {
            if (_xexData == null)   { MessageBox.Show(FindForm(), "No XEX loaded.");          return; }
            if (_newLayout == null) { MessageBox.Show(FindForm(), "Run compaction first.");    return; }

            try
            {
                MpSetupCompactor.FixBgPointers(_xexData, _newLayout, out var report);

                // Append the fixup report below the compaction report
                _txtLog.AppendText("\r\n" + string.Join("\r\n", report));
                _txtLog.ScrollToCaret();

                _hasUnsavedChanges = true;
                _btnFixBg.Enabled = false; // one-shot — don't apply twice
                XexModified?.Invoke(this, new XexModifiedEventArgs(_xexData, TabDisplayName));

                _lblStatus.Text = "BG pointers fixed. Unsaved.";
                _lblStatus.ForeColor = Color.DarkOrange;
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.ToString(), "BG Pointer Fixup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
