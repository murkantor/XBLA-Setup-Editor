using System;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public sealed class ToolLauncherForm : Form
    {
        public ToolLauncherForm()
        {
            Text = "XBLA Setup Editor - Tools";
            Width = 420;
            Height = 260;
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var btnWeapon = new Button { Text = "Weapon Setup Maker", Dock = DockStyle.Fill, Height = 40 };
            var btnStr = new Button { Text = "STR Editor", Dock = DockStyle.Fill, Height = 40 };
            var btnSetup = new Button { Text = "Setup Patching", Dock = DockStyle.Fill, Height = 40 };

            btnWeapon.Click += (_, __) => new WeaponEditorForm().Show(this);
            btnStr.Click += (_, __) => new StrEditorForm().Show(this);
            btnSetup.Click += (_, __) => new SetupPatchingForm().Show(this);

            layout.Controls.Add(btnWeapon, 0, 0);
            layout.Controls.Add(btnStr, 0, 1);
            layout.Controls.Add(btnSetup, 0, 2);

            Controls.Add(layout);
        }
    }
}
