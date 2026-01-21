using System;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    public sealed class MainMenuForm : Form
    {
        public MainMenuForm()
        {
            Text = "XBLA Setup Editor - Tools";
            Width = 420;
            Height = 280;
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(14)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var btnWeapon = new Button { Text = "Weapon Setup Maker", Dock = DockStyle.Fill, Height = 40 };
            var btnStr = new Button { Text = "STR Editor", Dock = DockStyle.Fill, Height = 40 };
            var btnSetup = new Button { Text = "Setup Patching", Dock = DockStyle.Fill, Height = 40 };

            var lbl = new Label
            {
                Text = "Select a tool:",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            var btnExit = new Button { Text = "Exit", Dock = DockStyle.Right, Width = 100 };

            layout.Controls.Add(lbl, 0, 0);
            layout.Controls.Add(btnWeapon, 0, 1);
            layout.Controls.Add(btnStr, 0, 2);
            layout.Controls.Add(btnSetup, 0, 3);
            layout.Controls.Add(btnExit, 0, 4);

            Controls.Add(layout);

            btnWeapon.Click += (_, __) => new WeaponEditorForm().Show(this);
            btnStr.Click += (_, __) => new StrEditorForm().Show(this);
            btnSetup.Click += (_, __) => new SetupPatchingForm().Show(this);
            btnExit.Click += (_, __) => Close();
        }
    }
}
