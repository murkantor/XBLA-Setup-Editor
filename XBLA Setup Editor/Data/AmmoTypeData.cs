using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    internal static class AmmoTypeData
    {
        internal static readonly (string Name, int Code)[] Pairs = new (string Name, int Code)[]
        {
            ("None",0x00),
            ("9mm Ammo",0x01),
            ("Rifle Ammo",0x03),
            ("Cartridges",0x04),
            ("Grenades",0x05),
            ("Rockets",0x06),
            ("Remote Mines",0x07),
            ("Proximity Mines",0x08),
            ("Timed Mines",0x09),
            ("Knife",0x0A),
            ("Grenade Rounds",0x0B),
            ("Magnum Bullets",0x0C),
            ("Golden Bullets",0x0D),
            ("Watch Laser",0x18),
            ("Tank",0xC1),
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