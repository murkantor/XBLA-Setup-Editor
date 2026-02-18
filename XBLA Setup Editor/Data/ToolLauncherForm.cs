using System;
using System.Windows.Forms;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    public sealed class ToolLauncherForm : Form
    {
        public ToolLauncherForm()
        {
            Text = "XBLA Setup Editor - Tools";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            Width = DpiHelper.Scale(this, 420);
            Height = DpiHelper.Scale(this, 420);
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(DpiHelper.Scale(this, 12)),
                ColumnCount = 1,
                RowCount = 7
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(this, 50)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var btnStr = new Button { Text = "STR Editor", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };
            var btnSetup = new Button { Text = "Setup Patching", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };
            var btn21990 = new Button { Text = "Import 21990 File", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };
            var btnXexExtend = new Button { Text = "XEX Extender", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };
            var btnMPWeapons = new Button { Text = "MP Weapon Set Editor", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };
            var btnWeaponStats = new Button { Text = "Weapon Stats Editor", Dock = DockStyle.Fill, Height = DpiHelper.Scale(this, 40) };

            btnStr.Click += (_, __) => new StrEditorForm().Show(this);
            btnSetup.Click += (_, __) => new SetupPatchingForm().Show(this);
            btn21990.Click += (_, __) => new File21990ImporterForm().Show(this);
            btnXexExtend.Click += (_, __) => new XexExtenderForm().Show(this);
            btnMPWeapons.Click += (_, __) => new MPWeaponSetEditorForm().Show(this);
            btnWeaponStats.Click += (_, __) => new WeaponStatsEditorForm().Show(this);

            layout.Controls.Add(btnStr, 0, 0);
            layout.Controls.Add(btnSetup, 0, 1);
            layout.Controls.Add(btn21990, 0, 2);
            layout.Controls.Add(btnXexExtend, 0, 3);
            layout.Controls.Add(btnMPWeapons, 0, 4);
            layout.Controls.Add(btnWeaponStats, 0, 5);

            Controls.Add(layout);
        }
    }
}
