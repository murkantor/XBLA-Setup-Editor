using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    internal static class WeaponData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("Nothing (No Pickup)", 0x00),
            ("Unarmed", 0x01),
            ("Hunting Knives", 0x02),
            ("Throwing Knives", 0x03),
            ("PP7", 0x04),
            ("PP7 (Silenced)", 0x05),
            ("DD44", 0x06),
            ("Klobb", 0x07),
            ("KF7", 0x08),
            ("ZMG", 0x09),
            ("D5K", 0x0A),
            ("D5K (Silenced)", 0x0B),
            ("Phantom", 0x0C),
            ("AR33", 0x0D),
            ("RC-P90", 0x0E),
            ("Shotgun", 0x0F),
            ("Automatic Shotgun", 0x10),
            ("Sniper Rifle", 0x11),
            ("Cougar Magnum", 0x12),
            ("Golden Gun", 0x13),
            ("Silver PP7", 0x14),
            ("Gold PP7", 0x15),
            ("Moonraker Laser", 0x16),
            ("Watch Laser", 0x17),
            ("Grenade Launcher", 0x18),
            ("Rocket Launcher", 0x19),
            ("Grenades", 0x1A),
            ("Timed Mine", 0x1B),
            ("Proximity Mine", 0x1C),
            ("Remote Mine", 0x1D),
            ("Detonator", 0x1E),
            ("Tazer", 0x1F),
            ("Tank", 0x20),
        };

        internal static Dictionary<string, int> Build()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Pairs)
                if (!d.ContainsKey(kv.Name)) d[kv.Name] = kv.Code;
            return d;
        }
    }
}
