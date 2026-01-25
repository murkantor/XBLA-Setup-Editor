using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Removes PD/GE "Body Armor" objects (Object Type 0x15) from the embedded setup block region.
    ///
    /// Confirmed by BEFORE/AFTER setup file diffs:
    ///   Armor object records begin with: 00 01 80 00 15 00
    ///   Typical follows:                73/74 ?? ...
    ///   Record stride/size:             0x88 bytes (136)
    /// </summary>
    public static class XEXArmorRemover
    {
        // XEX setup region offsets (your stated range)
        public const int SETUP_REGION_START = 0xC7DF38;
        public const int SETUP_REGION_END = 0xDDFF5F;

        // Real armor object signature header (6 bytes)
        // 00 01 80 00 = header fields
        // 15 00       = object type 0x15 (Body Armor), little-endian 16-bit
        private static readonly byte[] ARMOR_OBJECT_HEADER = { 0x00, 0x01, 0x80, 0x00, 0x15, 0x00 };

        // Full object record size (observed stride)
        public const int ARMOR_RECORD_SIZE = 0x88;

        // Extra sanity checks from your data (optional, reduces false positives)
        private const byte IMAGE_073 = 0x73;
        private const byte IMAGE_074 = 0x74;

        public sealed class ArmorBlock
        {
            public int Offset { get; set; }
            public byte Image { get; set; }   // 0x73/0x74 typically
            public byte PresetHi { get; set; } // byte at +7 (varies across records)
        }

        public sealed class ScanResult
        {
            public List<ArmorBlock> ArmorBlocks { get; set; } = new List<ArmorBlock>();
            public int TotalArmorSize => ArmorBlocks.Count * ARMOR_RECORD_SIZE;
        }

        public static ScanResult ScanForArmor(byte[] xexData, List<string>? log = null)
        {
            log?.Add("=== Scanning for Armor Objects (Type 0x15) ===");
            log?.Add($"Scan range: 0x{SETUP_REGION_START:X6} - 0x{SETUP_REGION_END:X6}");
            log?.Add($"Signature : {BitConverter.ToString(ARMOR_OBJECT_HEADER).Replace("-", " ")}");
            log?.Add($"Record size: 0x{ARMOR_RECORD_SIZE:X} ({ARMOR_RECORD_SIZE} bytes)");
            log?.Add("");

            var result = new ScanResult();

            if (xexData == null || xexData.Length == 0)
            {
                log?.Add("ERROR: XEX data is empty.");
                return result;
            }

            if (xexData.Length <= SETUP_REGION_START)
            {
                log?.Add("ERROR: XEX too small (setup region start is beyond file size).");
                return result;
            }

            int start = Math.Max(0, SETUP_REGION_START);
            int end = Math.Min(SETUP_REGION_END, xexData.Length - ARMOR_OBJECT_HEADER.Length);

            for (int i = start; i <= end; i++)
            {
                if (!IsMatch(xexData, i, ARMOR_OBJECT_HEADER))
                    continue;

                // Ensure a full record exists before we accept it
                if (i + ARMOR_RECORD_SIZE > xexData.Length)
                    continue;

                // header(6) + image(1) at +6, preset-ish byte at +7
                byte image = xexData[i + 6];
                byte presetHi = xexData[i + 7];

                // NON-STRICT: require only 0x73/0x74 image (matches "073 Body Armor" etc)
                if (!(image == IMAGE_073 || image == IMAGE_074))
                    continue;

                result.ArmorBlocks.Add(new ArmorBlock
                {
                    Offset = i,
                    Image = image,
                    PresetHi = presetHi
                });

                if (log != null && result.ArmorBlocks.Count <= 25)
                    log.Add($"  [{result.ArmorBlocks.Count}] 0x{i:X6}: Type=0x15, Image=0x{image:X2}, +7=0x{presetHi:X2}");
            }

            if (result.ArmorBlocks.Count > 25 && log != null)
                log.Add($"  ... and {result.ArmorBlocks.Count - 25} more");

            log?.Add("");
            log?.Add($"Found {result.ArmorBlocks.Count} armor objects.");
            log?.Add($"Total bytes to NOP: {result.TotalArmorSize:N0} bytes ({result.ArmorBlocks.Count} × {ARMOR_RECORD_SIZE})");

            return result;
        }

        public static byte[] RemoveArmor(byte[] xexData, ScanResult scanResult, List<string>? log = null)
        {
            log?.Add("");
            log?.Add("=== Removing Armor Objects ===");

            if (scanResult == null || scanResult.ArmorBlocks == null || scanResult.ArmorBlocks.Count == 0)
            {
                log?.Add("No armor objects to remove.");
                return xexData;
            }

            log?.Add($"NOPing {scanResult.ArmorBlocks.Count} armor object records (0x{ARMOR_RECORD_SIZE:X} bytes each)...");
            log?.Add("(Overwriting with 0x00 bytes - file size unchanged)");

            byte[] newData = new byte[xexData.Length];
            Array.Copy(xexData, newData, xexData.Length);

            int nopped = 0;
            foreach (var a in scanResult.ArmorBlocks)
            {
                // Safety: don't run off end of file
                int max = Math.Min(ARMOR_RECORD_SIZE, newData.Length - a.Offset);
                if (max <= 0) continue;

                for (int j = 0; j < max; j++)
                    newData[a.Offset + j] = 0x00;

                nopped++;

                if (log != null && nopped <= 10)
                    log.Add($"  NOPed 0x{a.Offset:X6}: (record {max} bytes)");
            }

            if (scanResult.ArmorBlocks.Count > 10 && log != null)
                log.Add($"  ... NOPed {scanResult.ArmorBlocks.Count - 10} more records");

            log?.Add($"Done. NOPed {nopped} records.");
            log?.Add($"File size: {newData.Length:N0} bytes (unchanged)");

            return newData;
        }

        private static bool IsMatch(byte[] data, int offset, byte[] needle)
        {
            if (offset < 0 || offset + needle.Length > data.Length) return false;
            for (int i = 0; i < needle.Length; i++)
                if (data[offset + i] != needle[i]) return false;
            return true;
        }
    }
}
