// =============================================================================
// XEXArmorRemover.cs - Body Armor Object Remover for XEX Files
// =============================================================================
// This utility scans the embedded setup data region in GoldenEye XBLA XEX files
// and removes (NOPs/zeroes out) all Body Armor pickup objects.
//
// PURPOSE:
// ========
// In multiplayer, Body Armor pickups can significantly affect game balance.
// This tool allows modders to remove all armor spawns from levels without
// manually editing each setup file.
//
// HOW IT WORKS:
// =============
// 1. Scans the XEX setup region (0xC7DF38 - 0xDDFF5F) for armor object headers
// 2. Identifies armor objects by their signature: 00 01 80 00 15 00
//    - 00 01 80 00 = Object header fields
//    - 15 00 = Object type 0x15 (Body Armor) in little-endian
// 3. Additional validation checks for image bytes (0x73 or 0x74)
// 4. Overwrites each found armor record (136 bytes) with zeros
//
// FILE SIZE:
// ==========
// The XEX file size remains unchanged - objects are simply zeroed in place.
// This is safe because the game ignores zeroed object records.
//
// OBJECT STRUCTURE:
// =================
// Each armor object record is 0x88 (136) bytes:
// [0-3]  Header fields (00 01 80 00)
// [4-5]  Object type (15 00 = armor)
// [6]    Image byte (0x73 or 0x74)
// [7]    Preset/variant byte
// [8-135] Position, rotation, and other object data
// =============================================================================

using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Removes PD/GE "Body Armor" objects (Object Type 0x15) from the embedded setup block region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Confirmed by BEFORE/AFTER setup file diffs:
    /// Armor object records begin with: 00 01 80 00 15 00
    /// Typical follows: 73/74 ?? ...
    /// Record stride/size: 0x88 bytes (136)
    /// </para>
    /// </remarks>
    public static class XEXArmorRemover
    {
        // =====================================================================
        // XEX REGION CONSTANTS
        // =====================================================================

        /// <summary>
        /// Start of the setup data region in the XEX file.
        /// All embedded level setup data begins at this offset.
        /// </summary>
        public const int SETUP_REGION_START = 0xC7DF38;

        /// <summary>
        /// End of the setup data region in the XEX file.
        /// Scanning stops at this offset.
        /// </summary>
        public const int SETUP_REGION_END = 0xDDFF5F;

        // =====================================================================
        // ARMOR OBJECT CONSTANTS
        // =====================================================================

        /// <summary>
        /// Binary signature for armor object headers (6 bytes).
        /// 00 01 80 00 = Header fields
        /// 15 00 = Object type 0x15 (Body Armor) in little-endian
        /// </summary>
        private static readonly byte[] ARMOR_OBJECT_HEADER = { 0x00, 0x01, 0x80, 0x00, 0x15, 0x00 };

        /// <summary>
        /// Full size of an armor object record in bytes.
        /// Observed stride between consecutive armor objects.
        /// </summary>
        public const int ARMOR_RECORD_SIZE = 0x88;  // 136 bytes

        /// <summary>Image byte for standard body armor model.</summary>
        private const byte IMAGE_073 = 0x73;

        /// <summary>Image byte for alternate body armor model.</summary>
        private const byte IMAGE_074 = 0x74;

        // =====================================================================
        // RESULT CLASSES
        // =====================================================================

        /// <summary>
        /// Represents a single armor object found during scanning.
        /// </summary>
        public sealed class ArmorBlock
        {
            /// <summary>File offset where the armor object header begins.</summary>
            public int Offset { get; set; }

            /// <summary>Image/model byte (typically 0x73 or 0x74).</summary>
            public byte Image { get; set; }

            /// <summary>Preset/variant byte at offset +7.</summary>
            public byte PresetHi { get; set; }
        }

        /// <summary>
        /// Contains the results of an armor scan operation.
        /// </summary>
        public sealed class ScanResult
        {
            /// <summary>List of all armor objects found in the XEX.</summary>
            public List<ArmorBlock> ArmorBlocks { get; set; } = new List<ArmorBlock>();

            /// <summary>Total bytes that will be zeroed if all armor is removed.</summary>
            public int TotalArmorSize => ArmorBlocks.Count * ARMOR_RECORD_SIZE;
        }

        // =====================================================================
        // SCANNING
        // =====================================================================

        /// <summary>
        /// Scans the XEX file for armor objects.
        /// </summary>
        /// <param name="xexData">The XEX file bytes to scan.</param>
        /// <param name="log">Optional log list for operation details.</param>
        /// <returns>ScanResult containing all found armor objects.</returns>
        /// <remarks>
        /// The scan searches for the ARMOR_OBJECT_HEADER signature within
        /// the setup region, then validates the image byte is 0x73 or 0x74
        /// to reduce false positives.
        /// </remarks>
        public static ScanResult ScanForArmor(byte[] xexData, List<string>? log = null)
        {
            log?.Add("=== Scanning for Armor Objects (Type 0x15) ===");
            log?.Add($"Scan range: 0x{SETUP_REGION_START:X6} - 0x{SETUP_REGION_END:X6}");
            log?.Add($"Signature : {BitConverter.ToString(ARMOR_OBJECT_HEADER).Replace("-", " ")}");
            log?.Add($"Record size: 0x{ARMOR_RECORD_SIZE:X} ({ARMOR_RECORD_SIZE} bytes)");
            log?.Add("");

            var result = new ScanResult();

            // Validate input
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

            // Calculate scan bounds
            int start = Math.Max(0, SETUP_REGION_START);
            int end = Math.Min(SETUP_REGION_END, xexData.Length - ARMOR_OBJECT_HEADER.Length);

            // Scan for armor objects
            for (int i = start; i <= end; i++)
            {
                // Check for signature match
                if (!IsMatch(xexData, i, ARMOR_OBJECT_HEADER))
                    continue;

                // Ensure a full record exists
                if (i + ARMOR_RECORD_SIZE > xexData.Length)
                    continue;

                // Read image byte at +6 and preset byte at +7
                byte image = xexData[i + 6];
                byte presetHi = xexData[i + 7];

                // Validate image byte (must be 0x73 or 0x74 for armor)
                if (!(image == IMAGE_073 || image == IMAGE_074))
                    continue;

                // Found a valid armor object
                result.ArmorBlocks.Add(new ArmorBlock
                {
                    Offset = i,
                    Image = image,
                    PresetHi = presetHi
                });

                // Log first 25 finds
                if (log != null && result.ArmorBlocks.Count <= 25)
                    log.Add($"  [{result.ArmorBlocks.Count}] 0x{i:X6}: Type=0x15, Image=0x{image:X2}, +7=0x{presetHi:X2}");
            }

            // Log overflow indicator
            if (result.ArmorBlocks.Count > 25 && log != null)
                log.Add($"  ... and {result.ArmorBlocks.Count - 25} more");

            // Summary
            log?.Add("");
            log?.Add($"Found {result.ArmorBlocks.Count} armor objects.");
            log?.Add($"Total bytes to NOP: {result.TotalArmorSize:N0} bytes ({result.ArmorBlocks.Count} × {ARMOR_RECORD_SIZE})");

            return result;
        }

        // =====================================================================
        // REMOVAL
        // =====================================================================

        /// <summary>
        /// Removes all armor objects from the XEX by zeroing their records.
        /// </summary>
        /// <param name="xexData">The original XEX data.</param>
        /// <param name="scanResult">Results from a previous ScanForArmor call.</param>
        /// <param name="log">Optional log list for operation details.</param>
        /// <returns>New byte array with armor objects zeroed out.</returns>
        /// <remarks>
        /// Creates a copy of the XEX data and zeros each armor record.
        /// The file size remains unchanged - objects are NOPed in place.
        /// The game ignores zeroed object records, effectively removing them.
        /// </remarks>
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

            // Create a copy of the XEX data
            byte[] newData = new byte[xexData.Length];
            Array.Copy(xexData, newData, xexData.Length);

            int nopped = 0;
            foreach (var a in scanResult.ArmorBlocks)
            {
                // Safety: don't run off end of file
                int max = Math.Min(ARMOR_RECORD_SIZE, newData.Length - a.Offset);
                if (max <= 0) continue;

                // Zero out the entire armor record
                for (int j = 0; j < max; j++)
                    newData[a.Offset + j] = 0x00;

                nopped++;

                // Log first 10 removals
                if (log != null && nopped <= 10)
                    log.Add($"  NOPed 0x{a.Offset:X6}: (record {max} bytes)");
            }

            // Log overflow indicator
            if (scanResult.ArmorBlocks.Count > 10 && log != null)
                log.Add($"  ... NOPed {scanResult.ArmorBlocks.Count - 10} more records");

            // Summary
            log?.Add($"Done. NOPed {nopped} records.");
            log?.Add($"File size: {newData.Length:N0} bytes (unchanged)");

            return newData;
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        /// <summary>
        /// Checks if a byte sequence matches at the given offset.
        /// </summary>
        /// <param name="data">Data to search in.</param>
        /// <param name="offset">Offset to check at.</param>
        /// <param name="needle">Byte sequence to match.</param>
        /// <returns>True if the needle matches at offset.</returns>
        private static bool IsMatch(byte[] data, int offset, byte[] needle)
        {
            if (offset < 0 || offset + needle.Length > data.Length) return false;
            for (int i = 0; i < needle.Length; i++)
                if (data[offset + i] != needle[i]) return false;
            return true;
        }
    }
}
