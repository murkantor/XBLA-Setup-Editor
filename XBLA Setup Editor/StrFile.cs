using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XBLA_Setup_Editor
{
    public sealed class StrEntry
    {
        public byte Bank { get; set; }
        public byte Id { get; set; }
        public string Text { get; set; } = "";
    }

    public sealed class StrFile
    {
        // Stable header offsets (validated against your orig.str)
        private const int OFF_LEN_TEXT_TO_EOF_1 = 0x0083;  // U16BE
        private const int OFF_LEN_TEXT_TO_EOF_2 = 0x0097;  // U16BE
        private const int OFF_LEN_FROM_1BE_TO_EOF = 0x01BE; // U16BE (len from this spot to EOF)
        private const int OFF_NUM_ENTRIES = 0x01C2;         // U16BE

        // DO NOT PATCH THIS: your file proves this is NOT stable here and patching it crashes.
        // private const int OFF_LEN_TEXT_TO_END_ACTUAL_TEXT = 0x0163;

        public List<StrEntry> Entries { get; } = new();

        // Located during parse
        public int TableStart { get; private set; }
        public int TableEnd { get; private set; }   // right after terminator entry
        public int TextStart { get; private set; }
        public int TextEndNoPadding { get; private set; } // right after last 00 00 (no padding)

        // Some files use terminator middle word 0x0000, your sample uses 0xFF00.
        public ushort TerminatorMiddleWord { get; private set; } = 0x0000;

        public byte[] OriginalBytes { get; private set; } = Array.Empty<byte>();

        public static StrFile Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var sf = new StrFile { OriginalBytes = bytes };
            sf.Parse();
            return sf;
        }

        private void Parse()
        {
            Entries.Clear();

            // Find terminator entry:
            // Accept:
            //   FF FF 00 00 ?? ??   (as per your notes)
            //   FF FF FF 00 ?? ??   (your orig/default)
            //   FF FF 00 FF ?? ??   (rare endian-ish variant)
            int termPos = FindTerminatorCandidate(OriginalBytes);
            if (termPos < 0)
                throw new InvalidDataException("Could not locate entry table terminator (FFFF ???? ????).");

            TerminatorMiddleWord = ReadU16BE(OriginalBytes, termPos + 2);

            // Walk backwards in 6-byte steps to find start of table.
            int tableStart = BacktrackTableStart(OriginalBytes, termPos);

            TableStart = tableStart;
            TableEnd = termPos + 6;
            TextStart = TableEnd;

            // Parse entries until terminator
            var offsetsDiv2 = new List<int>();
            var banks = new List<byte>();
            var ids = new List<byte>();

            for (int p = TableStart; p < termPos; p += 6)
            {
                ushort bankId = ReadU16BE(OriginalBytes, p);
                ushort mid = ReadU16BE(OriginalBytes, p + 2);
                ushort offDiv2 = ReadU16BE(OriginalBytes, p + 4);

                // In your files, normal entries have mid==0000
                if (mid != 0x0000)
                    throw new InvalidDataException($"Entry table malformed at 0x{p:X}: expected 0000 in middle word, got 0x{mid:X4}.");

                byte bank = (byte)(bankId >> 8);
                byte id = (byte)(bankId & 0xFF);

                banks.Add(bank);
                ids.Add(id);
                offsetsDiv2.Add(offDiv2);
            }

            // Determine text slices using offsets and decode
            // Offsets are from TextStart, divided by 2 => byte offset = offDiv2 * 2
            var byteOffsets = offsetsDiv2.Select(x => x * 2).ToArray();

            TextEndNoPadding = FindTextEndNoPadding(OriginalBytes, TextStart, byteOffsets);

            for (int i = 0; i < byteOffsets.Length; i++)
            {
                int begin = TextStart + byteOffsets[i];
                int end = (i + 1 < byteOffsets.Length) ? (TextStart + byteOffsets[i + 1]) : TextEndNoPadding;

                string s = DecodeNullTerminatedUtf16Be(OriginalBytes, begin, end);
                Entries.Add(new StrEntry { Bank = banks[i], Id = ids[i], Text = s });
            }
        }

        public byte[] SaveToBytes()
        {
            // Rebuild table + text blob.
            // Keep everything before TableStart unchanged.
            var prefix = OriginalBytes.Take(TableStart).ToArray();

            // Build text blob (UTF-16BE + 00 00 per entry)
            var textBytes = new List<byte>();
            var offsetsDiv2 = new List<ushort>();

            foreach (var e in Entries)
            {
                offsetsDiv2.Add((ushort)(textBytes.Count / 2)); // div2
                textBytes.AddRange(EncodeUtf16Be(e.Text));
                textBytes.Add(0x00);
                textBytes.Add(0x00);
            }

            // Terminator offset points to "last entry which is just 0000"
            ushort terminatorOffsetDiv2 = (ushort)(textBytes.Count / 2);
            textBytes.Add(0x00);
            textBytes.Add(0x00);

            // Build table: N entries + terminator
            var tableBytes = new List<byte>(Entries.Count * 6 + 6);
            for (int i = 0; i < Entries.Count; i++)
            {
                ushort bankId = (ushort)((Entries[i].Bank << 8) | Entries[i].Id);
                WriteU16BE(tableBytes, bankId);
                WriteU16BE(tableBytes, 0x0000);
                WriteU16BE(tableBytes, offsetsDiv2[i]);
            }

            // Terminator entry: keep same middle-word style as original (eg 0xFF00)
            WriteU16BE(tableBytes, 0xFFFF);
            WriteU16BE(tableBytes, TerminatorMiddleWord);
            WriteU16BE(tableBytes, terminatorOffsetDiv2);

            byte[] rebuilt = prefix
                .Concat(tableBytes)
                .Concat(textBytes)
                .ToArray();

            // Patch header (SAFE fields only)
            PatchHeaderLengthsSafe(rebuilt);

            return rebuilt;
        }

        private void PatchHeaderLengthsSafe(byte[] bytes)
        {
            int fileLen = bytes.Length;

            // Anchor is the ASCII chunk tag "text"
            int textPos = FindAscii(bytes, "text");
            if (textPos < 0)
            {
                // If not found, don't risk corrupting header; only patch entry count if possible.
                WriteU16BE_IfInBounds(bytes, OFF_NUM_ENTRIES, (ushort)Entries.Count);
                return;
            }

            // @0083 and @0097: length from "text" to EOF (U16BE)
            ushort lenTextToEof = (ushort)(fileLen - textPos);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_TEXT_TO_EOF_1, lenTextToEof);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_TEXT_TO_EOF_2, lenTextToEof);

            // @01BE: length from 0x01BE to EOF (U16BE)
            ushort lenFrom1beToEof = (ushort)(fileLen - OFF_LEN_FROM_1BE_TO_EOF);
            WriteU16BE_IfInBounds(bytes, OFF_LEN_FROM_1BE_TO_EOF, lenFrom1beToEof);

            // @01C2: number of entries (U16BE)
            WriteU16BE_IfInBounds(bytes, OFF_NUM_ENTRIES, (ushort)Entries.Count);

            // DO NOT PATCH 0x0163 here. It is not stable in your file and patching it caused crashes.
        }

        // ---------- discovery helpers ----------

        private static int FindTerminatorCandidate(byte[] b)
        {
            // Need at least a couple UTF-16BE chars after terminator to validate
            for (int p = 0; p <= b.Length - 10; p++)
            {
                if (b[p] == 0xFF && b[p + 1] == 0xFF)
                {
                    bool okMiddle =
                        (b[p + 2] == 0x00 && b[p + 3] == 0x00) ||
                        (b[p + 2] == 0xFF && b[p + 3] == 0x00) || // your file: FF FF FF 00
                        (b[p + 2] == 0x00 && b[p + 3] == 0xFF);

                    if (!okMiddle) continue;

                    int textStart = p + 6;

                    // Heuristic: after terminator should look like UTF-16BE: 00 xx 00 yy ...
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

        private static int BacktrackTableStart(byte[] b, int terminatorPos)
        {
            int p = terminatorPos;
            ushort lastOff = 0xFFFF;

            while (p - 6 >= 0)
            {
                int prev = p - 6;
                ushort bankId = ReadU16BE(b, prev);
                ushort mid = ReadU16BE(b, prev + 2);
                ushort off = ReadU16BE(b, prev + 4);

                if (mid != 0x0000) break;
                if (bankId == 0xFFFF) break;
                if (lastOff != 0xFFFF && off > lastOff) break;

                lastOff = off;
                p = prev;
            }

            return p;
        }

        private static int FindTextEndNoPadding(byte[] b, int textStart, int[] entryByteOffsets)
        {
            if (entryByteOffsets.Length == 0) return textStart;

            int lastStart = textStart + entryByteOffsets.Last();
            int p = lastStart;

            // Find the 00 00 terminator for the last string
            while (p + 1 < b.Length)
            {
                if (b[p] == 0x00 && b[p + 1] == 0x00)
                    return p + 2;
                p += 2;
            }
            return b.Length;
        }

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

        // ---------- encoding/decoding ----------

        private static string DecodeNullTerminatedUtf16Be(byte[] b, int start, int end)
        {
            var bytes = new List<byte>();
            for (int p = start; p + 1 < end; p += 2)
            {
                byte hi = b[p];
                byte lo = b[p + 1];
                if (hi == 0x00 && lo == 0x00)
                    break;

                bytes.Add(hi);
                bytes.Add(lo);
            }

            return Encoding.BigEndianUnicode.GetString(bytes.ToArray());
        }

        private static byte[] EncodeUtf16Be(string s)
            => Encoding.BigEndianUnicode.GetBytes(s ?? "");

        // ---------- endian helpers ----------

        private static ushort ReadU16BE(byte[] b, int o)
            => (ushort)((b[o] << 8) | b[o + 1]);

        private static void WriteU16BE(List<byte> dst, ushort v)
        {
            dst.Add((byte)(v >> 8));
            dst.Add((byte)(v & 0xFF));
        }

        private static void WriteU16BE_IfInBounds(byte[] b, int o, ushort v)
        {
            if (o < 0 || o + 1 >= b.Length) return;
            b[o] = (byte)(v >> 8);
            b[o + 1] = (byte)(v & 0xFF);
        }
    }
}
