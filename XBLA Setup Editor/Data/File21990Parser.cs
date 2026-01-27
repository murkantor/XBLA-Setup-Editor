using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XBLA_Setup_Editor
{
    public sealed class File21990Parser
    {
        public byte[] RawData { get; private set; } = Array.Empty<byte>();
        public string FilePath { get; private set; } = string.Empty;

        // --- PARSED COLLECTIONS ---
        public List<MenuEntry> MenuEntries { get; } = new();
        public List<SkyEntry21990> SkyEntries { get; } = new();
        public List<StageMusicEntry> MusicEntries { get; } = new();

        // --- 21990 CONSTANTS (N64 Source) ---
        public const int SKY_DATA_START_21990 = 0x24080;
        public const int SKY_ENTRY_SIZE_21990 = 0x5C;
        public const int SKY_ENTRY_COUNT = 48;
        public const int SKY_DATA_END_21990 = SKY_DATA_START_21990 + (SKY_ENTRY_COUNT * SKY_ENTRY_SIZE_21990);

        public const int MUSIC_DATA_START_21990 = 0x2DD80;
        public const int MUSIC_ENTRY_SIZE = 0x08;
        public const int MUSIC_DATA_END_21990 = 0x2DEA0;

        // --- XEX CONSTANTS (XBLA Destination) ---
        // Derived from your file breakdown
        public const int SKY_DATA_START_XEX = 0x84B860;
        public const int SKY_ENTRY_SIZE_XEX = 0x38;

        public const int MUSIC_MISSION_TABLE_OFFSET = 0xDE1A30;
        public const int MUSIC_MISSION_ENTRY_COUNT = 23;

        public const int BRIEFING_XEX_START = 0x71DF60;
        public const int BRIEFING_XEX_ENTRY_SIZE = 0x30;
        public const int BRIEFING_XEX_COUNT = 21;

        // NEW: Mission Select Menu Offset
        public const int MENU_XEX_START = 0x71E570;
        public const int MENU_XEX_END = 0x71E8B7;

        // --- HARDCODED DATA ---

        // N64 Briefing IDs (Since 21990 has 0000 padding)
        private static readonly ushort[][] N64BriefingIds = new ushort[][]
        {
            new ushort[] { 0x77F0, 0x77F1, 0x77F2, 0x77F3 }, // 00: Dam
            new ushort[] { 0x77F4, 0x77F5, 0x77F6, 0x77F7 }, // 01: Facility
            new ushort[] { 0x77F8, 0x77F9, 0x77FA, 0x77FB }, // 02: Runway
            new ushort[] { 0x77FC, 0x77FD, 0x77FE, 0x77FF }, // 03: Surface 1
            new ushort[] { 0x7800, 0x7801, 0x7802, 0x7803 }, // 04: Bunker 1
            new ushort[] { 0x7804, 0x7805, 0x7806, 0x7807 }, // 05: Silo
            new ushort[] { 0x7808, 0x7809, 0x780A, 0x780B }, // 06: Frigate
            new ushort[] { 0x780C, 0x780D, 0x780E, 0x780F }, // 07: Surface 2
            new ushort[] { 0x7810, 0x7811, 0x7812, 0x7813 }, // 08: Bunker 2
            new ushort[] { 0x7814, 0x7815, 0x7816, 0x7817 }, // 09: Statue
            new ushort[] { 0x7818, 0x7819, 0x781A, 0x781B }, // 10: Archives
            new ushort[] { 0x781C, 0x781D, 0x781E, 0x781F }, // 11: Streets
            new ushort[] { 0x7820, 0x7821, 0x7822, 0x7823 }, // 12: Depot
            new ushort[] { 0x7824, 0x7825, 0x7826, 0x7827 }, // 13: Train
            new ushort[] { 0x7828, 0x7829, 0x782A, 0x782B }, // 14: Jungle
            new ushort[] { 0x782C, 0x782D, 0x782E, 0x782F }, // 15: Control
            new ushort[] { 0x7830, 0x7831, 0x7832, 0x7833 }, // 16: Caverns
            new ushort[] { 0x7834, 0x7835, 0x7836, 0x7837 }, // 17: Cradle
            new ushort[] { 0x7838, 0x7839, 0x783A, 0x783B }, // 18: Aztec
            new ushort[] { 0x783C, 0x783D, 0x783E, 0x783F }, // 19: Egypt
            new ushort[] { 0x7840, 0x7841, 0x7842, 0x7843 }, // 20: Cuba
        };

        // Standard Fog Ratios
        private static readonly Dictionary<uint, (float FarRatio, float NearRatio)> XblaFogRatios = new()
        {
            { 0x09, (1.0f, 1.0f) }, { 0x14, (1.0f, 1.0f) }, { 0x16, (1.0f, 1.0f) }, { 0x17, (3.0f, 3.0f) },
            { 0x18, (2.0f, 2.0f) }, { 0x19, (2.5f, 2.5f) }, { 0x1A, (1.0f, 1.0f) }, { 0x1B, (3.0f, 3.0f) },
            { 0x1C, (1.0f, 1.0f) }, { 0x1D, (1.0f, 1.0f) }, { 0x1E, (2.5f, 2.5f) }, { 0x1F, (1.0f, 1.0f) },
            { 0x20, (1.0f, 1.0f) }, { 0x21, (1.0f, 1.0f) }, { 0x22, (1.0f, 1.0f) }, { 0x23, (1.5f, 1.75f) },
            { 0x24, (2.0f, 3.0f) }, { 0x25, (1.5f, 1.8f) }, { 0x26, (1.0f, 1.0f) }, { 0x27, (1.0f, 1.0f) },
            { 0x28, (1.0f, 1.0f) }, { 0x29, (1.0f, 1.5f) }, { 0x2B, (2.5f, 2.0f) }, { 0x2D, (1.0f, 1.0f) },
            { 0x2E, (1.0f, 1.0f) }, { 0x30, (1.0f, 1.0f) }, { 0x32, (1.0f, 1.0f) }, { 0x36, (1.5f, 1.8f) },
        };

        private static readonly Dictionary<uint, string> LevelNames = new()
        {
            { 0x21, "Dam" }, { 0x22, "Facility" }, { 0x23, "Runway" }, { 0x24, "Surface 1" },
            { 0x09, "Bunker 1" }, { 0x14, "Silo" }, { 0x1A, "Frigate" }, { 0x2B, "Surface 2" },
            { 0x1B, "Bunker 2" }, { 0x16, "Statue" }, { 0x18, "Archives" }, { 0x1D, "Streets" },
            { 0x1E, "Depot" }, { 0x19, "Train" }, { 0x25, "Jungle" }, { 0x17, "Control" },
            { 0x27, "Caverns" }, { 0x29, "Cradle" }, { 0x1C, "Aztec" }, { 0x20, "Egypt" }
        };

        // --- CLASSES ---

        public sealed class MenuEntry
        {
            public int FileOffset { get; set; }
            public uint LevelId { get; set; }
            public string LevelName { get; set; } = "Unknown";
            public ushort FolderTextId { get; set; }
            public ushort IconTextId { get; set; }
            public override string ToString() => $"Level {LevelId:X2} ({LevelName})";
        }

        public sealed class StageMusicEntry
        {
            public int Index { get; set; }
            public ushort LevelId { get; set; }
            public ushort MainTheme { get; set; }
            public ushort Background { get; set; }
            public ushort XTrack { get; set; }
        }

        public sealed class SkyEntry21990
        {
            public int Index { get; set; }
            public uint LevelId { get; set; }
            public float BlendMult, FarFog, NearFog, MaxObjVis, FarObjObfuscDist, NearObjObf, IntensityDiff, FarIntensity, NearIntensity;
            public byte SkyColourRed, SkyColourGreen, SkyColourBlue, SkyColourFlag;
            public float CloudHeight; public uint Unk1;
            public float CloudsRed, CloudsGreen, CloudsBlue; public uint Unk2;
            public float WaterHeight; public ushort WaterImgOffset2, WaterEnable;
            public float WaterRed, WaterGreen, WaterBlue;
            public byte[] Remainder { get; set; } = new byte[4];
        }

        public sealed class SkyEntryXex
        {
            public ushort LevelId, BlendMult, FarFog, NearFog, MaxObjVis, FarObjObfuscDist, FarIntensity, NearIntensity, CloudHeight, WaterHeight;
            public byte SkyColorRed, SkyColorGreen, SkyColorBlue, CloudEnable, Unk16, CloudColorRed, CloudColorGreen, CloudColorBlue, WaterEnable, Unk1B, WaterImgOffset, WaterColorRed, WaterColorGreen, WaterColorBlue, Unk22, Unk23, FogColorRed, FogColorGreen, FogColorBlue, Unk2B;
            public uint Unk24, Unk2C, Unk30, Unk34;

            public static SkyEntryXex FromBytes(byte[] data, int offset)
            {
                var s = new SkyEntryXex();
                s.LevelId = ReadU16BE(data, offset); s.BlendMult = ReadU16BE(data, offset + 2); s.FarFog = ReadU16BE(data, offset + 4);
                s.NearFog = ReadU16BE(data, offset + 6); s.MaxObjVis = ReadU16BE(data, offset + 8); s.FarObjObfuscDist = ReadU16BE(data, offset + 10);
                s.FarIntensity = ReadU16BE(data, offset + 12); s.NearIntensity = ReadU16BE(data, offset + 14);
                s.SkyColorRed = data[offset + 16]; s.SkyColorGreen = data[offset + 17]; s.SkyColorBlue = data[offset + 18]; s.CloudEnable = data[offset + 19];
                s.CloudHeight = ReadU16BE(data, offset + 20); s.Unk16 = data[offset + 22];
                s.CloudColorRed = data[offset + 23]; s.CloudColorGreen = data[offset + 24]; s.CloudColorBlue = data[offset + 25];
                s.WaterEnable = data[offset + 26]; s.Unk1B = data[offset + 27]; s.WaterHeight = ReadU16BE(data, offset + 28);
                s.WaterImgOffset = data[offset + 30]; s.WaterColorRed = data[offset + 31]; s.WaterColorGreen = data[offset + 32]; s.WaterColorBlue = data[offset + 33];
                s.Unk22 = data[offset + 34]; s.Unk23 = data[offset + 35]; s.Unk24 = ReadU32BE(data, offset + 36);
                s.FogColorRed = data[offset + 40]; s.FogColorGreen = data[offset + 41]; s.FogColorBlue = data[offset + 42]; s.Unk2B = data[offset + 43];
                s.Unk2C = ReadU32BE(data, offset + 44); s.Unk30 = ReadU32BE(data, offset + 48); s.Unk34 = ReadU32BE(data, offset + 52);
                return s;
            }
            public byte[] ToBytes()
            {
                var d = new byte[SKY_ENTRY_SIZE_XEX];
                WriteU16BE(d, 0, LevelId); WriteU16BE(d, 2, BlendMult); WriteU16BE(d, 4, FarFog); WriteU16BE(d, 6, NearFog);
                WriteU16BE(d, 8, MaxObjVis); WriteU16BE(d, 10, FarObjObfuscDist); WriteU16BE(d, 12, FarIntensity); WriteU16BE(d, 14, NearIntensity);
                d[16] = SkyColorRed; d[17] = SkyColorGreen; d[18] = SkyColorBlue; d[19] = CloudEnable;
                WriteU16BE(d, 20, CloudHeight); d[22] = Unk16; d[23] = CloudColorRed; d[24] = CloudColorGreen; d[25] = CloudColorBlue;
                d[26] = WaterEnable; d[27] = Unk1B; WriteU16BE(d, 28, WaterHeight);
                d[30] = WaterImgOffset; d[31] = WaterColorRed; d[32] = WaterColorGreen; d[33] = WaterColorBlue;
                d[34] = Unk22; d[35] = Unk23; WriteU32BE(d, 36, Unk24);
                d[40] = FogColorRed; d[41] = FogColorGreen; d[42] = FogColorBlue; d[43] = Unk2B;
                WriteU32BE(d, 44, Unk2C); WriteU32BE(d, 48, Unk30); WriteU32BE(d, 52, Unk34);
                return d;
            }
        }

        // --- LOADING ---
        public static File21990Parser Load(string path) { var p = new File21990Parser { FilePath = path, RawData = File.ReadAllBytes(path) }; p.Parse(); return p; }
        private void Parse() { ScanForMenuEntries(); ParseSkyEntries(); ParseMusicEntries(); }

        private void ScanForMenuEntries()
        {
            MenuEntries.Clear();
            if (RawData.Length < 0x10000) return;
            // Scan approximate menu area in N64 file
            for (int i = 0x9E00; i < 0xB000; i += 4)
            {
                uint lvl = ReadU32BE(i);
                if (LevelNames.TryGetValue(lvl, out string? name))
                {
                    int txtOff = i - 4;
                    if (txtOff < 0) continue;
                    // Read N64 Text IDs (Folder, Icon)
                    MenuEntries.Add(new MenuEntry { LevelId = lvl, LevelName = name ?? "Unk", FolderTextId = ReadU16BE(txtOff), IconTextId = ReadU16BE(txtOff + 2) });
                }
            }
        }

        private void ParseSkyEntries()
        {
            if (RawData.Length < SKY_DATA_END_21990) return;
            for (int i = 0; i < SKY_ENTRY_COUNT; i++)
            {
                int o = SKY_DATA_START_21990 + (i * SKY_ENTRY_SIZE_21990);
                if (o + SKY_ENTRY_SIZE_21990 > RawData.Length) break;
                SkyEntries.Add(new SkyEntry21990
                {
                    Index = i,
                    LevelId = ReadU32BE(o),
                    BlendMult = ReadFloatBE(o + 4),
                    FarFog = ReadFloatBE(o + 8),
                    NearFog = ReadFloatBE(o + 12),
                    MaxObjVis = ReadFloatBE(o + 16),
                    FarObjObfuscDist = ReadFloatBE(o + 20),
                    NearObjObf = ReadFloatBE(o + 24),
                    IntensityDiff = ReadFloatBE(o + 28),
                    FarIntensity = ReadFloatBE(o + 32),
                    NearIntensity = ReadFloatBE(o + 36),
                    SkyColourRed = RawData[o + 40],
                    SkyColourGreen = RawData[o + 41],
                    SkyColourBlue = RawData[o + 42],
                    SkyColourFlag = RawData[o + 43],
                    CloudHeight = ReadFloatBE(o + 44),
                    Unk1 = ReadU32BE(o + 48),
                    CloudsRed = ReadFloatBE(o + 52),
                    CloudsGreen = ReadFloatBE(o + 56),
                    CloudsBlue = ReadFloatBE(o + 60),
                    Unk2 = ReadU32BE(o + 64),
                    WaterHeight = ReadFloatBE(o + 68),
                    WaterImgOffset2 = ReadU16BE(o + 72),
                    WaterEnable = ReadU16BE(o + 74),
                    WaterRed = ReadFloatBE(o + 76),
                    WaterGreen = ReadFloatBE(o + 80),
                    WaterBlue = ReadFloatBE(o + 84)
                });
            }
        }

        private void ParseMusicEntries()
        {
            if (RawData.Length < MUSIC_DATA_END_21990) return;
            int count = (MUSIC_DATA_END_21990 - MUSIC_DATA_START_21990) / MUSIC_ENTRY_SIZE;
            for (int i = 0; i < count; i++)
            {
                int o = MUSIC_DATA_START_21990 + (i * MUSIC_ENTRY_SIZE);
                MusicEntries.Add(new StageMusicEntry { Index = i, LevelId = ReadU16BE(o), MainTheme = ReadU16BE(o + 2), Background = ReadU16BE(o + 4), XTrack = ReadU16BE(o + 6) });
            }
        }

        // --- APPLY METHODS ---

        public int ApplyMenuDataToXex(byte[] xexData, List<string> log)
        {
            log.Add($"=== Applying Menu Table Data (Offset: 0x{MENU_XEX_START:X}) ===");
            int patchedCount = 0;

            // Iterate ONLY through the known XEX Menu Table range
            for (int i = MENU_XEX_START; i < MENU_XEX_END; i += 4)
            {
                // Check if this 4-byte block matches a Level ID in our list
                uint xexLevelId = ReadU32BE(xexData, i);
                var match = MenuEntries.FirstOrDefault(m => m.LevelId == xexLevelId);

                if (match != null)
                {
                    // Found a level entry! 
                    // Structure: [Ptr 4] [Folder 2] [Icon 2] [LevelID 4]
                    // We are at [LevelID], so struct start is i - 8.
                    int structStart = i - 8;

                    if (structStart < MENU_XEX_START) continue; // Out of bounds check

                    // Write N64 IDs to XEX
                    WriteU16BE(xexData, structStart + 0x04, match.FolderTextId);
                    WriteU16BE(xexData, structStart + 0x06, match.IconTextId);

                    log.Add($"  Patched {match.LevelName} (ID {match.LevelId:X2}) at 0x{structStart:X}");
                    patchedCount++;
                }
            }
            return patchedCount;
        }

        public int ApplySkyData(byte[] xexData, List<string> log, bool skyColourToFog, bool applyN64FogDistances)
        {
            log.Add($"=== Applying Sky Data ===");
            int count = 0;
            var skyByLevel = SkyEntries.Where(s => s.LevelId != 0).ToDictionary(s => s.LevelId, s => s);
            for (int i = 0; i < SKY_ENTRY_COUNT; i++)
            {
                int o = SKY_DATA_START_XEX + (i * SKY_ENTRY_SIZE_XEX);
                var ex = SkyEntryXex.FromBytes(xexData, o);
                if (skyByLevel.TryGetValue(ex.LevelId, out var src))
                {
                    var newXex = ConvertToXexFormat(src, ex, skyColourToFog, applyN64FogDistances);
                    Array.Copy(newXex.ToBytes(), 0, xexData, o, SKY_ENTRY_SIZE_XEX);
                    count++;
                    log.Add($"  [{i:D2}] Level 0x{ex.LevelId:X2} updated.");
                }
            }
            return count;
        }

        public int ApplyMusicData(byte[] xexData, List<string> log)
        {
            log.Add($"=== Applying Music Data ===");
            int count = 0;
            var musicMap = new Dictionary<uint, StageMusicEntry>();
            foreach (var m in MusicEntries) if (m.LevelId != 0) musicMap[m.LevelId] = m;
            for (int i = 0; i < MUSIC_MISSION_ENTRY_COUNT; i++)
            {
                int o = MUSIC_MISSION_TABLE_OFFSET + (i * MUSIC_ENTRY_SIZE);
                if (o + MUSIC_ENTRY_SIZE > xexData.Length) break;
                ushort lvl = ReadU16BE(xexData, o);
                if (musicMap.TryGetValue(lvl, out var src))
                {
                    WriteU16BE(xexData, o + 2, src.MainTheme);
                    WriteU16BE(xexData, o + 4, src.Background);
                    WriteU16BE(xexData, o + 6, src.XTrack);
                    count++;
                    log.Add($"  Level 0x{lvl:X2}: Music updated.");
                }
            }
            return count;
        }

        public int ApplyMissionBriefings(byte[] xexData, List<string> log)
        {
            log.Add($"=== Applying Mission Briefings (Hardcoded N64 IDs) ===");
            int count = Math.Min(N64BriefingIds.Length, BRIEFING_XEX_COUNT);
            for (int i = 0; i < count; i++)
            {
                int o = BRIEFING_XEX_START + (i * BRIEFING_XEX_ENTRY_SIZE);
                if (o + BRIEFING_XEX_ENTRY_SIZE > xexData.Length) break;
                ushort[] ids = N64BriefingIds[i];
                WriteU16BE(xexData, o + 0, ids[0]);
                WriteU16BE(xexData, o + 2, ids[1]);
                WriteU16BE(xexData, o + 4, ids[2]);
                WriteU16BE(xexData, o + 6, ids[3]);
                log.Add($"  [{i:D2}] Wrote IDs: {ids[0]:X4}, {ids[1]:X4}...");
            }
            return count;
        }

        // --- HELPER CONVERTER ---
        public static SkyEntryXex ConvertToXexFormat(SkyEntry21990 src, SkyEntryXex? ex = null, bool skyColourToFog = false, bool applyN64FogDistances = true)
        {
            var x = ex ?? new SkyEntryXex();
            x.LevelId = (ushort)src.LevelId;
            x.BlendMult = (ushort)Math.Clamp(src.BlendMult, 0, 65535);
            if (applyN64FogDistances && ex != null)
            {
                var (_, nearRatio) = XblaFogRatios.TryGetValue(ex.LevelId, out var ratios) ? ratios : (3.0f, 3.0f);
                if (nearRatio <= 0) nearRatio = 3.0f;
                x.Unk24 = (uint)Math.Clamp(ex.Unk24 / nearRatio, 0, uint.MaxValue);
                x.FarFog = (ushort)Math.Clamp(ex.FarFog / nearRatio, 0, 65535);
                x.NearFog = (ushort)Math.Clamp(ex.NearFog / nearRatio, 0, 65535);
            }
            x.SkyColorRed = src.SkyColourRed; x.SkyColorGreen = src.SkyColourGreen; x.SkyColorBlue = src.SkyColourBlue; x.CloudEnable = src.SkyColourFlag;
            x.CloudHeight = (ushort)Math.Clamp(src.CloudHeight, -32768, 32767);
            x.CloudColorRed = (byte)src.CloudsRed; x.CloudColorGreen = (byte)src.CloudsGreen; x.CloudColorBlue = (byte)src.CloudsBlue;
            x.WaterEnable = (byte)(src.WaterEnable != 0 ? 1 : 0); x.WaterHeight = (ushort)(short)Math.Clamp(src.WaterHeight, -32768, 32767);
            x.WaterImgOffset = src.WaterImgOffset2 > 255 ? (byte)255 : (byte)src.WaterImgOffset2;
            x.WaterColorRed = (byte)src.WaterRed; x.WaterColorGreen = (byte)src.WaterGreen; x.WaterColorBlue = (byte)src.WaterBlue;
            if (skyColourToFog) { x.FogColorRed = src.SkyColourRed; x.FogColorGreen = src.SkyColourGreen; x.FogColorBlue = src.SkyColourBlue; }
            if (ex == null) { x.Unk30 = 0x41200000; x.Unk34 = 0xB8D1B717; }
            return x;
        }

        public void GenerateComparisonLog(List<string> log) { foreach (var e in MenuEntries) log.Add(e.ToString()); }

        // --- BINARY HELPERS ---
        private static ushort ReadU16BE(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
        private static uint ReadU32BE(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        private static void WriteU16BE(byte[] b, int o, ushort v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
        private static void WriteU32BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        private uint ReadU32BE(int o) { if (o + 4 > RawData.Length) return 0; return (uint)((RawData[o] << 24) | (RawData[o + 1] << 16) | (RawData[o + 2] << 8) | RawData[o + 3]); }
        private ushort ReadU16BE(int o) { if (o + 2 > RawData.Length) return 0; return (ushort)((RawData[o] << 8) | RawData[o + 1]); }
        private float ReadFloatBE(int o) { if (o + 4 > RawData.Length) return 0; var b = new byte[4]; b[0] = RawData[o + 3]; b[1] = RawData[o + 2]; b[2] = RawData[o + 1]; b[3] = RawData[o]; return BitConverter.ToSingle(b, 0); }
    }
}