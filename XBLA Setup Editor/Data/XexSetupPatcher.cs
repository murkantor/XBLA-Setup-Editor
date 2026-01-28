using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    public static class XexSetupPatcher
    {
        // --- CONSTANTS ---
        public const int SetupBlocksStart = 0x00C7DF38;
        public const int SetupBlocksEndExclusive = 0x00DDFF60;
        public const int SharedReadOnlyStart = 0x00C7DF38;
        public const int SharedReadOnlyEndExclusive = 0x00C94480;
        public const int MpHeadersStart = 0x00DB8CC0;
        public const uint VaBase = 0x8200D000u;
        public static uint FileOffsetToVa(int fileOffset) => VaBase + (uint)fileOffset;
        public const int EndOfXexDefaultStart = 0x00F1B6D0;

        // --- MENU & BRIEFING CONSTANTS ---
        private const int MENU_XEX_START = 0x71E570;
        private const int MENU_XEX_END = 0x71E8B7;

        private const int BRIEFING_XEX_START = 0x71DF60;
        private const int BRIEFING_ENTRY_SIZE = 0x30;     // 48 bytes per entry
        private const int BRIEFING_COUNT = 21;            // 21 entries (including Cuba)
        private const int BRIEFING_BLOCK_LENGTH = BRIEFING_ENTRY_SIZE * BRIEFING_COUNT; // 0x3F0 bytes

        // Map String Name -> Level ID
        private static readonly Dictionary<string, uint> LevelNameToId = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Dam", 0x21 }, { "Facility", 0x22 }, { "Runway", 0x23 }, { "Surface (1)", 0x24 },
            { "Bunker (1)", 0x09 }, { "Silo", 0x14 }, { "Frigate", 0x1A }, { "Surface (2)", 0x2B },
            { "Bunker (2)", 0x1B }, { "Statue", 0x16 }, { "Archives", 0x18 }, { "Streets", 0x1D },
            { "Depot", 0x1E }, { "Train", 0x19 }, { "Jungle", 0x25 }, { "Control", 0x17 },
            { "Caverns", 0x27 }, { "Cradle", 0x29 }, { "Aztec", 0x1C }, { "Egyptian", 0x20 },
            // Cuba (0x36) Excluded from Menu
        };

        // Map Level ID -> Image ID
        private static readonly Dictionary<uint, uint> LevelImageIds = new()
        {
            { 0x18, 0x08 }, // Archives
            { 0x17, 0x20 }, // Control
            { 0x22, 0x0C }, // Facility
            { 0x1C, 0x14 }, // Aztec
            { 0x27, 0x1C }, // Caverns
            { 0x29, 0x24 }, // Cradle
            { 0x20, 0x28 }, // Egypt
            { 0x21, 0x2C }, // Dam
            { 0x1E, 0x30 }, // Depot
            { 0x1A, 0x34 }, // Frigate
            { 0x25, 0x48 }, // Jungle
            { 0x1D, 0x64 }, // Streets
            { 0x23, 0x70 }, // Runway
            { 0x09, 0x78 }, // Bunker 1
            { 0x1B, 0x74 }, // Bunker 2
            { 0x24, 0x7C }, // Surface 1
            { 0x2B, 0x80 }, // Surface 2
            { 0x14, 0x88 }, // Silo
            { 0x16, 0x8C }, // Statue
            { 0x19, 0x90 }  // Train
        };

        // Map Level ID -> BRIEFING BASE BYTE (used to identify blocks)
        private static readonly Dictionary<uint, byte> LevelBriefBase = new()
        {
            { 0x18, 0x08 }, // Archives
            { 0x17, 0x20 }, // Control
            { 0x22, 0x0C }, // Facility
            { 0x1C, 0x14 }, // Aztec
            { 0x27, 0x1C }, // Caverns
            { 0x29, 0x24 }, // Cradle
            { 0x20, 0x28 }, // Egypt
            { 0x21, 0x2C }, // Dam
            { 0x1E, 0x30 }, // Depot
            { 0x1A, 0x34 }, // Frigate
            { 0x25, 0x48 }, // Jungle
            { 0x1D, 0x64 }, // Streets
            { 0x23, 0x70 }, // Runway
            { 0x09, 0x78 }, // Bunker 1
            { 0x1B, 0x74 }, // Bunker 2
            { 0x24, 0x7C }, // Surface 1
            { 0x2B, 0x80 }, // Surface 2
            { 0x14, 0x88 }, // Silo
            { 0x16, 0x8C }, // Statue
            { 0x19, 0x90 }  // Train
        };

        private static readonly uint[] VanillaImageOrder =
        {
            0x2C, 0x0C, 0x70, 0x7C, 0x78, 0x88, 0x34, 0x80, 0x74, 0x8C,
            0x08, 0x64, 0x30, 0x90, 0x48, 0x20, 0x1C, 0x24, 0x14, 0x28
        };

        // NOTE: we will NO LONGER rely on these hard-coded indices for copying.
        private static readonly Dictionary<uint, int> OriginalBriefingIndices = new()
        {
            { 0x21, 0 }, { 0x22, 1 }, { 0x23, 2 }, { 0x24, 3 }, { 0x09, 4 }, { 0x14, 5 },
            { 0x1A, 6 }, { 0x2B, 7 }, { 0x1B, 8 }, { 0x16, 9 }, { 0x18, 10 }, { 0x1D, 11 },
            { 0x1E, 12 }, { 0x19, 13 }, { 0x25, 14 }, { 0x17, 15 }, { 0x27, 16 }, { 0x29, 17 },
            { 0x1C, 18 }, { 0x20, 19 }, { 0x36, 20 }
        };

        public static readonly uint[] VanillaMenuOrder =
        {
            0x21, 0x22, 0x23, 0x24, 0x09, 0x14, 0x1A, 0x2B, 0x1B, 0x16,
            0x18, 0x1D, 0x1E, 0x19, 0x25, 0x17, 0x27, 0x29, 0x1C, 0x20
        };

        public sealed record SoloRegion(string Name, int OriginalFileOffset);
        public static readonly SoloRegion[] SoloRegions =
        {
            new("Archives", 0xC94480), new("Control", 0xCA4CF8), new("Facility", 0xCBB470),
            new("Aztec", 0xCCC988), new("Caverns", 0xCE2420), new("Cradle", 0xCE7B08),
            new("Egyptian", 0xCF0BD8), new("Dam", 0xD045F0), new("Depot", 0xD11A40),
            new("Frigate", 0xD1F3E8), new("Jungle", 0xD37440), new("Cuba", 0xD39898),
            new("Streets", 0xD47C40), new("Runway", 0xD4F238), new("Bunker (1)", 0xD589C0),
            new("Bunker (2)", 0xD67C10), new("Surface (1)", 0xD787E8), new("Surface (2)", 0xD86CD0),
            new("Silo", 0xD9AAC8), new("Statue", 0xDA18C0), new("Train", 0xDB4C50),
        };

        public static readonly string[] PriorityOrder =
        {
            "Dam", "Facility", "Runway", "Surface (1)", "Bunker (1)", "Silo",
            "Frigate", "Surface (2)", "Bunker (2)", "Statue", "Archives", "Streets",
            "Depot", "Train", "Jungle", "Control", "Caverns", "Cradle", "Cuba",
            "Aztec", "Egyptian",
        };

        public static readonly Dictionary<string, int> SpPointerOffsets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Bunker (1)"] = 0x0084AFA8,
            ["Silo"] = 0x0084AFE0,
            ["Statue"] = 0x0084B018,
            ["Control"] = 0x0084B050,
            ["Archives"] = 0x0084B088,
            ["Train"] = 0x0084B0C0,
            ["Frigate"] = 0x0084B0F8,
            ["Bunker (2)"] = 0x0084B130,
            ["Aztec"] = 0x0084B168,
            ["Streets"] = 0x0084B1A0,
            ["Depot"] = 0x0084B1D8,
            ["Egyptian"] = 0x0084B248,
            ["Dam"] = 0x0084B280,
            ["Facility"] = 0x0084B2B8,
            ["Runway"] = 0x0084B2F0,
            ["Surface (1)"] = 0x0084B328,
            ["Jungle"] = 0x0084B360,
            ["Caverns"] = 0x0084B3D0,
            ["Cradle"] = 0x0084B440,
            ["Surface (2)"] = 0x0084B4B0,
            ["Cuba"] = 0x0084B718,
        };

        public enum RegionKind { FixedSP, SpPool, MpPool, EndOfXex, ExtendedXex }
        public sealed record Placement(string LevelName, int FileOffset, uint NewVa, int Size, RegionKind Region, bool RequiresRepack);
        private sealed record Segment(int Start, int EndExclusive, RegionKind Kind) { public int Size => EndExclusive - Start; }

        public static int AlignUp(int v, int align) { if (align <= 1) return v; return (v + (align - 1)) & ~(align - 1); }
        public static bool Overlaps(int aStart, int aEndExclusive, int bStart, int bEndExclusive) => aStart < bEndExclusive && bStart < aEndExclusive;
        public static void WriteBE32(byte[] b, int o, uint v) { b[o + 0] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        private static uint ReadBE32(byte[] b, int o) { return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]); }
        private static void WriteBE16(byte[] b, int o, ushort v) { b[o + 0] = (byte)(v >> 8); b[o + 1] = (byte)v; }

        private static int GetOriginalFileOffset(string levelName)
        {
            var r = SoloRegions.FirstOrDefault(s => s.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            return r == null ? -1 : r.OriginalFileOffset;
        }
        private static uint GetOriginalVa(string levelName) { int off = GetOriginalFileOffset(levelName); return off < 0 ? 0 : FileOffsetToVa(off); }

        private static bool TryAlloc(ref List<Segment> segs, int size, int align, out int outStart, out RegionKind kind)
        {
            for (int idx = 0; idx < segs.Count; idx++)
            {
                var s = segs[idx]; int start = AlignUp(s.Start, align); int end = start + size;
                if (end <= s.EndExclusive)
                {
                    outStart = start; kind = s.Kind;
                    var newSegs = new List<Segment>();
                    if (s.Start < start) newSegs.Add(new Segment(s.Start, start, s.Kind));
                    if (end < s.EndExclusive) newSegs.Add(new Segment(end, s.EndExclusive, s.Kind));
                    segs.RemoveAt(idx); segs.InsertRange(idx, newSegs);
                    return true;
                }
            }
            outStart = 0; kind = default; return false;
        }

        public static IReadOnlyDictionary<string, int> ComputeFixedRegionCaps(bool allowMpSpill)
        {
            var caps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < SoloRegions.Length; i++)
            {
                var cur = SoloRegions[i]; int start = cur.OriginalFileOffset; int endExclusive;
                if (i < SoloRegions.Length - 1) endExclusive = SoloRegions[i + 1].OriginalFileOffset;
                else endExclusive = allowMpSpill ? SetupBlocksEndExclusive : MpHeadersStart;
                caps[cur.Name] = Math.Max(0, endExclusive - start);
            }
            return caps;
        }

        // --- PLANNING METHODS (Condensed) ---
        public static IReadOnlyList<Placement> PlanHybridPlacements(byte[] xex, IReadOnlyDictionary<string, int> levelToSize, IEnumerable<string> candidateLevels, bool allowMp, bool allowExtendXex, int extendChunkBytes, int align, bool forceRepack, out List<string> reportLines, out List<string> notPlaced)
        {
            reportLines = new List<string>(); notPlaced = new List<string>(); reportLines.Add("=== XEX PATCH PLAN ===");
            uint extensionBaseVa = 0; int extensionBaseFileOffset = xex.Length;
            if (allowExtendXex) { var analysis = XexExtender.Analyze(xex); if (analysis.IsValid) { extensionBaseVa = analysis.EndMemoryAddress; extensionBaseFileOffset = xex.Length; } else allowExtendXex = false; }
            var caps = ComputeFixedRegionCaps(allowMpSpill: true); var segs = new List<Segment>();
            int spFreeStart = SharedReadOnlyEndExclusive; int spFreeEnd = MpHeadersStart;
            if (spFreeEnd > spFreeStart) segs.Add(new Segment(spFreeStart, spFreeEnd, RegionKind.SpPool));
            if (allowMp) { int mpStart = MpHeadersStart; int mpEnd = SetupBlocksEndExclusive; if (mpEnd > mpStart) segs.Add(new Segment(mpStart, mpEnd, RegionKind.MpPool)); }
            var placements = new List<Placement>(); var remaining = new HashSet<string>(candidateLevels, StringComparer.OrdinalIgnoreCase);
            remaining.RemoveWhere(l => { if (!SpPointerOffsets.ContainsKey(l) || !levelToSize.TryGetValue(l, out int s) || s <= 0) return true; return false; });
            if (!forceRepack)
            {
                foreach (var level in PriorityOrder.Where(remaining.Contains))
                {
                    int size = levelToSize[level]; if (!caps.TryGetValue(level, out int cap)) cap = 0; int origOff = GetOriginalFileOffset(level); if (origOff < 0) continue;
                    if (size <= cap && (origOff + size) <= SetupBlocksEndExclusive) { placements.Add(new Placement(level, origOff, FileOffsetToVa(origOff), size, RegionKind.FixedSP, false)); remaining.Remove(level); }
                }
            }
            foreach (var p in placements.Where(p => p.Region == RegionKind.FixedSP))
            {
                int pStart = p.FileOffset; int pEnd = p.FileOffset + p.Size;
                for (int i = segs.Count - 1; i >= 0; i--)
                {
                    var s = segs[i]; if (!Overlaps(s.Start, s.EndExclusive, pStart, pEnd)) continue; segs.RemoveAt(i);
                    if (s.Start < pStart) segs.Insert(i, new Segment(s.Start, pStart, s.Kind)); if (s.EndExclusive > pEnd) segs.Add(new Segment(pEnd, s.EndExclusive, s.Kind));
                }
            }
            bool progress;
            do
            {
                progress = false;
                foreach (var level in PriorityOrder.Where(remaining.Contains).ToList())
                {
                    int size = levelToSize[level]; if (!TryAlloc(ref segs, size, align, out int off, out RegionKind kind))
                    {
                        if (allowExtendXex) { int oldLen = xex.Length; int newLen = AlignUp(oldLen + Math.Max(size, extendChunkBytes), 0x10); segs.Add(new Segment(AlignUp(oldLen, align), newLen, RegionKind.ExtendedXex)); if (!TryAlloc(ref segs, size, align, out off, out kind)) continue; } else continue;
                    }
                    uint vaNew = (kind == RegionKind.ExtendedXex && extensionBaseVa != 0) ? extensionBaseVa + (uint)(off - extensionBaseFileOffset) : FileOffsetToVa(off);
                    placements.Add(new Placement(level, off, vaNew, size, kind, vaNew != GetOriginalVa(level))); remaining.Remove(level); progress = true;
                }
            } while (progress && remaining.Count > 0);
            foreach (var l in remaining) notPlaced.Add(l);
            return placements;
        }

        public static void PlanSplitAcrossTwoXex(byte[] xex, IReadOnlyDictionary<string, int> levelToSize, bool allowMp, bool allowExtendXex, int extendChunkBytes, int align, bool forceRepack, out IReadOnlyList<Placement> placementsXex1, out List<string> rep1, out List<string> remainingLevels, out IReadOnlyList<Placement> placementsXex2, out List<string> rep2)
        {
            var allLevels = PriorityOrder.Where(l => levelToSize.ContainsKey(l) && levelToSize[l] > 0).ToList();
            int bestSplit = 0; IReadOnlyList<Placement> bestPlacements = Array.Empty<Placement>(); List<string> bestReport = new List<string>();
            for (int count = 1; count <= allLevels.Count; count++)
            {
                var candidateLevels = allLevels.Take(count).ToList();
                var testPlacements = PlanHybridPlacements(xex, levelToSize, candidateLevels, allowMp, allowExtendXex, extendChunkBytes, align, forceRepack, out var testReport, out var notPlaced);
                if (notPlaced.Count == 0) { bestSplit = count; bestPlacements = testPlacements; bestReport = testReport; } else break;
            }
            placementsXex1 = bestPlacements; rep1 = bestReport; remainingLevels = allLevels.Skip(bestSplit).ToList();
            if (remainingLevels.Count > 0) { rep1.Add(""); rep1.Add($"=== SPLIT POINT: XEX1 has {bestSplit} levels. XEX2 has {remainingLevels.Count}. ==="); }
            placementsXex2 = PlanHybridPlacements(xex, levelToSize, remainingLevels, allowMp, allowExtendXex, extendChunkBytes, align, forceRepack, out rep2, out var notPlaced2);
        }

        // =========================================================
        // APPLY METHODS
        // =========================================================

        public static void ApplyHybrid(
            string inputXexPath,
            string outputXexPath,
            IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            bool allowExtendXex,
            IEnumerable<string>? desiredMenuOrder,
            out List<string> reportLines)
        {
            ApplyHybridCore(inputXexPath, outputXexPath, placements, levelToBlob, allowExtendXex, desiredMenuOrder, out reportLines);
        }

        public static void ApplyHybrid(
            string inputXexPath,
            string outputXexPath,
            IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            bool allowExtendXex,
            out List<string> reportLines)
        {
            ApplyHybridCore(inputXexPath, outputXexPath, placements, levelToBlob, allowExtendXex, null, out reportLines);
        }

        private static void ApplyHybridCore(
            string inputXexPath,
            string outputXexPath,
            IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            bool allowExtendXex,
            IEnumerable<string>? desiredMenuOrder,
            out List<string> reportLines)
        {
            byte[] xex = File.ReadAllBytes(inputXexPath);
            int requiredLen = xex.Length;
            foreach (var p in placements) { int end = p.FileOffset + p.Size; if (end > requiredLen) requiredLen = end; }

            int extensionSize = requiredLen - xex.Length;
            if (extensionSize > 0)
            {
                if (!allowExtendXex) throw new InvalidOperationException($"Extension needed but disabled.");
                var analysis = XexExtender.Analyze(xex); if (!analysis.IsValid) throw new InvalidOperationException(analysis.Error);
                byte[] extensionData = new byte[extensionSize];
                var (extendedXex, extResult) = XexExtender.Extend(xex, extensionData, recalculateSha1: false);
                if (!extResult.Success || extendedXex == null) throw new InvalidOperationException(extResult.Error ?? "Extension failed");
                xex = extendedXex;
            }

            var patched = new List<string>();
            var warnings = new List<string>();
            var levelsBeingWritten = new List<MenuEntryInfo>();

            // 1) Write setup blobs & pointers
            foreach (var p in placements)
            {
                if (!levelToBlob.TryGetValue(p.LevelName, out var blob) || blob == null) continue;
                if (blob.Length != p.Size) warnings.Add($"WARN: {p.LevelName} size mismatch.");
                Buffer.BlockCopy(blob, 0, xex, p.FileOffset, blob.Length);
                if (SpPointerOffsets.TryGetValue(p.LevelName, out int ptrOff)) WriteBE32(xex, ptrOff, p.NewVa);
                patched.Add($"{p.LevelName,-12} -> 0x{p.FileOffset:X}");

                if (LevelNameToId.TryGetValue(p.LevelName, out uint id))
                    levelsBeingWritten.Add(new MenuEntryInfo { LevelId = id, Name = p.LevelName });
            }

            // Custom menu order (optional)
            if (desiredMenuOrder != null)
                levelsBeingWritten = ReorderLevels(levelsBeingWritten, desiredMenuOrder, patched, warnings);

            // 2) Build map: LevelId -> menu struct start (canonical destination slots)
            var menuStructById = BuildMenuStructIndex(xex);

            // 3) Buffer original menu text IDs by LevelId (so we can transplant source texts correctly)
            var textIdsByLevelId = ReadMenuTextIdsByLevelId(xex, menuStructById, warnings);

            // 4) Image table (if present)
            int imageTableOffset = FindImageTable(xex, patched, warnings);

            // 5) DISCOVER briefing table indices dynamically for this XEX
            var briefIdx = BuildBriefingIndexByLevelId(xex, patched, warnings);

            // 6) Buffer original briefing blocks for all visible source levels using discovered indices
            var origBriefByLevelId = new Dictionary<uint, byte[]>();
            foreach (var lv in levelsBeingWritten)
            {
                if (briefIdx.TryGetValue(lv.LevelId, out int srcIndex))
                {
                    int readOffset = BRIEFING_XEX_START + (srcIndex * BRIEFING_ENTRY_SIZE);
                    var buf = new byte[BRIEFING_ENTRY_SIZE];
                    Buffer.BlockCopy(xex, readOffset, buf, 0, BRIEFING_ENTRY_SIZE);
                    origBriefByLevelId[lv.LevelId] = buf;
                }
                else
                {
                    warnings.Add($"WARN: Could not discover briefing index for {lv.Name} (0x{lv.LevelId:X}).");
                }
            }

            // 7) Write menu entries INTO DESTINATION SLOTS (by VanillaMenuOrder)
            int slotsFilled = 0;
            int n = Math.Min(levelsBeingWritten.Count, VanillaMenuOrder.Length);
            for (int i = 0; i < n; i++)
            {
                uint destId = VanillaMenuOrder[i];
                if (!menuStructById.TryGetValue(destId, out int menuOffset))
                {
                    warnings.Add($"WARN: Couldn't locate destination menu struct for 0x{destId:X} (slot {i}).");
                    continue;
                }

                var srcLevel = levelsBeingWritten[i];

                // transplant text IDs from source level's original struct
                ushort folderId = 0;
                ushort iconId = 0;
                if (textIdsByLevelId.TryGetValue(srcLevel.LevelId, out var txPair))
                {
                    folderId = txPair.folder;
                    iconId = txPair.icon;
                }

                WriteBE16(xex, menuOffset + 4, folderId);
                WriteBE16(xex, menuOffset + 6, iconId);
                WriteBE32(xex, menuOffset + 8, srcLevel.LevelId);

                if (imageTableOffset != -1 && LevelImageIds.TryGetValue(srcLevel.LevelId, out uint imgId))
                {
                    // image table is exactly in Vanilla order
                    WriteBE32(xex, imageTableOffset + (i * 4), imgId);
                }

                patched.Add($"  Slot {i}: {srcLevel.Name} → dest 0x{destId:X} (Menu/Image updated)");
                slotsFilled++;
            }

            // 8) Clear remaining destination slots (menu + image)
            for (int i = n; i < VanillaMenuOrder.Length; i++)
            {
                uint destId = VanillaMenuOrder[i];
                if (!menuStructById.TryGetValue(destId, out int menuOffset)) continue;
                WriteBE16(xex, menuOffset + 4, 0);
                WriteBE16(xex, menuOffset + 6, 0);
                WriteBE32(xex, menuOffset + 8, 0);
                if (imageTableOffset != -1) WriteBE32(xex, imageTableOffset + (i * 4), 0xFFFFFFFF);
            }

            // 9) Remap briefing blocks using discovered indices (srcId block -> destId canonical idx)
            for (int i = 0; i < n; i++)
            {
                uint destId = VanillaMenuOrder[i];
                uint srcId = levelsBeingWritten[i].LevelId;

                if (!origBriefByLevelId.TryGetValue(srcId, out var block)) continue;

                if (!briefIdx.TryGetValue(destId, out int destIdx))
                {
                    warnings.Add($"WARN: Could not discover dest briefing index for 0x{destId:X}; slot {i}.");
                    continue;
                }
                int destOff = BRIEFING_XEX_START + destIdx * BRIEFING_ENTRY_SIZE;
                Buffer.BlockCopy(block, 0, xex, destOff, BRIEFING_ENTRY_SIZE);
                patched.Add($"    Briefing: 0x{srcId:X} → 0x{destId:X} @ idx {destIdx}");
            }

            patched.Add($"Packed {slotsFilled} levels. Cleared {VanillaMenuOrder.Length - slotsFilled} slots.");

            File.WriteAllBytes(outputXexPath, xex);

            reportLines = new List<string> { "=== APPLY REPORT ===" };
            reportLines.AddRange(patched.Select(x => "  " + x));
            reportLines.AddRange(warnings.Select(x => "  " + x));
        }

        // Build map of menu struct starts keyed by mission LevelId (robust against struct order)
        private static Dictionary<uint, int> BuildMenuStructIndex(byte[] xex)
        {
            var dict = new Dictionary<uint, int>();
            var validIds = new HashSet<uint>(LevelNameToId.Values);
            for (int i = MENU_XEX_START; i < MENU_XEX_END; i += 4)
            {
                uint id = ReadBE32(xex, i);
                if (id != 0 && validIds.Contains(id))
                {
                    int structStart = i - 8;
                    if (structStart >= MENU_XEX_START && xex[structStart] == 0x82)
                    {
                        dict[id] = structStart;
                    }
                }
            }
            return dict;
        }

        // Read folder/icon text IDs from each level's current menu struct
        private static Dictionary<uint, (ushort folder, ushort icon)> ReadMenuTextIdsByLevelId(byte[] xex, Dictionary<uint, int> menuStructById, List<string> warnings)
        {
            var res = new Dictionary<uint, (ushort, ushort)>();
            foreach (var kv in menuStructById)
            {
                int off = kv.Value;
                if (off + 10 >= xex.Length) { warnings.Add($"WARN: Menu struct for 0x{kv.Key:X} out of range."); continue; }
                ushort folder = (ushort)((xex[off + 4] << 8) | xex[off + 5]);
                ushort icon = (ushort)((xex[off + 6] << 8) | xex[off + 7]);
                res[kv.Key] = (folder, icon);
            }
            return res;
        }

        private static int FindImageTable(byte[] xex, List<string> patched, List<string> warnings)
        {
            int imageTableOffset = -1;
            for (int i = 0x700000; i < xex.Length - (VanillaImageOrder.Length * 4); i += 4)
            {
                bool match = true;
                for (int j = 0; j < VanillaImageOrder.Length; j++)
                {
                    if (ReadBE32(xex, i + (j * 4)) != VanillaImageOrder[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    imageTableOffset = i;
                    patched.Add($"Image Table Found at 0x{imageTableOffset:X}");
                    break;
                }
            }
            if (imageTableOffset == -1) warnings.Add("WARN: Image Table not found! Images won't be reordered.");
            return imageTableOffset;
        }

        // Dynamically discover briefing indices by reading the first byte of each 0x30 block
        private static Dictionary<uint, int> BuildBriefingIndexByLevelId(byte[] xex, List<string> patched, List<string> warnings)
        {
            var result = new Dictionary<uint, int>();
            var baseToLevel = LevelBriefBase.ToDictionary(kv => kv.Value, kv => kv.Key);
            for (int i = 0; i < BRIEFING_COUNT; i++)
            {
                int o = BRIEFING_XEX_START + i * BRIEFING_ENTRY_SIZE;
                if (o + 4 > xex.Length) break;

                byte baseByte = xex[o];
                // sanity: next two bytes usually baseByte, 00 / baseByte, 01
                if (!baseToLevel.TryGetValue(baseByte, out uint levelId))
                {
                    warnings.Add($"WARN: Briefing block idx {i}: unknown base 0x{baseByte:X2}. Skipping.");
                    continue;
                }
                result[levelId] = i;
            }
            // Log quick summary for first few indices
            int logCount = 0;
            foreach (var kv in result.OrderBy(kv => kv.Value))
            {
                patched.Add($"    BriefIdx: idx {kv.Value} -> level 0x{kv.Key:X}");
                if (++logCount >= 6) break;
            }
            return result;
        }

        private static List<MenuEntryInfo> ReorderLevels(
            List<MenuEntryInfo> current,
            IEnumerable<string> desiredMenuOrder,
            List<string> patched,
            List<string> warnings)
        {
            var orderIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int idx = 0;
            foreach (var name in desiredMenuOrder)
            {
                if (!orderIndex.ContainsKey(name)) orderIndex[name] = idx++;
            }

            var presentNames = new HashSet<string>(current.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var unknown = orderIndex.Keys.Where(n => !presentNames.Contains(n)).ToList();
            if (unknown.Count > 0)
                warnings.Add("WARN: Desired menu contains levels not present in placements: " + string.Join(", ", unknown));

            var explicitOrdered = current
                .Where(c => orderIndex.TryGetValue(c.Name, out _))
                .OrderBy(c => orderIndex[c.Name])
                .ToList();

            var theRest = current
                .Where(c => !orderIndex.ContainsKey(c.Name))
                .ToList(); // keep original relative order

            var combined = new List<MenuEntryInfo>(explicitOrdered.Count + theRest.Count);
            combined.AddRange(explicitOrdered);
            combined.AddRange(theRest);

            patched.Add($"Applied custom menu order ({explicitOrdered.Count} matched, {theRest.Count} left in original order).");
            return combined;
        }

        public static void ApplySplitHybrid(
            string inputXexPath,
            string outputXex1Path,
            string outputXex2Path,
            IReadOnlyList<Placement> placementsXex1,
            IReadOnlyList<Placement> placementsXex2,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            bool allowExtendXex,
            IEnumerable<string>? menuOrderXex1,
            IEnumerable<string>? menuOrderXex2,
            out List<string> report1,
            out List<string> report2)
        {
            ApplyHybrid(inputXexPath, outputXex1Path, placementsXex1, levelToBlob, allowExtendXex, menuOrderXex1, out var rep1);
            ApplyHybrid(inputXexPath, outputXex2Path, placementsXex2, levelToBlob, allowExtendXex, menuOrderXex2, out var rep2);

            report1 = new List<string> { "=== APPLY #1 ===" };
            report1.AddRange(rep1);
            report2 = new List<string> { "=== APPLY #2 ===" };
            report2.AddRange(rep2);
        }

        public static void PlanSplitAndApplyHybrid(
            string inputXexPath,
            string outputXex1Path,
            string outputXex2Path,
            IReadOnlyDictionary<string, int> levelToSize,
            bool allowMp,
            bool allowExtendXex,
            int extendChunkBytes,
            int align,
            bool forceRepack,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            IEnumerable<string>? menuOrderXex1,
            IEnumerable<string>? menuOrderXex2,
            out List<string> planReport1,
            out List<string> applyReport1,
            out List<string> planReport2,
            out List<string> applyReport2)
        {
            var xexBytes = File.ReadAllBytes(inputXexPath);

            PlanSplitAcrossTwoXex(
                xexBytes,
                levelToSize,
                allowMp,
                allowExtendXex,
                extendChunkBytes,
                align,
                forceRepack,
                out var placementsXex1,
                out planReport1,
                out var remainingLevels,
                out var placementsXex2,
                out planReport2);

            ApplySplitHybrid(
                inputXexPath,
                outputXex1Path,
                outputXex2Path,
                placementsXex1,
                placementsXex2,
                levelToBlob,
                allowExtendXex,
                menuOrderXex1,
                menuOrderXex2,
                out applyReport1,
                out applyReport2);
        }

        private sealed class MenuEntryInfo { public uint LevelId; public string Name = ""; }
    }
}
