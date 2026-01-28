using System;
using System.Collections.Generic;
using System.IO;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Parses and patches Multiplayer Weapon Sets in GoldenEye XBLA XEX files.
    /// </summary>
    public sealed class MPWeaponSetParser
    {
        // XEX offsets for MP Weapon Sets
        public const int WEAPONS_START = 0x417728;
        public const int WEAPONS_END = 0x417AE7;
        public const int SELECT_LIST_START = 0x417AE8;
        public const int SELECT_LIST_END = 0x417BA7;

        // Structure sizes
        public const int WEAPON_ENTRY_SIZE = 0x8;      // 8 bytes per weapon
        public const int WEAPONS_PER_SET = 8;          // 8 weapons per set
        public const int WEAPON_SET_SIZE = 0x40;       // 64 bytes per weapon set (8 * 8)
        public const int SELECT_ENTRY_SIZE = 0x0C;     // 12 bytes per select list entry

        // Counts
        public const int WEAPON_SET_COUNT = 15;        // 15 weapon sets (excluding Random)
        public const int SELECT_LIST_COUNT = 16;       // 16 select list entries (including Random)

        /// <summary>
        /// Represents a single weapon entry in a weapon set.
        /// </summary>
        public sealed class WeaponEntry
        {
            public byte WeaponId { get; set; }         // 0x00
            public byte AmmoType { get; set; }         // 0x01
            public byte AmmoCount { get; set; }        // 0x02
            public byte WeaponToggle { get; set; }     // 0x03 (00 = no prop, 01 = has prop)
            public byte Unknown04 { get; set; }        // 0x04 (usually 0x00)
            public byte PropId { get; set; }           // 0x05
            public byte Scale { get; set; }            // 0x06
            public byte Unknown07 { get; set; }        // 0x07 (00/40/80)

            public string WeaponName => GetNameByCode(WeaponData.Pairs, WeaponId);
            public string AmmoTypeName => GetNameByCode(AmmoTypeData.Pairs, AmmoType);
            public string PropName => GetNameByCode(PropData.Pairs, PropId);

            public byte[] ToBytes()
            {
                var data = new byte[WEAPON_ENTRY_SIZE];
                data[0] = WeaponId;
                data[1] = AmmoType;
                data[2] = AmmoCount;
                data[3] = WeaponToggle;
                data[4] = Unknown04;
                data[5] = PropId;
                data[6] = Scale;
                data[7] = Unknown07;
                return data;
            }

            public static WeaponEntry FromBytes(byte[] data, int offset = 0)
            {
                return new WeaponEntry
                {
                    WeaponId = data[offset],
                    AmmoType = data[offset + 1],
                    AmmoCount = data[offset + 2],
                    WeaponToggle = data[offset + 3],
                    Unknown04 = data[offset + 4],
                    PropId = data[offset + 5],
                    Scale = data[offset + 6],
                    Unknown07 = data[offset + 7]
                };
            }

            public override string ToString() =>
                $"{WeaponName} (Ammo: {AmmoTypeName} x{AmmoCount}, Prop: {(WeaponToggle != 0 ? PropName : "None")})";
        }

        /// <summary>
        /// Represents a weapon set (8 weapons).
        /// </summary>
        public sealed class WeaponSet
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }
            public WeaponEntry[] Weapons { get; set; } = new WeaponEntry[WEAPONS_PER_SET];

            public byte[] ToBytes()
            {
                var data = new byte[WEAPON_SET_SIZE];
                for (int i = 0; i < WEAPONS_PER_SET; i++)
                {
                    var weaponBytes = Weapons[i].ToBytes();
                    Array.Copy(weaponBytes, 0, data, i * WEAPON_ENTRY_SIZE, WEAPON_ENTRY_SIZE);
                }
                return data;
            }

            public static WeaponSet FromBytes(byte[] data, int offset, int index)
            {
                var set = new WeaponSet
                {
                    Index = index,
                    FileOffset = offset,
                    Weapons = new WeaponEntry[WEAPONS_PER_SET]
                };

                for (int i = 0; i < WEAPONS_PER_SET; i++)
                {
                    set.Weapons[i] = WeaponEntry.FromBytes(data, offset + (i * WEAPON_ENTRY_SIZE));
                }

                return set;
            }
        }

        /// <summary>
        /// Represents a select list entry (menu item for weapon set selection).
        /// </summary>
        public sealed class SelectListEntry
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }
            public uint TextId { get; set; }           // 0x00-0x03
            public uint WeaponsAddress { get; set; }   // 0x04-0x07 (pointer to weapons)
            public uint Flags { get; set; }            // 0x08-0x0B

            // Weapon set index: Select list index 0 = Random (no weapons, returns -1)
            // Select list indices 1-15 map to weapon set indices 0-14
            public int WeaponSetIndex => Index == 0 ? -1 : Index - 1;

            public byte[] ToBytes()
            {
                var data = new byte[SELECT_ENTRY_SIZE];
                WriteU32BE(data, 0, TextId);
                WriteU32BE(data, 4, WeaponsAddress);
                WriteU32BE(data, 8, Flags);
                return data;
            }

            public static SelectListEntry FromBytes(byte[] data, int offset, int index)
            {
                return new SelectListEntry
                {
                    Index = index,
                    FileOffset = offset,
                    TextId = ReadU32BE(data, offset),
                    WeaponsAddress = ReadU32BE(data, offset + 4),
                    Flags = ReadU32BE(data, offset + 8)
                };
            }
        }

        // Parsed data
        public List<WeaponSet> WeaponSets { get; } = new();
        public List<SelectListEntry> SelectList { get; } = new();

        // Default select list names (in order)
        public static readonly string[] SelectListNames =
        {
            "Random",
            "Slappers Only!",
            "Pistols",
            "Throwing Knives",
            "Automatics",
            "Power Weapons",
            "Sniper Rifles",
            "Grenades",
            "Remote Mines",
            "Grenade Launchers",
            "Timed Mines",
            "Proximity Mines",
            "Rockets",
            "Lasers",
            "Golden Gun",
            "Hunting Knives"
        };

        /// <summary>
        /// Loads MP Weapon Sets from XEX file data.
        /// </summary>
        public static MPWeaponSetParser LoadFromXex(byte[] xexData)
        {
            var parser = new MPWeaponSetParser();
            parser.Parse(xexData);
            return parser;
        }

        /// <summary>
        /// Loads MP Weapon Sets from an XEX file path.
        /// </summary>
        public static MPWeaponSetParser LoadFromFile(string path)
        {
            var xexData = File.ReadAllBytes(path);
            return LoadFromXex(xexData);
        }

        private void Parse(byte[] xexData)
        {
            WeaponSets.Clear();
            SelectList.Clear();

            if (xexData.Length < SELECT_LIST_END)
                throw new InvalidOperationException($"XEX file too small. Expected at least {SELECT_LIST_END} bytes.");

            // Parse weapon sets
            for (int i = 0; i < WEAPON_SET_COUNT; i++)
            {
                int offset = WEAPONS_START + (i * WEAPON_SET_SIZE);
                WeaponSets.Add(WeaponSet.FromBytes(xexData, offset, i));
            }

            // Parse select list
            for (int i = 0; i < SELECT_LIST_COUNT; i++)
            {
                int offset = SELECT_LIST_START + (i * SELECT_ENTRY_SIZE);
                SelectList.Add(SelectListEntry.FromBytes(xexData, offset, i));
            }
        }

        /// <summary>
        /// Applies the current weapon sets and select list to XEX data.
        /// </summary>
        public void ApplyToXex(byte[] xexData, List<string>? log = null)
        {
            log?.Add("=== Applying MP Weapon Sets ===");

            if (xexData.Length < SELECT_LIST_END)
                throw new InvalidOperationException($"XEX file too small. Expected at least {SELECT_LIST_END} bytes.");

            // Write weapon sets
            for (int i = 0; i < WeaponSets.Count && i < WEAPON_SET_COUNT; i++)
            {
                int offset = WEAPONS_START + (i * WEAPON_SET_SIZE);
                var bytes = WeaponSets[i].ToBytes();
                Array.Copy(bytes, 0, xexData, offset, WEAPON_SET_SIZE);
                log?.Add($"  Written weapon set {i} at 0x{offset:X}");
            }

            // Write select list
            for (int i = 0; i < SelectList.Count && i < SELECT_LIST_COUNT; i++)
            {
                int offset = SELECT_LIST_START + (i * SELECT_ENTRY_SIZE);
                var bytes = SelectList[i].ToBytes();
                Array.Copy(bytes, 0, xexData, offset, SELECT_ENTRY_SIZE);
                log?.Add($"  Written select entry {i} at 0x{offset:X}");
            }

            log?.Add($"Applied {WeaponSets.Count} weapon sets and {SelectList.Count} select entries.");
        }

        /// <summary>
        /// Gets the weapon set associated with a select list entry.
        /// </summary>
        public WeaponSet? GetWeaponSetForSelectEntry(SelectListEntry entry)
        {
            int idx = entry.WeaponSetIndex;
            if (idx < 0 || idx >= WeaponSets.Count) return null;
            return WeaponSets[idx];
        }

        /// <summary>
        /// Generates a report of all weapon sets.
        /// </summary>
        public List<string> GenerateReport()
        {
            var report = new List<string>
            {
                "=== MP Weapon Sets Report ===",
                ""
            };

            for (int i = 0; i < SelectList.Count; i++)
            {
                var entry = SelectList[i];
                var name = i < SelectListNames.Length ? SelectListNames[i] : $"Unknown {i}";
                report.Add($"[{i}] {name}");
                report.Add($"    TextId: 0x{entry.TextId:X8}  Flags: 0x{entry.Flags:X8}");

                var weaponSet = GetWeaponSetForSelectEntry(entry);
                if (weaponSet != null)
                {
                    report.Add($"    Weapons (Set {entry.WeaponSetIndex}):");
                    for (int w = 0; w < WEAPONS_PER_SET; w++)
                    {
                        var weapon = weaponSet.Weapons[w];
                        if (weapon.WeaponId != 0)
                        {
                            report.Add($"      [{w}] {weapon}");
                        }
                    }
                }
                else
                {
                    report.Add("    (No weapon set - Random selection)");
                }
                report.Add("");
            }

            return report;
        }

        // --- Big-endian helper methods ---
        private static uint ReadU32BE(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        private static void WriteU32BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        private static string GetNameByCode((string Name, int Code)[] pairs, int code)
        {
            foreach (var pair in pairs)
            {
                if (pair.Code == code)
                    return pair.Name;
            }
            return $"Unknown (0x{code:X2})";
        }
    }
}
