using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Parses and applies 21990 patch files to GoldenEye XBLA XEX files.
    /// </summary>
    public sealed class File21990Parser
    {
        // Raw file data
        public byte[] RawData { get; private set; } = Array.Empty<byte>();
        public string FilePath { get; private set; } = string.Empty;

        // Parsed sections
        public List<PatchRecord> PatchRecords { get; } = new();
        public List<AddressPair> AddressTable { get; } = new();
        public List<StringEntry> Strings { get; } = new();
        public List<SkyEntry21990> SkyEntries { get; } = new();

        // Sky data constants - 21990 file format
        public const int SKY_DATA_START_21990 = 0x24080;
        public const int SKY_DATA_END_21990 = 0x251BF;
        public const int SKY_ENTRY_SIZE_21990 = 0x5C; // 92 bytes per entry in 21990
        public const int SKY_ENTRY_COUNT = 48;

        // Sky data constants - XEX file format
        public const int SKY_DATA_START_XEX = 0x84B860;
        public const int SKY_DATA_END_XEX = 0x84C50F;
        public const int SKY_ENTRY_SIZE_XEX = 0x38; // 56 bytes per entry in XEX

        // Patch record types
        public const byte PATCH_TYPE_09 = 0x09;
        public const byte PATCH_TYPE_0D = 0x0D;

        // XBLA Fog Ratio Table - Using correct XBLA level IDs
        // Usage: xbla_fog_new = xbla_fog / ratio (applied to Unk24/Unk2C fields at 0x24/0x2C)
        // Dividing XBLA fog by ratio makes it thicker (lower distance = closer fog)
        private static readonly Dictionary<uint, (float FarRatio, float NearRatio)> XblaFogRatios = new()
        {
            // Single Player levels (XBLA level IDs)
            { 0x09, (1.0f, 1.0f) },   // Bunker I
            { 0x14, (1.0f, 1.0f) },   // Silo
            { 0x16, (1.0f, 1.0f) },   // Statue
            { 0x17, (3.0f, 3.0f) },   // Control
            { 0x18, (2.0f, 2.0f) },   // Archives
            { 0x19, (2.5f, 2.5f) },   // Train
            { 0x1A, (1.0f, 1.0f) },   // Frigate
            { 0x1B, (3.0f, 3.0f) },   // Bunker II
            { 0x1C, (1.0f, 1.0f) },   // Aztec
            { 0x1D, (1.0f, 1.0f) },   // Streets
            { 0x1E, (2.5f, 2.5f) },   // Depot
            { 0x1F, (1.0f, 1.0f) },   // Complex
            { 0x20, (1.0f, 1.0f) },   // Egypt
            { 0x21, (1.0f, 1.0f) },   // Dam
            { 0x22, (1.0f, 1.0f) },   // Facility
            { 0x23, (1.5f, 1.75f) },  // Runway
            { 0x24, (3.0f, 3.0f) },   // Surface I
            { 0x25, (1.5f, 1.8f) },   // Jungle
            { 0x26, (1.0f, 1.0f) },   // Temple
            { 0x27, (1.0f, 1.0f) },   // Caverns
            { 0x28, (1.0f, 1.0f) },   // Citadel
            { 0x29, (1.0f, 1.5f) },   // Cradle
            { 0x2B, (3.0f, 3.0f) },   // Surface II
            { 0x2D, (1.0f, 1.0f) },   // Basement
            { 0x2E, (1.0f, 1.0f) },   // Stack
            { 0x30, (1.0f, 1.0f) },   // Library
            { 0x32, (1.0f, 1.0f) },   // Caves
            { 0x36, (1.5f, 1.8f) },   // Cuba (Credits)
        };

        /// <summary>
        /// Sky entry as stored in the 21990 file (0x5C bytes, big-endian floats).
        /// </summary>
        public sealed class SkyEntry21990
        {
            public int FileOffset { get; set; }
            public int Index { get; set; }
            public byte[] RawBytes { get; set; } = Array.Empty<byte>();

            // Parsed fields from 21990 format
            public uint LevelId { get; set; }
            public float BlendMult { get; set; }
            public float FarFog { get; set; }
            public float NearFog { get; set; }
            public float MaxObjVis { get; set; }
            public float FarObjObfuscDist { get; set; }
            public float NearObjObf { get; set; }
            public float IntensityDiff { get; set; }
            public float FarIntensity { get; set; }
            public float NearIntensity { get; set; }

            // 0x28-0x2B: Sky Colour (was incorrectly labeled as image offsets)
            public byte SkyColourRed { get; set; }
            public byte SkyColourGreen { get; set; }
            public byte SkyColourBlue { get; set; }
            public byte SkyColourFlag { get; set; }  // Often 0x01

            public float CloudHeight { get; set; }
            public uint Unk1 { get; set; }
            public float CloudsRed { get; set; }
            public float CloudsGreen { get; set; }
            public float CloudsBlue { get; set; }
            public uint Unk2 { get; set; }
            public float WaterHeight { get; set; }
            public ushort WaterImgOffset2 { get; set; }
            public ushort WaterEnable { get; set; }
            public float WaterRed { get; set; }
            public float WaterGreen { get; set; }
            public float WaterBlue { get; set; }
            public byte[] Remainder { get; set; } = new byte[4];

            public override string ToString() =>
                $"Sky[{Index:D2}] Level=0x{LevelId:X4} FarFog={FarFog:F0} " +
                $"Sky=({SkyColourRed},{SkyColourGreen},{SkyColourBlue}) " +
                $"Clouds=({CloudsRed:F0},{CloudsGreen:F0},{CloudsBlue:F0}) " +
                $"Water=({WaterRed:F0},{WaterGreen:F0},{WaterBlue:F0})";
        }

        /// <summary>
        /// Sky entry as stored in the XEX file (0x38 bytes, mixed format).
        /// </summary>
        public sealed class SkyEntryXex
        {
            // 0x00: Level ID (2 bytes)
            public ushort LevelId { get; set; }
            // 0x02: Blend Multiplier (2 bytes)
            public ushort BlendMult { get; set; }
            // 0x04: Far Fog (2 bytes)
            public ushort FarFog { get; set; }
            // 0x06: Near Fog (2 bytes)
            public ushort NearFog { get; set; }
            // 0x08: Max Obj Visibility (2 bytes)
            public ushort MaxObjVis { get; set; }
            // 0x0A: Far Obj Obfusc Dist (2 bytes)
            public ushort FarObjObfuscDist { get; set; }
            // 0x0C: Far Intensity (2 bytes)
            public ushort FarIntensity { get; set; }
            // 0x0E: Near Intensity (2 bytes)
            public ushort NearIntensity { get; set; }
            // 0x10: Sky Colour Red (1 byte)
            public byte SkyColorRed { get; set; }
            // 0x11: Sky Colour Green (1 byte)
            public byte SkyColorGreen { get; set; }
            // 0x12: Sky Colour Blue (1 byte)
            public byte SkyColorBlue { get; set; }
            // 0x13: Cloud Enable (1 byte)
            public byte CloudEnable { get; set; }
            // 0x14: Cloud/Ceiling Height (2 bytes)
            public ushort CloudHeight { get; set; }
            // 0x16: Unknown (1 byte) - water image offset?
            public byte Unk16 { get; set; }
            // 0x17: Cloud Colour Red (1 byte)
            public byte CloudColorRed { get; set; }
            // 0x18: Cloud Colour Green (1 byte)
            public byte CloudColorGreen { get; set; }
            // 0x19: Cloud Colour Blue (1 byte)
            public byte CloudColorBlue { get; set; }
            // 0x1A: Water Enable (1 byte)
            public byte WaterEnable { get; set; }
            // 0x1B: Unknown (1 byte)
            public byte Unk1B { get; set; }
            // 0x1C: Water/Bottom Height (2 bytes)
            public ushort WaterHeight { get; set; }
            // 0x1E: Water Img Offset (1 byte)
            public byte WaterImgOffset { get; set; }
            // 0x1F: Water Colour Red (1 byte)
            public byte WaterColorRed { get; set; }
            // 0x20: Water Colour Green (1 byte)
            public byte WaterColorGreen { get; set; }
            // 0x21: Water Colour Blue (1 byte)
            public byte WaterColorBlue { get; set; }
            // 0x22: No use (1 byte)
            public byte Unk22 { get; set; }
            // 0x23: Unknown (1 byte)
            public byte Unk23 { get; set; }
            // 0x24: Unknown (4 bytes) - XBLA New Near Fog?
            public uint Unk24 { get; set; }
            // 0x28: Fog Colour Red (1 byte) - NEW ONLY
            public byte FogColorRed { get; set; }
            // 0x29: Fog Colour Green (1 byte) - NEW ONLY
            public byte FogColorGreen { get; set; }
            // 0x2A: Fog Colour Blue (1 byte) - NEW ONLY
            public byte FogColorBlue { get; set; }
            // 0x2B: Unknown (1 byte)
            public byte Unk2B { get; set; }
            // 0x2C: Unknown (4 bytes) - XBLA New Far Fog?
            public uint Unk2C { get; set; }
            // 0x30: Unknown (4 bytes float) - affects clipping
            public uint Unk30 { get; set; }
            // 0x34: Unknown (4 bytes) - always B8D1B717
            public uint Unk34 { get; set; }

            /// <summary>
            /// Converts to XEX format bytes (0x38 bytes, big-endian).
            /// </summary>
            public byte[] ToBytes()
            {
                var data = new byte[SKY_ENTRY_SIZE_XEX];

                WriteU16BE(data, 0x00, LevelId);
                WriteU16BE(data, 0x02, BlendMult);
                WriteU16BE(data, 0x04, FarFog);
                WriteU16BE(data, 0x06, NearFog);
                WriteU16BE(data, 0x08, MaxObjVis);
                WriteU16BE(data, 0x0A, FarObjObfuscDist);
                WriteU16BE(data, 0x0C, FarIntensity);
                WriteU16BE(data, 0x0E, NearIntensity);
                data[0x10] = SkyColorRed;
                data[0x11] = SkyColorGreen;
                data[0x12] = SkyColorBlue;
                data[0x13] = CloudEnable;
                WriteU16BE(data, 0x14, CloudHeight);
                data[0x16] = Unk16;
                data[0x17] = CloudColorRed;
                data[0x18] = CloudColorGreen;
                data[0x19] = CloudColorBlue;
                data[0x1A] = WaterEnable;
                data[0x1B] = Unk1B;
                WriteU16BE(data, 0x1C, WaterHeight);
                data[0x1E] = WaterImgOffset;
                data[0x1F] = WaterColorRed;
                data[0x20] = WaterColorGreen;
                data[0x21] = WaterColorBlue;
                data[0x22] = Unk22;
                data[0x23] = Unk23;
                WriteU32BE(data, 0x24, Unk24);
                data[0x28] = FogColorRed;
                data[0x29] = FogColorGreen;
                data[0x2A] = FogColorBlue;
                data[0x2B] = Unk2B;
                WriteU32BE(data, 0x2C, Unk2C);
                WriteU32BE(data, 0x30, Unk30);
                WriteU32BE(data, 0x34, Unk34);

                return data;
            }

            /// <summary>
            /// Reads from XEX format bytes.
            /// </summary>
            public static SkyEntryXex FromBytes(byte[] data, int offset = 0)
            {
                return new SkyEntryXex
                {
                    LevelId = ReadU16BE(data, offset + 0x00),
                    BlendMult = ReadU16BE(data, offset + 0x02),
                    FarFog = ReadU16BE(data, offset + 0x04),
                    NearFog = ReadU16BE(data, offset + 0x06),
                    MaxObjVis = ReadU16BE(data, offset + 0x08),
                    FarObjObfuscDist = ReadU16BE(data, offset + 0x0A),
                    FarIntensity = ReadU16BE(data, offset + 0x0C),
                    NearIntensity = ReadU16BE(data, offset + 0x0E),
                    SkyColorRed = data[offset + 0x10],
                    SkyColorGreen = data[offset + 0x11],
                    SkyColorBlue = data[offset + 0x12],
                    CloudEnable = data[offset + 0x13],
                    CloudHeight = ReadU16BE(data, offset + 0x14),
                    Unk16 = data[offset + 0x16],
                    CloudColorRed = data[offset + 0x17],
                    CloudColorGreen = data[offset + 0x18],
                    CloudColorBlue = data[offset + 0x19],
                    WaterEnable = data[offset + 0x1A],
                    Unk1B = data[offset + 0x1B],
                    WaterHeight = ReadU16BE(data, offset + 0x1C),
                    WaterImgOffset = data[offset + 0x1E],
                    WaterColorRed = data[offset + 0x1F],
                    WaterColorGreen = data[offset + 0x20],
                    WaterColorBlue = data[offset + 0x21],
                    Unk22 = data[offset + 0x22],
                    Unk23 = data[offset + 0x23],
                    Unk24 = ReadU32BE(data, offset + 0x24),
                    FogColorRed = data[offset + 0x28],
                    FogColorGreen = data[offset + 0x29],
                    FogColorBlue = data[offset + 0x2A],
                    Unk2B = data[offset + 0x2B],
                    Unk2C = ReadU32BE(data, offset + 0x2C),
                    Unk30 = ReadU32BE(data, offset + 0x30),
                    Unk34 = ReadU32BE(data, offset + 0x34),
                };
            }

            private static ushort ReadU16BE(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
            private static uint ReadU32BE(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
            private static void WriteU16BE(byte[] b, int o, ushort v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
            private static void WriteU32BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        }

        /// <summary>
        /// Converts a 21990 sky entry to XEX format.
        /// </summary>
        /// <param name="src">Source 21990 entry</param>
        /// <param name="existingXex">Existing XEX entry to preserve unknown fields (optional)</param>
        /// <param name="skyColourToFog">If true, copies N64 sky colour to the XEX fog colour fields</param>
        /// <param name="applyN64FogDistances">If true, applies fog ratios to make XBLA fog thicker</param>
        public static SkyEntryXex ConvertToXexFormat(SkyEntry21990 src, SkyEntryXex? existingXex = null,
            bool skyColourToFog = false, bool applyN64FogDistances = true)
        {
            // Start with existing XEX data if provided (preserves unknown fields)
            var xex = existingXex ?? new SkyEntryXex();

            // Convert level ID (truncate to 16-bit)
            xex.LevelId = (ushort)src.LevelId;

            // Convert floats to integers
            xex.BlendMult = (ushort)Math.Clamp(src.BlendMult, 0, 65535);

            // Apply ratio to XBLA near fog (Unk24) to make it thicker if enabled
            // Keep far fog (Unk2C) unchanged to prevent skydome clipping
            if (applyN64FogDistances && existingXex != null)
            {
                // Get XBLA fog ratios for this level using XBLA level ID (default to 3.0 if not found)
                var (_, nearRatio) = XblaFogRatios.TryGetValue(existingXex.LevelId, out var ratios)
                    ? ratios
                    : (3.0f, 3.0f);

                // If ratio is 0 or invalid, use default of 3.0
                if (nearRatio <= 0) nearRatio = 3.0f;

                // Apply ratio to make fog thicker (lower distance = fog starts closer)
                var newNearFog = existingXex.Unk24 / nearRatio;
                xex.Unk24 = (uint)Math.Clamp(newNearFog, 0, uint.MaxValue);
            }

            // SKIP MaxObjVis and FarObjObfuscDist - preserve existing XEX values
            // These cause guards to render too close if overwritten
            // xex.MaxObjVis = (ushort)Math.Clamp(src.MaxObjVis, 0, 65535);
            // xex.FarObjObfuscDist = (ushort)Math.Clamp(src.FarObjObfuscDist, 0, 65535);

            // SKIP FarIntensity and NearIntensity - preserve existing XEX values
            // N64 has these as 0, XBLA has ~996/1000 - setting to 0 makes objects disappear!
            // xex.FarIntensity = (ushort)Math.Clamp(src.FarIntensity, 0, 65535);
            // xex.NearIntensity = (ushort)Math.Clamp(src.NearIntensity, 0, 65535);

            // Sky colour from 21990 (corrected - was labeled as image offsets)
            xex.SkyColorRed = src.SkyColourRed;
            xex.SkyColorGreen = src.SkyColourGreen;
            xex.SkyColorBlue = src.SkyColourBlue;

            // Cloud enable from sky colour flag
            xex.CloudEnable = src.SkyColourFlag;

            // Cloud colors (float 0-255 range to byte)
            xex.CloudColorRed = (byte)Math.Clamp(src.CloudsRed, 0, 255);
            xex.CloudColorGreen = (byte)Math.Clamp(src.CloudsGreen, 0, 255);
            xex.CloudColorBlue = (byte)Math.Clamp(src.CloudsBlue, 0, 255);

            // Cloud height (float to 16-bit)
            xex.CloudHeight = (ushort)Math.Clamp(src.CloudHeight, -32768, 32767);

            // Water colors
            xex.WaterColorRed = (byte)Math.Clamp(src.WaterRed, 0, 255);
            xex.WaterColorGreen = (byte)Math.Clamp(src.WaterGreen, 0, 255);
            xex.WaterColorBlue = (byte)Math.Clamp(src.WaterBlue, 0, 255);

            // Water height (float to signed 16-bit)
            xex.WaterHeight = (ushort)(short)Math.Clamp(src.WaterHeight, -32768, 32767);

            // Water enable
            xex.WaterEnable = (byte)(src.WaterEnable != 0 ? 1 : 0);

            // Water image offset
            xex.WaterImgOffset = src.WaterImgOffset2 > 255 ? (byte)255 : (byte)src.WaterImgOffset2;

            // SKIP XBLA New Near/Far Fog fields (0x24 and 0x2C) - preserve existing XEX values
            // Overwriting these produces graphical errors
            // xex.Unk24 = (uint)Math.Clamp(src.NearFog, 0, uint.MaxValue);  // XBLA New Near Fog
            // xex.Unk2C = (uint)Math.Clamp(src.FarFog, 0, uint.MaxValue);   // XBLA New Far Fog

            // Option: Copy N64 sky colour to fog colour (XBLA-specific fields at 0x28-0x2A)
            if (skyColourToFog)
            {
                xex.FogColorRed = src.SkyColourRed;
                xex.FogColorGreen = src.SkyColourGreen;
                xex.FogColorBlue = src.SkyColourBlue;
            }

            // Set defaults for unknown fields if not preserving existing
            if (existingXex == null)
            {
                xex.Unk30 = 0x41200000; // Common value (10.0f)
                xex.Unk34 = 0xB8D1B717; // Always this value
            }

            return xex;
        }

        /// <summary>
        /// Represents a patch record in the 21990 file.
        /// </summary>
        public sealed class PatchRecord
        {
            public int FileOffset { get; set; }
            public byte Type { get; set; }
            public byte SubType { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public string Description { get; set; } = string.Empty;

            public override string ToString() =>
                $"0x{FileOffset:X6}: Type={Type:X2} Sub={SubType:X2}";
        }

        /// <summary>
        /// Represents an address pair for relocation.
        /// </summary>
        public sealed class AddressPair
        {
            public int FileOffset { get; set; }
            public uint SourceAddress { get; set; }
            public uint DestAddress { get; set; }

            public override string ToString() =>
                $"0x{FileOffset:X6}: 0x{SourceAddress:X8} -> 0x{DestAddress:X8}";
        }

        /// <summary>
        /// Represents a string found in the 21990 file.
        /// </summary>
        public sealed class StringEntry
        {
            public int FileOffset { get; set; }
            public string Value { get; set; } = string.Empty;

            public override string ToString() =>
                $"0x{FileOffset:X6}: \"{Value}\"";
        }

        /// <summary>
        /// Loads a 21990 file from disk.
        /// </summary>
        public static File21990Parser Load(string path)
        {
            var parser = new File21990Parser
            {
                FilePath = path,
                RawData = File.ReadAllBytes(path)
            };
            parser.Parse();
            return parser;
        }

        /// <summary>
        /// Loads a 21990 file from a byte array.
        /// </summary>
        public static File21990Parser LoadFromBytes(byte[] data, string name = "memory")
        {
            var parser = new File21990Parser
            {
                FilePath = name,
                RawData = data
            };
            parser.Parse();
            return parser;
        }

        private void Parse()
        {
            PatchRecords.Clear();
            AddressTable.Clear();
            Strings.Clear();
            SkyEntries.Clear();

            if (RawData.Length == 0)
                return;

            ScanForPatchMarkers();
            ScanForAddressPairs();
            ParseSkyEntries();
        }

        private void ParseSkyEntries()
        {
            if (RawData.Length < SKY_DATA_END_21990)
                return;

            for (int i = 0; i < SKY_ENTRY_COUNT; i++)
            {
                int offset = SKY_DATA_START_21990 + (i * SKY_ENTRY_SIZE_21990);
                if (offset + SKY_ENTRY_SIZE_21990 > RawData.Length)
                    break;

                var entry = new SkyEntry21990
                {
                    FileOffset = offset,
                    Index = i,
                    RawBytes = new byte[SKY_ENTRY_SIZE_21990]
                };

                Array.Copy(RawData, offset, entry.RawBytes, 0, SKY_ENTRY_SIZE_21990);

                // Parse fields (big-endian)
                entry.LevelId = ReadU32BE(offset + 0x00);
                entry.BlendMult = ReadFloatBE(offset + 0x04);
                entry.FarFog = ReadFloatBE(offset + 0x08);
                entry.NearFog = ReadFloatBE(offset + 0x0C);
                entry.MaxObjVis = ReadFloatBE(offset + 0x10);
                entry.FarObjObfuscDist = ReadFloatBE(offset + 0x14);
                entry.NearObjObf = ReadFloatBE(offset + 0x18);
                entry.IntensityDiff = ReadFloatBE(offset + 0x1C);
                entry.FarIntensity = ReadFloatBE(offset + 0x20);
                entry.NearIntensity = ReadFloatBE(offset + 0x24);

                // 0x28-0x2B: Sky Colour (RGB + flag)
                entry.SkyColourRed = RawData[offset + 0x28];
                entry.SkyColourGreen = RawData[offset + 0x29];
                entry.SkyColourBlue = RawData[offset + 0x2A];
                entry.SkyColourFlag = RawData[offset + 0x2B];

                entry.CloudHeight = ReadFloatBE(offset + 0x2C);
                entry.Unk1 = ReadU32BE(offset + 0x30);
                entry.CloudsRed = ReadFloatBE(offset + 0x34);
                entry.CloudsGreen = ReadFloatBE(offset + 0x38);
                entry.CloudsBlue = ReadFloatBE(offset + 0x3C);
                entry.Unk2 = ReadU32BE(offset + 0x40);
                entry.WaterHeight = ReadFloatBE(offset + 0x44);
                entry.WaterImgOffset2 = ReadU16BE(offset + 0x48);
                entry.WaterEnable = ReadU16BE(offset + 0x4A);
                entry.WaterRed = ReadFloatBE(offset + 0x4C);
                entry.WaterGreen = ReadFloatBE(offset + 0x50);
                entry.WaterBlue = ReadFloatBE(offset + 0x54);
                Array.Copy(RawData, offset + 0x58, entry.Remainder, 0, 4);

                SkyEntries.Add(entry);
            }
        }

        /// <summary>
        /// Applies sky data from this 21990 file to an XEX file.
        /// Reads existing XEX sky data first to preserve unknown fields.
        /// </summary>
        /// <param name="xexData">XEX file data to patch</param>
        /// <param name="log">Log output</param>
        /// <param name="skyColourToFog">If true, copies N64 sky colour to fog colour fields</param>
        /// <param name="applyN64FogDistances">If true, applies fog ratios to make XBLA fog thicker</param>
        public int ApplySkyData(byte[] xexData, List<string> log, bool skyColourToFog = false, bool applyN64FogDistances = true)
        {
            log.Add($"=== Applying Sky Data ===");
            log.Add($"21990 sky entries: {SkyEntries.Count}");
            log.Add($"XEX sky table: 0x{SKY_DATA_START_XEX:X} - 0x{SKY_DATA_END_XEX:X}");
            log.Add($"XEX entry size: 0x{SKY_ENTRY_SIZE_XEX:X} ({SKY_ENTRY_SIZE_XEX} bytes)");
            log.Add($"N64 sky colour to fog: {(skyColourToFog ? "Yes" : "No")}");
            log.Add($"Apply N64 fog distances: {(applyN64FogDistances ? "Yes" : "No")}");

            if (SKY_DATA_START_XEX + (SKY_ENTRY_COUNT * SKY_ENTRY_SIZE_XEX) > xexData.Length)
            {
                log.Add($"ERROR: XEX too small for sky data.");
                return 0;
            }

            int patchedCount = 0;

            // Build a lookup of 21990 entries by level ID
            var skyByLevel = new Dictionary<uint, SkyEntry21990>();
            foreach (var sky in SkyEntries)
            {
                if (sky.LevelId != 0) // Skip empty entries
                    skyByLevel[sky.LevelId] = sky;
            }

            // Process each XEX sky slot
            for (int i = 0; i < SKY_ENTRY_COUNT; i++)
            {
                int xexOffset = SKY_DATA_START_XEX + (i * SKY_ENTRY_SIZE_XEX);

                // Read existing XEX entry
                var existingXex = SkyEntryXex.FromBytes(xexData, xexOffset);

                // Find matching 21990 entry by level ID
                if (skyByLevel.TryGetValue(existingXex.LevelId, out var src))
                {
                    // Capture original XBLA fog value before modification
                    var originalNearFog = existingXex.Unk24;

                    // Convert and write
                    var newXex = ConvertToXexFormat(src, existingXex, skyColourToFog, applyN64FogDistances);
                    var newBytes = newXex.ToBytes();
                    Array.Copy(newBytes, 0, xexData, xexOffset, SKY_ENTRY_SIZE_XEX);

                    // Log info
                    string fogInfo;
                    if (applyN64FogDistances)
                    {
                        var (_, nearRatio) = XblaFogRatios.TryGetValue(existingXex.LevelId, out var ratios)
                            ? ratios
                            : (3.0f, 3.0f);
                        if (nearRatio <= 0) nearRatio = 3.0f;
                        fogInfo = $"NearFog={originalNearFog}/{nearRatio:F1}→{newXex.Unk24}";
                    }
                    else
                    {
                        fogInfo = "colours only";
                    }

                    log.Add($"  [{i:D2}] Level 0x{existingXex.LevelId:X2}: {fogInfo}");
                    patchedCount++;
                }
            }

            log.Add("");
            log.Add($"Patched {patchedCount} sky entries.");
            return patchedCount;
        }

        private void ScanForPatchMarkers()
        {
            for (int i = 0; i < RawData.Length - 4; i++)
            {
                if ((RawData[i] == 0x09 || RawData[i] == 0x0D) &&
                    RawData[i + 1] == 0x00 &&
                    RawData[i + 2] == 0x04)
                {
                    PatchRecords.Add(new PatchRecord
                    {
                        FileOffset = i,
                        Type = RawData[i],
                        SubType = RawData[i + 3]
                    });
                }
            }
        }

        private void ScanForAddressPairs()
        {
            const int ADDRESS_TABLE_START = 0x22BC;
            const int ADDRESS_TABLE_SCAN_END = 0x2400;

            if (RawData.Length < ADDRESS_TABLE_SCAN_END)
                return;

            for (int i = ADDRESS_TABLE_START; i + 8 <= ADDRESS_TABLE_SCAN_END; i += 8)
            {
                uint addr1 = ReadU32BE(i);
                uint addr2 = ReadU32BE(i + 4);

                if (addr1 == 0 && addr2 == 0)
                    continue;

                if ((addr1 & 0xFF000000) == 0x80000000 ||
                    (addr2 & 0xFF000000) == 0x80000000)
                {
                    AddressTable.Add(new AddressPair
                    {
                        FileOffset = i,
                        SourceAddress = addr1,
                        DestAddress = addr2
                    });
                }
            }
        }

        /// <summary>
        /// Generates a detailed report of the 21990 file contents.
        /// </summary>
        public List<string> GenerateReport()
        {
            var report = new List<string>
            {
                $"=== 21990 File Analysis Report ===",
                $"File: {Path.GetFileName(FilePath)}",
                $"Size: {RawData.Length:N0} bytes (0x{RawData.Length:X})",
                "",
                $"=== Sky Entries ({SkyEntries.Count}) ==="
            };

            foreach (var sky in SkyEntries)
                report.Add($"  {sky}");

            report.Add("");
            report.Add($"=== Patch Records ({PatchRecords.Count}) ===");
            report.Add($"  Type 09: {PatchRecords.Count(p => p.Type == 0x09)}");
            report.Add($"  Type 0D: {PatchRecords.Count(p => p.Type == 0x0D)}");

            report.Add("");
            report.Add($"=== Address Pairs ({AddressTable.Count}) ===");
            foreach (var a in AddressTable.Take(10))
                report.Add($"  {a}");
            if (AddressTable.Count > 10)
                report.Add($"  ... and {AddressTable.Count - 10} more");

            return report;
        }

        // --- Helper methods ---
        private uint ReadU32BE(int offset)
        {
            if (offset + 4 > RawData.Length) return 0;
            return (uint)((RawData[offset] << 24) | (RawData[offset + 1] << 16) |
                          (RawData[offset + 2] << 8) | RawData[offset + 3]);
        }

        private ushort ReadU16BE(int offset)
        {
            if (offset + 2 > RawData.Length) return 0;
            return (ushort)((RawData[offset] << 8) | RawData[offset + 1]);
        }

        private float ReadFloatBE(int offset)
        {
            if (offset + 4 > RawData.Length) return 0;
            var bytes = new byte[4];
            bytes[0] = RawData[offset + 3];
            bytes[1] = RawData[offset + 2];
            bytes[2] = RawData[offset + 1];
            bytes[3] = RawData[offset];
            return BitConverter.ToSingle(bytes, 0);
        }

        public static void WriteU32BE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        public static void WriteU16BE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }
    }
}