// =============================================================================
// MpSetupCompactor.cs - Multiplayer Setup Region Compactor
// =============================================================================
// The XBLA XEX contains a contiguous block of multiplayer level setups
// spanning file offsets 0x84C5F0 - 0xB00AC0 (total 0x2B44D0 / 2,835,664 bytes).
//
// Some entries (Library/Basement/Stack, Citadel, Caves, Complex, Temple) are
// being removed to free space. After removal the remaining entries are shifted
// down to be contiguous again from the original base address, zeroing out the
// tail of the region that is no longer used.
//
// KNOWN LAYOUT (file offsets, confirmed contiguous):
// ===================================================
//   0x84C5F0  Library / Basement / Stack  [0x9F60]
//   0x856550  Archives                    [0x25AF0]
//   0x87C040  Control                     [0x2E380]
//   0x8AA3C0  Facility                    [0x30F80]
//   0x8DB340  Aztec                       [0x21A50]
//   0x8FCD90  Citadel                     [0x5530]
//   0x9022C0  Caverns                     [0x244F0]
//   0x9267B0  Cradle                      [0x10350]
//   0x936B00  Egypt                       [0x156B0]
//   0x94C1B0  Dam                         [0x301A0]
//   0x97C350  Depot                       [0x2C970]
//   0x9A8CC0  Frigate                     [0x2D9C0]
//   0x9D6680  Temple                      [0x4870]
//   0x9DAEF0  Jungle                      [0x150F0]
//   0x9EFFE0  Cuba                        [0xFA0]
//   0x9F0F80  Caves                       [0x6E50]
//   0x9F7DD0  Streets                     [0x19C30]
//   0xA11A00  Complex                     [0x9610]
//   0xA1B010  Runway                      [0xA3D0]
//   0xA253E0  Bunker I                    [0x10DF0]
//   0xA361D0  Bunker II                   [0x1ADA0]
//   0xA50F70  Surface I & II              [0x1C5D0]
//   0xA6D540  Silo                        [0x50F40]
//   0xABE480  Statue                      [0x220D0]
//   0xAE0550  Train                       [0x20570]
//   0xB00AC0  <end>
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace XBLA_Setup_Editor.Data
{
    // Level ID Setup table (CE XEX only)
    // 0x84AF90 - 0x84B7DF  |  38 entries × 0x38 bytes each
    // Per-entry layout:
    //   +0x00  Level ID          (uint32, BE)
    //   +0x04  Name Pointer      (uint32, BE)
    //   +0x08  Scale             (float,  BE)
    //   +0x0C  Visibility        (float,  BE)
    //   +0x10  Unused            (float,  BE)
    //   +0x14  Stans Pointer     (uint32, BE)
    //   +0x18  SP Setup Pointer  (uint32, BE)
    //   +0x1C  MP Setup Pointer  (uint32, BE)
    //   +0x20  BG Data Pointer   (uint32, BE)  <-- updated by FixBgPointers
    //   +0x24 … rest unknown/unused

    public static class MpSetupCompactor
    {
        public sealed record MpSetupEntry(string Name, int FileOffset, int Size);

        /// <summary>Known contiguous layout of the MP setup region.</summary>
        public static readonly MpSetupEntry[] KnownLayout =
        {
            new("Library / Basement / Stack", 0x84C5F0, 0x9F60),
            new("Archives",                   0x856550, 0x25AF0),
            new("Control",                    0x87C040, 0x2E380),
            new("Facility",                   0x8AA3C0, 0x30F80),
            new("Aztec",                      0x8DB340, 0x21A50),
            new("Citadel",                    0x8FCD90, 0x5530),
            new("Caverns",                    0x9022C0, 0x244F0),
            new("Cradle",                     0x9267B0, 0x10350),
            new("Egypt",                      0x936B00, 0x156B0),
            new("Dam",                        0x94C1B0, 0x301A0),
            new("Depot",                      0x97C350, 0x2C970),
            new("Frigate",                    0x9A8CC0, 0x2D9C0),
            new("Temple",                     0x9D6680, 0x4870),
            new("Jungle",                     0x9DAEF0, 0x150F0),
            new("Cuba",                       0x9EFFE0, 0xFA0),
            new("Caves",                      0x9F0F80, 0x6E50),
            new("Streets",                    0x9F7DD0, 0x19C30),
            new("Complex",                    0xA11A00, 0x9610),
            new("Runway",                     0xA1B010, 0xA3D0),
            new("Bunker I",                   0xA253E0, 0x10DF0),
            new("Bunker II",                  0xA361D0, 0x1ADA0),
            new("Surface I & II",             0xA50F70, 0x1C5D0),
            new("Silo",                       0xA6D540, 0x50F40),
            new("Statue",                     0xABE480, 0x220D0),
            new("Train",                      0xAE0550, 0x20570),
        };

        /// <summary>File offset of the first byte of the first entry.</summary>
        public const int RegionStart = 0x84C5F0;

        /// <summary>File offset one past the last byte of the last entry.</summary>
        public const int RegionEnd = 0xB00AC0;

        // --- Level ID Setup constants (CE XEX) ---
        public const int LevelIdSetupStart   = 0x84AF90;
        public const int LevelIdSetupEnd     = 0x84B7E0; // exclusive  (38 × 0x38 = 0x850)
        public const int LevelIdEntrySize    = 0x38;
        public const int LevelIdBgPtrOffset  = 0x20;

        /// <summary>The five entries removed in the default compaction pass.</summary>
        public static readonly string[] DefaultRemove =
        {
            "Library / Basement / Stack",
            "Citadel",
            "Caves",
            "Complex",
            "Temple",
        };

        /// <summary>
        /// Compacts the MP setup region by removing the specified entries and shifting
        /// the remaining ones down to fill the gaps. The tail of the region is zeroed.
        /// </summary>
        /// <param name="xex">Source XEX bytes (not mutated).</param>
        /// <param name="levelsToRemove">Names matching entries in KnownLayout to drop.</param>
        /// <param name="newLayout">New entry list with updated FileOffset values.</param>
        /// <param name="report">Human-readable log of what moved where.</param>
        /// <returns>New XEX byte array with the compacted region applied.</returns>
        public static byte[] Compact(
            byte[] xex,
            IReadOnlyCollection<string> levelsToRemove,
            out IReadOnlyList<MpSetupEntry> newLayout,
            out List<string> report)
        {
            report = new List<string>();
            var removeSet = new HashSet<string>(levelsToRemove, StringComparer.OrdinalIgnoreCase);

            var kept    = KnownLayout.Where(e => !removeSet.Contains(e.Name)).ToList();
            var removed = KnownLayout.Where(e =>  removeSet.Contains(e.Name)).ToList();

            // Validate every requested removal is actually known
            foreach (var name in levelsToRemove)
                if (!KnownLayout.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    report.Add($"WARN: '{name}' not found in KnownLayout - skipped.");

            report.Add("=== MP SETUP COMPACTION ===");
            report.Add($"Removing {removed.Count} entr{(removed.Count == 1 ? "y" : "ies")}  ({removed.Sum(r => r.Size):N0} bytes freed):");
            foreach (var r in removed)
                report.Add($"  - {r.Name,-28}  size 0x{r.Size:X5}  ({r.Size:N0} bytes)");

            report.Add("");
            report.Add($"Keeping {kept.Count} entries - new layout:");

            var result = (byte[])xex.Clone();

            // Zero the entire region first so the freed tail is clean
            Array.Clear(result, RegionStart, RegionEnd - RegionStart);

            // Write kept entries contiguously from RegionStart
            var newLayoutList = new List<MpSetupEntry>();
            int writeOffset = RegionStart;

            foreach (var entry in kept)
            {
                Buffer.BlockCopy(xex, entry.FileOffset, result, writeOffset, entry.Size);

                var newEntry = entry with { FileOffset = writeOffset };
                newLayoutList.Add(newEntry);

                int delta = writeOffset - entry.FileOffset;
                string deltaStr = delta == 0 ? "unchanged" : $"{delta:+#;-#;0} bytes";
                report.Add($"  {entry.Name,-28}  0x{entry.FileOffset:X7} -> 0x{writeOffset:X7}  ({deltaStr})");

                writeOffset += entry.Size;
            }

            int freedBytes = RegionEnd - writeOffset;
            report.Add("");
            report.Add($"Compaction complete.");
            report.Add($"  Bytes freed : {freedBytes:N0}  (0x{freedBytes:X})");
            report.Add($"  New end     : 0x{writeOffset:X7}  (was 0x{RegionEnd:X7})");

            newLayout = newLayoutList;
            return result;
        }

        /// <summary>
        /// Updates the BG Data Pointer (+0x20) in every Level ID Setup entry after
        /// compaction. Pointers that fall inside a kept entry are shifted by that
        /// entry's delta. Pointers that fall inside a removed entry are zeroed and
        /// reported as warnings. Pointers outside the MP region are left alone.
        /// </summary>
        /// <param name="xex">
        ///   Already-compacted XEX bytes — mutated in place.
        ///   (This is the array returned by <see cref="Compact"/>.)
        /// </param>
        /// <param name="newLayout">New layout returned by <see cref="Compact"/>.</param>
        /// <param name="report">Human-readable log of every pointer change.</param>
        public static void FixBgPointers(
            byte[] xex,
            IReadOnlyList<MpSetupEntry> newLayout,
            out List<string> report)
        {
            report = new List<string>();
            report.Add("=== BG POINTER FIXUP (Level ID Setup) ===");
            report.Add($"  Table : 0x{LevelIdSetupStart:X7} – 0x{LevelIdSetupEnd - 1:X7}" +
                       $"  ({(LevelIdSetupEnd - LevelIdSetupStart) / LevelIdEntrySize} entries × 0x{LevelIdEntrySize:X} bytes)");
            report.Add("");

            // Build name -> new entry lookup
            var newByName = newLayout.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            int updated = 0, zeroed = 0, unchanged = 0;
            int entryCount = (LevelIdSetupEnd - LevelIdSetupStart) / LevelIdEntrySize;

            for (int i = 0; i < entryCount; i++)
            {
                int entryOff = LevelIdSetupStart + i * LevelIdEntrySize;

                uint levelId = ReadBE32(xex, entryOff);
                if (levelId == 0) continue;

                uint bgVa = ReadBE32(xex, entryOff + LevelIdBgPtrOffset);
                if (bgVa == 0) continue;

                int bgFile = (int)(bgVa - XexSetupPatcher.VaBase);

                // Find which KnownLayout entry originally owned this file offset
                MpSetupEntry? oldEntry = null;
                foreach (var k in KnownLayout)
                {
                    if (bgFile >= k.FileOffset && bgFile < k.FileOffset + k.Size)
                    {
                        oldEntry = k;
                        break;
                    }
                }

                if (oldEntry == null)
                {
                    // BG pointer is outside the MP setup region — leave it alone
                    unchanged++;
                    continue;
                }

                if (!newByName.TryGetValue(oldEntry.Name, out var newEntry))
                {
                    // BG pointer is inside a removed entry — zero it out
                    report.Add($"  Level 0x{levelId:X2}  BG 0x{bgVa:X8}  REMOVED (in '{oldEntry.Name}') — zeroed");
                    WriteBE32(xex, entryOff + LevelIdBgPtrOffset, 0);
                    zeroed++;
                    continue;
                }

                int delta = newEntry.FileOffset - oldEntry.FileOffset;
                if (delta == 0) { unchanged++; continue; }

                uint newBgVa = (uint)(bgVa + delta);
                WriteBE32(xex, entryOff + LevelIdBgPtrOffset, newBgVa);
                report.Add($"  Level 0x{levelId:X2}  BG 0x{bgVa:X8} -> 0x{newBgVa:X8}  (delta {delta:+#;-#;0}, in '{oldEntry.Name}')");
                updated++;
            }

            report.Add("");
            report.Add($"  Updated : {updated}");
            report.Add($"  Zeroed  : {zeroed}  (pointers into removed entries)");
            report.Add($"  Skipped : {unchanged}  (outside region or already correct)");
        }

        // --- Helpers ---

        private static uint ReadBE32(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

        private static void WriteBE32(byte[] b, int o, uint v)
        {
            b[o + 0] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }
    }
}
