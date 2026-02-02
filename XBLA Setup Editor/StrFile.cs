// =============================================================================
// StrFile.cs - STR/ADB String Database Parser and Serializer
// =============================================================================
// This file handles parsing and serialization of STR/ADB string database files
// used by GoldenEye 007 XBLA for localized text (menus, HUD, subtitles, etc.).
//
// FILE FORMAT OVERVIEW:
// =====================
// STR files use a custom format with these main sections:
//
// 1. HEADER (0x000 - 0x1C3):
//    - Contains chunk tags (e.g., "text") and length fields
//    - Key offsets store lengths from various points to EOF
//    - Entry count stored at 0x01C2 (big-endian U16)
//
// 2. ENTRY TABLE (variable start, ends at terminator):
//    - Each entry is 6 bytes:
//      [0-1] Bank:ID (U16BE) - Bank in high byte, ID in low byte
//      [2-3] Middle word (U16BE) - Always 0x0000 for valid entries
//      [4-5] Text offset / 2 (U16BE) - Byte offset into text section, divided by 2
//    - Terminator entry: FFFF ??00 <offset> (middle word varies: 0000, FF00, 00FF)
//
// 3. TEXT SECTION (immediately after table):
//    - UTF-16BE encoded strings
//    - Each string terminated by 00 00
//    - May have padding at the end
//
// ENCODING:
// =========
// - All multi-byte values are big-endian (Xbox 360 / N64 convention)
// - Text is UTF-16BE (Big-Endian Unicode)
// - Bank:ID pairs allow up to 256 banks × 256 IDs = 65536 strings
//
// PARSING STRATEGY:
// =================
// 1. Scan for terminator pattern (FFFF ???? followed by UTF-16BE text)
// 2. Backtrack 6 bytes at a time to find table start
// 3. Parse entries, using offsets to slice text section
// 4. Decode UTF-16BE strings
//
// SERIALIZATION:
// ==============
// 1. Preserve header bytes before table start (unknown/unused fields)
// 2. Rebuild entry table with new offsets
// 3. Rebuild text section with UTF-16BE + terminators
// 4. Patch known length fields in header
//
// NOTE: Offset 0x0163 is intentionally NOT patched - empirical testing showed
// it's not stable across files and patching it causes crashes.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XBLA_Setup_Editor
{
    // =========================================================================
    // STR ENTRY CLASS
    // =========================================================================

    /// <summary>
    /// Represents a single text entry in an STR/ADB string database.
    /// Each entry has a Bank:ID pair for identification and the text content.
    /// </summary>
    /// <remarks>
    /// Bank and ID together form a unique key for looking up strings in-game.
    /// Banks group related strings (e.g., menu text, weapon names, level names).
    /// </remarks>
    public sealed class StrEntry
    {
        /// <summary>Bank number (0-255). Groups related strings together.</summary>
        public byte Bank { get; set; }

        /// <summary>String ID within the bank (0-255). Unique within each bank.</summary>
        public byte Id { get; set; }

        /// <summary>The actual text content (UTF-16BE in the file).</summary>
        public string Text { get; set; } = "";
    }

    // =========================================================================
    // STR FILE CLASS
    // =========================================================================

    /// <summary>
    /// Parser and serializer for STR/ADB string database files.
    /// Handles reading, modifying, and saving localized text data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// STR files contain all localized text for the game including menus,
    /// weapon names, level descriptions, HUD text, and subtitles.
    /// </para>
    /// <para>
    /// The parser is designed to be robust against minor format variations
    /// by detecting the entry table dynamically rather than relying on
    /// fixed offsets.
    /// </para>
    /// </remarks>
    public sealed class StrFile
    {
        // =====================================================================
        // HEADER OFFSET CONSTANTS
        // =====================================================================
        // These are the known-stable header offsets for length fields.
        // Values at these offsets must be updated when file size changes.

        /// <summary>
        /// Offset 0x0083: Stores U16BE length from "text" chunk tag to EOF.
        /// </summary>
        private const int OFF_LEN_TEXT_TO_EOF_1 = 0x0083;

        /// <summary>
        /// Offset 0x0097: Also stores U16BE length from "text" chunk tag to EOF.
        /// (Duplicate of OFF_LEN_TEXT_TO_EOF_1, both must be patched)
        /// </summary>
        private const int OFF_LEN_TEXT_TO_EOF_2 = 0x0097;

        /// <summary>
        /// Offset 0x01BE: Stores U16BE length from this offset to EOF.
        /// </summary>
        private const int OFF_LEN_FROM_1BE_TO_EOF = 0x01BE;

        /// <summary>
        /// Offset 0x01C2: Stores U16BE entry count.
        /// </summary>
        private const int OFF_NUM_ENTRIES = 0x01C2;

        // NOTE: Offset 0x0163 intentionally NOT patched.
        // Testing showed this field is not stable and patching it causes crashes.
        // private const int OFF_LEN_TEXT_TO_END_ACTUAL_TEXT = 0x0163;

        // =====================================================================
        // PUBLIC PROPERTIES
        // =====================================================================

        /// <summary>
        /// List of all text entries in the file.
        /// Modify this list to add, remove, or edit strings.
        /// </summary>
        public List<StrEntry> Entries { get; } = new();

        /// <summary>
        /// Byte offset where the entry table begins in the file.
        /// Determined dynamically during parsing.
        /// </summary>
        public int TableStart { get; private set; }

        /// <summary>
        /// Byte offset immediately after the terminator entry.
        /// This is where the text section begins.
        /// </summary>
        public int TableEnd { get; private set; }

        /// <summary>
        /// Byte offset where the text section begins (same as TableEnd).
        /// All text offsets in entries are relative to this position.
        /// </summary>
        public int TextStart { get; private set; }

        /// <summary>
        /// Byte offset immediately after the last string's null terminator.
        /// Excludes any padding bytes that may follow.
        /// </summary>
        public int TextEndNoPadding { get; private set; }

        /// <summary>
        /// The middle word value used in the terminator entry.
        /// Preserved during serialization to maintain file compatibility.
        /// Common values: 0x0000, 0xFF00, 0x00FF
        /// </summary>
        public ushort TerminatorMiddleWord { get; private set; } = 0x0000;

        /// <summary>
        /// Original file bytes as loaded. Used to preserve header bytes
        /// and detect format variations.
        /// </summary>
        public byte[] OriginalBytes { get; private set; } = Array.Empty<byte>();

        // =====================================================================
        // LOADING
        // =====================================================================

        /// <summary>
        /// Loads and parses an STR/ADB file from disk.
        /// </summary>
        /// <param name="path">Path to the .str or .adb file.</param>
        /// <returns>Parsed StrFile instance with all entries.</returns>
        /// <exception cref="InvalidDataException">
        /// Thrown if the file format cannot be parsed.
        /// </exception>
        public static StrFile Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var sf = new StrFile { OriginalBytes = bytes };
            sf.Parse();
            return sf;
        }

        // =====================================================================
        // PARSING
        // =====================================================================

        /// <summary>
        /// Parses the loaded file bytes to extract all string entries.
        /// </summary>
        /// <remarks>
        /// Parsing strategy:
        /// 1. Find terminator entry (FFFF pattern followed by UTF-16BE text)
        /// 2. Backtrack to find table start
        /// 3. Parse each 6-byte entry to get Bank:ID and text offset
        /// 4. Use offsets to slice and decode UTF-16BE text strings
        /// </remarks>
        /// <exception cref="InvalidDataException">
        /// Thrown if terminator cannot be found or entry table is malformed.
        /// </exception>
        private void Parse()
        {
            Entries.Clear();

            // Step 1: Find the terminator entry that marks end of entry table.
            // The terminator has pattern: FF FF ?? ?? XX XX
            // Where middle word can be 0000, FF00, or 00FF
            int termPos = FindTerminatorCandidate(OriginalBytes);
            if (termPos < 0)
                throw new InvalidDataException("Could not locate entry table terminator (FFFF ???? ????).");

            // Preserve the middle word style for serialization
            TerminatorMiddleWord = ReadU16BE(OriginalBytes, termPos + 2);

            // Step 2: Walk backwards from terminator to find table start.
            // Each entry is 6 bytes, so we step back in 6-byte increments.
            int tableStart = BacktrackTableStart(OriginalBytes, termPos);

            TableStart = tableStart;
            TableEnd = termPos + 6;  // Include terminator entry
            TextStart = TableEnd;     // Text section starts immediately after

            // Step 3: Parse all entries from table
            var offsetsDiv2 = new List<int>();  // Text offsets (divided by 2)
            var banks = new List<byte>();
            var ids = new List<byte>();

            for (int p = TableStart; p < termPos; p += 6)
            {
                // Read 6-byte entry: [Bank:ID] [MiddleWord] [OffsetDiv2]
                ushort bankId = ReadU16BE(OriginalBytes, p);
                ushort mid = ReadU16BE(OriginalBytes, p + 2);
                ushort offDiv2 = ReadU16BE(OriginalBytes, p + 4);

                // Validate middle word is 0x0000 for normal entries
                if (mid != 0x0000)
                    throw new InvalidDataException($"Entry table malformed at 0x{p:X}: expected 0000 in middle word, got 0x{mid:X4}.");

                // Extract Bank (high byte) and ID (low byte)
                byte bank = (byte)(bankId >> 8);
                byte id = (byte)(bankId & 0xFF);

                banks.Add(bank);
                ids.Add(id);
                offsetsDiv2.Add(offDiv2);
            }

            // Step 4: Convert offsets and decode text strings
            // Offsets in file are divided by 2, so multiply to get byte offset
            var byteOffsets = offsetsDiv2.Select(x => x * 2).ToArray();

            // Find where text section ends (after last null terminator)
            TextEndNoPadding = FindTextEndNoPadding(OriginalBytes, TextStart, byteOffsets);

            // Decode each string using offset ranges
            for (int i = 0; i < byteOffsets.Length; i++)
            {
                int begin = TextStart + byteOffsets[i];
                // End is either next entry's offset or end of text section
                int end = (i + 1 < byteOffsets.Length) ? (TextStart + byteOffsets[i + 1]) : TextEndNoPadding;

                string s = DecodeNullTerminatedUtf16Be(OriginalBytes, begin, end);
                Entries.Add(new StrEntry { Bank = banks[i], Id = ids[i], Text = s });
            }
        }

        // =====================================================================
        // SERIALIZATION
        // =====================================================================

        /// <summary>
        /// Serializes all entries back to binary format.
        /// </summary>
        /// <returns>Complete STR file as byte array, ready to save.</returns>
        /// <remarks>
        /// The serialization process:
        /// 1. Preserve all bytes before the entry table (header)
        /// 2. Build new text blob with UTF-16BE encoding
        /// 3. Build new entry table with updated offsets
        /// 4. Concatenate: header + table + text
        /// 5. Patch length fields in header
        /// </remarks>
        public byte[] SaveToBytes()
        {
            // Keep everything before the entry table unchanged
            var prefix = OriginalBytes.Take(TableStart).ToArray();

            // Build text blob: each string as UTF-16BE followed by 00 00 terminator
            var textBytes = new List<byte>();
            var offsetsDiv2 = new List<ushort>();

            foreach (var e in Entries)
            {
                // Record offset (divided by 2) for this entry
                offsetsDiv2.Add((ushort)(textBytes.Count / 2));
                // Encode string as UTF-16BE
                textBytes.AddRange(EncodeUtf16Be(e.Text));
                // Add null terminator (00 00)
                textBytes.Add(0x00);
                textBytes.Add(0x00);
            }

            // Terminator entry points to an empty string (just 00 00)
            ushort terminatorOffsetDiv2 = (ushort)(textBytes.Count / 2);
            textBytes.Add(0x00);
            textBytes.Add(0x00);

            // Build entry table: N entries + terminator entry = (N+1) × 6 bytes
            var tableBytes = new List<byte>(Entries.Count * 6 + 6);
            for (int i = 0; i < Entries.Count; i++)
            {
                // Pack Bank:ID into U16BE (bank in high byte)
                ushort bankId = (ushort)((Entries[i].Bank << 8) | Entries[i].Id);
                WriteU16BE(tableBytes, bankId);
                WriteU16BE(tableBytes, 0x0000);  // Middle word always 0000 for entries
                WriteU16BE(tableBytes, offsetsDiv2[i]);
            }

            // Write terminator entry with preserved middle word style
            WriteU16BE(tableBytes, 0xFFFF);
            WriteU16BE(tableBytes, TerminatorMiddleWord);
            WriteU16BE(tableBytes, terminatorOffsetDiv2);

            // Concatenate all sections
            byte[] rebuilt = prefix
                .Concat(tableBytes)
                .Concat(textBytes)
                .ToArray();

            // Update length fields in header
            PatchHeaderLengthsSafe(rebuilt);

            return rebuilt;
        }

        /// <summary>
        /// Updates the known-safe length fields in the header.
        /// Only patches fields that are confirmed stable across all tested files.
        /// </summary>
        /// <param name="bytes">The rebuilt file bytes to patch.</param>
        private void PatchHeaderLengthsSafe(byte[] bytes)
        {
            int fileLen = bytes.Length;

            // Find the "text" ASCII chunk tag to calculate offsets from it
            int textPos = FindAscii(bytes, "text");
            if (textPos < 0)
            {
                // If "text" tag not found, only patch entry count to avoid corruption
                WriteU16BE_IfInBounds(bytes, OFF_NUM_ENTRIES, (ushort)Entries.Count);
                return;
            }

            // Calculate and write length from "text" tag to EOF
            ushort lenTextToEof = (ushort)(fileLen - textPos);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_TEXT_TO_EOF_1, lenTextToEof);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_TEXT_TO_EOF_2, lenTextToEof);

            // Calculate and write length from offset 0x01BE to EOF
            ushort lenFrom1beToEof = (ushort)(fileLen - OFF_LEN_FROM_1BE_TO_EOF);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_FROM_1BE_TO_EOF, lenFrom1beToEof);

            // Write entry count
            WriteU16BE_IfInBounds(bytes, OFF_NUM_ENTRIES, (ushort)Entries.Count);

            // NOTE: 0x0163 is intentionally NOT patched.
            // Testing showed it's not stable and patching causes crashes.
        }

        // =====================================================================
        // DISCOVERY HELPERS - Finding Table Structure
        // =====================================================================

        /// <summary>
        /// Scans for the terminator entry that marks the end of the entry table.
        /// </summary>
        /// <param name="b">File bytes to scan.</param>
        /// <returns>Offset of terminator entry, or -1 if not found.</returns>
        /// <remarks>
        /// Terminator pattern: FF FF ?? ?? XX XX
        /// Where middle word (??) can be:
        /// - 00 00 (standard)
        /// - FF 00 (common variant)
        /// - 00 FF (rare variant)
        ///
        /// Validated by checking that bytes after it look like UTF-16BE text
        /// (alternating 00 bytes typical of ASCII in big-endian Unicode).
        /// </remarks>
        private static int FindTerminatorCandidate(byte[] b)
        {
            // Need at least 10 bytes: 6 for terminator + 4 for text heuristic
            for (int p = 0; p <= b.Length - 10; p++)
            {
                // Look for FFFF marker
                if (b[p] == 0xFF && b[p + 1] == 0xFF)
                {
                    // Validate middle word is an acceptable pattern
                    bool okMiddle =
                        (b[p + 2] == 0x00 && b[p + 3] == 0x00) ||  // Standard
                        (b[p + 2] == 0xFF && b[p + 3] == 0x00) ||  // Common variant
                        (b[p + 2] == 0x00 && b[p + 3] == 0xFF);    // Rare variant

                    if (!okMiddle) continue;

                    int textStart = p + 6;

                    // Heuristic: UTF-16BE ASCII text has 00 in every other byte
                    // Check for pattern: 00 xx 00 yy (typical for ASCII chars)
                    if (textStart + 4 <= b.Length &&
                        b[textStart] == 0x00 &&
                        b[textStart + 2] == 0x00)
                    {
                        return p;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Walks backward from terminator to find the start of the entry table.
        /// </summary>
        /// <param name="b">File bytes.</param>
        /// <param name="terminatorPos">Position of terminator entry.</param>
        /// <returns>Offset where entry table begins.</returns>
        /// <remarks>
        /// Entry validation rules:
        /// - Middle word must be 0x0000
        /// - Bank:ID must not be 0xFFFF (that's terminator)
        /// - Offsets should be in ascending order (or at least not decreasing)
        /// </remarks>
        private static int BacktrackTableStart(byte[] b, int terminatorPos)
        {
            int p = terminatorPos;
            ushort lastOff = 0xFFFF;

            while (p - 6 >= 0)
            {
                int prev = p - 6;

                // Read previous entry
                ushort bankId = ReadU16BE(b, prev);
                ushort mid = ReadU16BE(b, prev + 2);
                ushort off = ReadU16BE(b, prev + 4);

                // Stop if middle word is not 0x0000 (not a valid entry)
                if (mid != 0x0000) break;

                // Stop if we hit another FFFF marker (shouldn't happen)
                if (bankId == 0xFFFF) break;

                // Stop if offsets are out of order (went too far back)
                if (lastOff != 0xFFFF && off > lastOff) break;

                lastOff = off;
                p = prev;
            }

            return p;
        }

        /// <summary>
        /// Finds the end of the text section (after last string's null terminator).
        /// </summary>
        /// <param name="b">File bytes.</param>
        /// <param name="textStart">Start of text section.</param>
        /// <param name="entryByteOffsets">Byte offsets for each entry.</param>
        /// <returns>Offset immediately after last null terminator.</returns>
        private static int FindTextEndNoPadding(byte[] b, int textStart, int[] entryByteOffsets)
        {
            if (entryByteOffsets.Length == 0) return textStart;

            // Find start of last string
            int lastStart = textStart + entryByteOffsets.Last();
            int p = lastStart;

            // Scan for 00 00 null terminator
            while (p + 1 < b.Length)
            {
                if (b[p] == 0x00 && b[p + 1] == 0x00)
                    return p + 2;  // Include the terminator
                p += 2;  // Step by 2 (UTF-16 code unit)
            }
            return b.Length;
        }

        /// <summary>
        /// Finds an ASCII string in a byte array.
        /// Used to locate the "text" chunk tag in the header.
        /// </summary>
        /// <param name="haystack">Bytes to search.</param>
        /// <param name="needle">ASCII string to find.</param>
        /// <returns>Offset of the string, or -1 if not found.</returns>
        private static int FindAscii(byte[] haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return -1;
            var n = Encoding.ASCII.GetBytes(needle);

            for (int i = 0; i <= haystack.Length - n.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < n.Length; j++)
                {
                    if (haystack[i + j] != n[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        // =====================================================================
        // ENCODING / DECODING HELPERS
        // =====================================================================

        /// <summary>
        /// Decodes a null-terminated UTF-16BE string from the file.
        /// </summary>
        /// <param name="b">File bytes.</param>
        /// <param name="start">Start offset of string.</param>
        /// <param name="end">Maximum end offset (may contain next string).</param>
        /// <returns>Decoded string.</returns>
        private static string DecodeNullTerminatedUtf16Be(byte[] b, int start, int end)
        {
            var bytes = new List<byte>();

            // Read UTF-16 code units until null terminator or end
            for (int p = start; p + 1 < end; p += 2)
            {
                byte hi = b[p];
                byte lo = b[p + 1];

                // Stop at null terminator (00 00)
                if (hi == 0x00 && lo == 0x00)
                    break;

                bytes.Add(hi);
                bytes.Add(lo);
            }

            return Encoding.BigEndianUnicode.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Encodes a string to UTF-16BE bytes.
        /// </summary>
        /// <param name="s">String to encode.</param>
        /// <returns>UTF-16BE byte array (without null terminator).</returns>
        private static byte[] EncodeUtf16Be(string s)
            => Encoding.BigEndianUnicode.GetBytes(s ?? "");

        // =====================================================================
        // ENDIAN HELPERS - Big-Endian U16 Operations
        // =====================================================================

        /// <summary>
        /// Reads a big-endian unsigned 16-bit integer.
        /// </summary>
        /// <param name="b">Source bytes.</param>
        /// <param name="o">Offset to read from.</param>
        /// <returns>The U16 value.</returns>
        private static ushort ReadU16BE(byte[] b, int o)
            => (ushort)((b[o] << 8) | b[o + 1]);

        /// <summary>
        /// Writes a big-endian unsigned 16-bit integer to a list.
        /// </summary>
        /// <param name="dst">Destination list.</param>
        /// <param name="v">Value to write.</param>
        private static void WriteU16BE(List<byte> dst, ushort v)
        {
            dst.Add((byte)(v >> 8));    // High byte first (big-endian)
            dst.Add((byte)(v & 0xFF));  // Low byte second
        }

        /// <summary>
        /// Writes a big-endian unsigned 16-bit integer to an array if in bounds.
        /// Safely handles edge cases where offset might be near end of file.
        /// </summary>
        /// <param name="b">Destination array.</param>
        /// <param name="o">Offset to write at.</param>
        /// <param name="v">Value to write.</param>
        private static void WriteU16BE_IfInBounds(byte[] b, int o, ushort v)
        {
            if (o < 0 || o + 1 >= b.Length) return;
            b[o] = (byte)(v >> 8);
            b[o + 1] = (byte)(v & 0xFF);
        }
    }
}
