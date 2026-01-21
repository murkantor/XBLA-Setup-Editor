using System.Text;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    public partial class WeaponEditorForm : Form
    {
        // Runtime maps (fed by Data/*.cs)
        private Dictionary<string, int> _weapon = new();
        private Dictionary<string, int> _ammoType = new();
        private Dictionary<string, int> _ammoCount = new();
        private Dictionary<string, int> _toggle = new();
        private Dictionary<string, int> _prop = new();
        private Dictionary<string, int> _unk = new();
        private Dictionary<string, int> _scale = new();

        // Beginner rules (fed by Data/BeginnerRulesData.cs)
        private bool _advancedMode = false;
        private Dictionary<string, string> _weaponToAmmoType = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _weaponToDefaultAmmoCount = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _propNames = new(StringComparer.OrdinalIgnoreCase);

        private const string DEFAULT_TOGGLE = "Yes";
        private const string DEFAULT_UNK = "No";
        private const string DEFAULT_SCALE = "Normal";

        // Clipboard raw hex (no spaces)
        private string _rawHexOutput = "";

        public WeaponEditorForm()
        {
            Text = "Weapon Setup Maker";
            Width = 520;
            Height = 460;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 11,
                Padding = new Padding(12),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            // Controls
            ComboBox cbWeapon = MakeCombo();
            ComboBox cbAmmoType = MakeCombo();
            ComboBox cbAmmoCount = MakeCombo();
            ComboBox cbToggle = MakeCombo();
            ComboBox cbProp = MakeCombo();
            ComboBox cbUnk = MakeCombo();
            ComboBox cbScale = MakeCombo();

            var chkAdvanced = new CheckBox
            {
                Text = "Advanced (unlock all options)",
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var btnCopy = new Button
            {
                Text = "Copy raw hex to clipboard",
                Dock = DockStyle.Fill,
                Height = 36
            };

            var output = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 10)
            };

            // Layout rows
            AddRow(layout, 0, "Weapon ID", cbWeapon);
            AddRow(layout, 1, "Ammo Type", cbAmmoType);
            AddRow(layout, 2, "Ammo Count", cbAmmoCount);
            AddRow(layout, 3, "Weapon Toggle", cbToggle);
            AddRow(layout, 4, "Unk", cbUnk);
            AddRow(layout, 5, "Prop ID", cbProp);
            AddRow(layout, 6, "Scale", cbScale);

            layout.Controls.Add(chkAdvanced, 0, 7);
            layout.SetColumnSpan(chkAdvanced, 2);

            layout.Controls.Add(btnCopy, 0, 8);
            layout.SetColumnSpan(btnCopy, 2);

            layout.Controls.Add(new Label
            {
                Text = "Output (spaced hex bytes)",
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            }, 0, 9);
            layout.SetColumnSpan(layout.Controls[layout.Controls.Count - 1], 2);

            layout.Controls.Add(output, 0, 10);
            layout.SetColumnSpan(output, 2);

            Controls.Add(layout);

            // Load all hardcoded data
            try
            {
                LoadHardcoded();

                // Bind dropdowns
                Bind(cbWeapon, _weapon.Keys);
                Bind(cbAmmoType, _ammoType.Keys);
                Bind(cbAmmoCount, _ammoCount.Keys);
                Bind(cbToggle, _toggle.Keys);
                Bind(cbProp, _prop.Keys);
                Bind(cbUnk, _unk.Keys);
                Bind(cbScale, _scale.Keys);

                // Defaults
                SelectFirst(cbWeapon);
                SelectFirst(cbAmmoType);
                SelectFirst(cbAmmoCount);
                SelectFirst(cbToggle);
                SelectFirst(cbProp);
                SelectFirst(cbUnk);
                SelectFirst(cbScale);

                // Beginner mode default
                chkAdvanced.Checked = false;
                _advancedMode = false;

                SetAdvancedEnabled(cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale, enabled: false);

                // Apply beginner auto-rules once at start
                ApplyBeginnerDefaults(cbWeapon, cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale);

                // Live output updates
                HookLiveUpdate(cbWeapon, cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale, output);

                // When weapon changes, update dependent fields in Beginner mode
                cbWeapon.SelectedIndexChanged += (_, __) =>
                {
                    if (!_advancedMode)
                        ApplyBeginnerDefaults(cbWeapon, cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale);
                };

                // Advanced toggle
                chkAdvanced.CheckedChanged += (_, __) =>
                {
                    _advancedMode = chkAdvanced.Checked;

                    SetAdvancedEnabled(cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale, enabled: _advancedMode);

                    if (!_advancedMode)
                        ApplyBeginnerDefaults(cbWeapon, cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale);
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            // Copy raw hex (no spaces) to clipboard
            btnCopy.Click += (_, __) =>
            {
                if (!string.IsNullOrWhiteSpace(_rawHexOutput))
                    Clipboard.SetText(_rawHexOutput);
            };
        }

        private void LoadHardcoded()
        {
            _weapon = WeaponData.Build();
            _ammoType = AmmoTypeData.Build();
            _ammoCount = AmmoCountData.Build();
            _toggle = ToggleData.Build();
            _prop = PropData.Build();
            _unk = UnkData.Build();
            _scale = ScaleData.Build();

            _weaponToAmmoType = new Dictionary<string, string>(BeginnerRulesData.WeaponToAmmoType, StringComparer.OrdinalIgnoreCase);
            _weaponToDefaultAmmoCount = new Dictionary<string, string>(BeginnerRulesData.WeaponToDefaultAmmoCount, StringComparer.OrdinalIgnoreCase);
            _propNames = new HashSet<string>(BeginnerRulesData.PropNames, StringComparer.OrdinalIgnoreCase);
        }

        // ===== UI helpers =====

        private static ComboBox MakeCombo() => new()
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        private static void AddRow(TableLayoutPanel p, int row, string label, Control control)
        {
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            p.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            }, 0, row);
            p.Controls.Add(control, 1, row);
        }

        private static void Bind(ComboBox cb, IEnumerable<string> items)
        {
            cb.Items.Clear();
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x)))
                cb.Items.Add(item);
        }

        private static void SelectFirst(ComboBox cb)
        {
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        private static void SetAdvancedEnabled(
            ComboBox cbAmmoType,
            ComboBox cbAmmoCount,
            ComboBox cbToggle,
            ComboBox cbProp,
            ComboBox cbUnk,
            ComboBox cbScale,
            bool enabled)
        {
            cbAmmoType.Enabled = enabled;
            cbAmmoCount.Enabled = enabled;
            cbToggle.Enabled = enabled;
            cbProp.Enabled = enabled;
            cbUnk.Enabled = enabled;
            cbScale.Enabled = enabled;
        }

        // ===== Output generation =====

        private static string Code2(Dictionary<string, int> map, ComboBox cb)
        {
            if (cb.SelectedItem is null)
                throw new InvalidOperationException("Please select all dropdowns.");

            var key = cb.SelectedItem.ToString() ?? "";
            if (!map.TryGetValue(key, out var code))
                throw new KeyNotFoundException("No code mapping found for '" + key + "'.");

            return $"{code:X2}";
        }

        private static string FormatHexBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return hex ?? "";

            var sb = new StringBuilder(hex.Length + hex.Length / 2);
            for (int i = 0; i < hex.Length; i += 2)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(hex, i, 2);
            }
            return sb.ToString();
        }

        private void UpdateOutputLive(
            ComboBox cbWeapon,
            ComboBox cbAmmoType,
            ComboBox cbAmmoCount,
            ComboBox cbToggle,
            ComboBox cbProp,
            ComboBox cbUnk,
            ComboBox cbScale,
            TextBox output)
        {
            try
            {
                var w = Code2(_weapon, cbWeapon);
                var aT = Code2(_ammoType, cbAmmoType);
                var aC = Code2(_ammoCount, cbAmmoCount);
                var t = Code2(_toggle, cbToggle);
                var p = Code2(_prop, cbProp);
                var u = Code2(_unk, cbUnk);
                var s = Code2(_scale, cbScale);

                _rawHexOutput = (w + aT + aC + t + u + p + s).ToUpperInvariant();
                output.Text = FormatHexBytes(_rawHexOutput);
            }
            catch
            {
                _rawHexOutput = "";
                output.Text = "";
            }
        }

        private void HookLiveUpdate(
            ComboBox cbWeapon,
            ComboBox cbAmmoType,
            ComboBox cbAmmoCount,
            ComboBox cbToggle,
            ComboBox cbProp,
            ComboBox cbUnk,
            ComboBox cbScale,
            TextBox output)
        {
            void Refresh() => UpdateOutputLive(cbWeapon, cbAmmoType, cbAmmoCount, cbToggle, cbProp, cbUnk, cbScale, output);

            cbWeapon.SelectedIndexChanged += (_, __) => Refresh();
            cbAmmoType.SelectedIndexChanged += (_, __) => Refresh();
            cbAmmoCount.SelectedIndexChanged += (_, __) => Refresh();
            cbToggle.SelectedIndexChanged += (_, __) => Refresh();
            cbProp.SelectedIndexChanged += (_, __) => Refresh();
            cbUnk.SelectedIndexChanged += (_, __) => Refresh();
            cbScale.SelectedIndexChanged += (_, __) => Refresh();

            Refresh();
        }

        // ===== Beginner auto-rules =====

        private void ApplyBeginnerDefaults(
            ComboBox cbWeapon,
            ComboBox cbAmmoType,
            ComboBox cbAmmoCount,
            ComboBox cbToggle,
            ComboBox cbProp,
            ComboBox cbUnk,
            ComboBox cbScale)
        {
            if (cbWeapon.SelectedItem is null) return;
            var weapon = cbWeapon.SelectedItem.ToString() ?? "";

            if (_weaponToAmmoType.TryGetValue(weapon, out var ammoType))
                SelectIfExists(cbAmmoType, ammoType);

            if (_weaponToDefaultAmmoCount.TryGetValue(weapon, out var ammoCount))
                SelectIfExists(cbAmmoCount, ammoCount);

            SelectIfExists(cbToggle, DEFAULT_TOGGLE);
            SelectIfExists(cbUnk, DEFAULT_UNK);
            SelectIfExists(cbScale, DEFAULT_SCALE);

            if (_propNames.Contains(weapon))
                SelectIfExists(cbProp, weapon);
        }

        private static void SelectIfExists(ComboBox cb, string value)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (string.Equals(cb.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
