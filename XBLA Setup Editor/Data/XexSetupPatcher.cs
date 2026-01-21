using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XBLA_Setup_Editor
{
    public static class XexSetupPatcher
    {
        // --------------------------------------------
        // Ranges / offsets (confirmed)
        // --------------------------------------------

        // Setup pool: C7DF38 - DDFF5F (inclusive)
        public const int SetupBlocksStart = 0x00C7DF38;
        public const int SetupBlocksEndExclusive = 0x00DDFF60;

        // NEVER touch: C7DF38 - C9447F (inclusive)
        public const int SharedReadOnlyStart = 0x00C7DF38;
        public const int SharedReadOnlyEndExclusive = 0x00C94480; // first safe byte

        // Start of MP pool
        public const int MpHeadersStart = 0x00DB8CC0;

        // VA mapping:
        //   file C94480 -> VA 82CA1480  => VA = 0x8200D000 + fileOffset
        public const uint VaBase = 0x8200D000u;
        public static uint FileOffsetToVa(int fileOffset) => VaBase + (uint)fileOffset;

        // Default "end-of-xex blank space" start (user requested)
        public const int EndOfXexDefaultStart = 0x00F1B6D0;

        // --------------------------------------------
        // Mission order + original Solo file offsets
        // --------------------------------------------

        public sealed record SoloRegion(string Name, int OriginalFileOffset);

        public static readonly SoloRegion[] SoloRegions =
        {
            new("Archives",   0xC94480),
            new("Control",    0xCA4CF8),
            new("Facility",   0xCBB470),
            new("Aztec",      0xCCC988),
            new("Caverns",    0xCE2420),
            new("Cradle",     0xCE7B08),
            new("Egyptian",   0xCF0BD8),
            new("Dam",        0xD045F0),
            new("Depot",      0xD11A40),
            new("Frigate",    0xD1F3E8),
            new("Jungle",     0xD37440),
            new("Cuba",       0xD39898),
            new("Streets",    0xD47C40),
            new("Runway",     0xD4F238),
            new("Bunker (1)", 0xD589C0),
            new("Bunker (2)", 0xD67C10),
            new("Surface (1)",0xD787E8),
            new("Surface (2)",0xD86CD0),
            new("Silo",       0xD9AAC8),
            new("Statue",     0xDA18C0),
            new("Train",      0xDB4C50),
        };

        // Your requested priority order for filling/splitting
        public static readonly string[] PriorityOrder =
        {
            "Dam",
            "Facility",
            "Runway",
            "Surface (1)",
            "Bunker (1)",
            "Silo",
            "Frigate",
            "Surface (2)",
            "Bunker (2)",
            "Statue",
            "Archives",
            "Streets",
            "Depot",
            "Train",
            "Jungle",
            "Control",
            "Caverns",
            "Cradle",
            "Cuba",
            "Aztec",
            "Egyptian",
        };

        // SP pointer table offsets in the XEX (file offsets where BE32 VA is stored)
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

        // --------------------------------------------
        // Types
        // --------------------------------------------

        public enum RegionKind
        {
            FixedSP,     // original level slot
            SpPool,      // free region after SharedReadOnlyEndExclusive
            MpPool,      // MP pool region
            EndOfXex,    // blank space region(s) near EOF
            ExtendedXex  // appended space at EOF
        }

        public sealed record Placement(
            string LevelName,
            int FileOffset,
            uint NewVa,
            int Size,
            RegionKind Region,
            bool RequiresRepack);

        private sealed record Segment(int Start, int EndExclusive, RegionKind Kind)
        {
            public int Size => EndExclusive - Start;
        }

        // --------------------------------------------
        // Helpers
        // --------------------------------------------

        public static int AlignUp(int v, int align)
        {
            if (align <= 1) return v;
            return (v + (align - 1)) & ~(align - 1);
        }

        public static bool Overlaps(int aStart, int aEndExclusive, int bStart, int bEndExclusive)
            => aStart < bEndExclusive && bStart < aEndExclusive;

        public static void WriteBE32(byte[] b, int o, uint v)
        {
            b[o + 0] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }

        private static int GetOriginalFileOffset(string levelName)
        {
            var r = SoloRegions.FirstOrDefault(s => s.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            return r == null ? -1 : r.OriginalFileOffset;
        }

        private static uint GetOriginalVa(string levelName)
        {
            int off = GetOriginalFileOffset(levelName);
            return off < 0 ? 0 : FileOffsetToVa(off);
        }

        private static bool IsAllZero(byte[] xex, int start, int len)
        {
            int end = start + len;
            if (end > xex.Length) return false;
            for (int i = start; i < end; i++)
            {
                if (xex[i] != 0x00) return false;
            }
            return true;
        }

        private static List<Segment> FindZeroSegments(byte[] xex, int start, int minLen, RegionKind kind)
        {
            var segs = new List<Segment>();
            if (start < 0) start = 0;
            if (start >= xex.Length) return segs;

            int i = start;
            while (i < xex.Length)
            {
                // find run start
                while (i < xex.Length && xex[i] != 0x00) i++;
                if (i >= xex.Length) break;

                int runStart = i;
                while (i < xex.Length && xex[i] == 0x00) i++;
                int runEnd = i;

                int runLen = runEnd - runStart;
                if (runLen >= minLen)
                    segs.Add(new Segment(runStart, runEnd, kind));
            }

            return segs;
        }

        private static bool TryAlloc(ref List<Segment> segs, int size, int align, out int outStart, out RegionKind kind)
        {
            for (int idx = 0; idx < segs.Count; idx++)
            {
                var s = segs[idx];
                int start = AlignUp(s.Start, align);
                int end = start + size;
                if (end <= s.EndExclusive)
                {
                    outStart = start;
                    kind = s.Kind;

                    // consume from segment
                    // left remainder
                    var newSegs = new List<Segment>();

                    // keep left piece if any
                    if (s.Start < start)
                        newSegs.Add(new Segment(s.Start, start, s.Kind));

                    // keep right piece if any
                    if (end < s.EndExclusive)
                        newSegs.Add(new Segment(end, s.EndExclusive, s.Kind));

                    // replace current segment with remainder pieces
                    segs.RemoveAt(idx);
                    segs.InsertRange(idx, newSegs);

                    return true;
                }
            }

            outStart = 0;
            kind = default;
            return false;
        }

        // --------------------------------------------
        // HYBRID allocator
        //
        // - It may place levels out-of-order (greedy) to maximize placements.
        // - It always tries FixedSP first (if it fits inside that level’s original region).
        // - Then SP pool, then MP pool, then End-of-XEX zero runs,
        // - Then (optional) extend XEX and allocate there.
        // --------------------------------------------

        public static IReadOnlyList<Placement> PlanHybridPlacements(
            byte[] xex,
            IReadOnlyDictionary<string, int> levelToSize,
            IEnumerable<string> candidateLevels,
            bool allowMp,
            bool allowEndOfXex,
            int endOfXexStart,
            bool allowExtendXex,
            int extendChunkBytes,
            int align,
            out List<string> reportLines,
            out List<string> notPlaced)
        {
            reportLines = new List<string>();
            notPlaced = new List<string>();

            reportLines.Add("=== XEX PATCH (HYBRID PLAN) ===");
            reportLines.Add($"Align          : 0x{align:X}");
            reportLines.Add($"Allow MP pool  : {(allowMp ? "YES" : "no")}");
            reportLines.Add($"Allow EndOfXex : {(allowEndOfXex ? $"YES (scan zeros from 0x{endOfXexStart:X})" : "no")}");
            reportLines.Add($"Allow Extend   : {(allowExtendXex ? $"YES (chunk 0x{extendChunkBytes:X})" : "no")}");
            reportLines.Add("");

            // Build per-level fixed caps (original region until next header, except Train -> MP start)
            var caps = ComputeFixedRegionCaps(allowMpSpill: true);

            // Build free segments
            var segs = new List<Segment>();

            // SP pool = after read-only to MP start
            int spFreeStart = SharedReadOnlyEndExclusive;
            int spFreeEnd = MpHeadersStart;
            if (spFreeEnd > spFreeStart)
                segs.Add(new Segment(spFreeStart, spFreeEnd, RegionKind.SpPool));

            // MP pool segment
            if (allowMp)
            {
                int mpStart = MpHeadersStart;
                int mpEnd = SetupBlocksEndExclusive;
                if (mpEnd > mpStart)
                    segs.Add(new Segment(mpStart, mpEnd, RegionKind.MpPool));
            }

            // End-of-xex zero segments
            if (allowEndOfXex)
            {
                const int minRun = 0x200; // ignore tiny holes
                var zeroSegs = FindZeroSegments(xex, endOfXexStart, minRun, RegionKind.EndOfXex);
                segs.AddRange(zeroSegs);
            }

            // Greedy plan:
            // 1) try FixedSP if fits
            // 2) else allocate from segments (SP/MP/End/Extend)
            var placements = new List<Placement>();

            // We'll iterate multiple passes so a big level that fails doesn't block smaller ones.
            // Each pass tries to place any remaining level that fits somewhere.
            var remaining = new HashSet<string>(candidateLevels, StringComparer.OrdinalIgnoreCase);

            // filter only those we have sizes for and pointer offsets for
            // Note: We use a local reference since 'out' parameters cannot be used in lambdas
            var report = reportLines;
            remaining.RemoveWhere(l =>
            {
                if (!SpPointerOffsets.ContainsKey(l))
                {
                    report.Add($"SKIP: {l} (no SP pointer offset known)");
                    return true;
                }
                if (!levelToSize.TryGetValue(l, out int s) || s <= 0)
                {
                    report.Add($"SKIP: {l} (no size/setup provided)");
                    return true;
                }
                return false;
            });

            // First attempt: fixed slots
            foreach (var level in PriorityOrder.Where(remaining.Contains))
            {
                int size = levelToSize[level];
                if (!caps.TryGetValue(level, out int cap)) cap = 0;

                int origOff = GetOriginalFileOffset(level);
                if (origOff < 0) continue;

                int writeStart = origOff;
                int writeEnd = origOff + size;

                // Must not touch shared read-only region
                if (Overlaps(writeStart, writeEnd, SharedReadOnlyStart, SharedReadOnlyEndExclusive))
                    continue;

                if (size <= cap && writeEnd <= SetupBlocksEndExclusive)
                {
                    uint va = FileOffsetToVa(origOff);
                    placements.Add(new Placement(level, origOff, va, size, RegionKind.FixedSP, false));
                    remaining.Remove(level);
                }
            }

            // Carve out FixedSP placements from the pool segments to prevent overlaps
            foreach (var p in placements.Where(p => p.Region == RegionKind.FixedSP))
            {
                int pStart = p.FileOffset;
                int pEnd = p.FileOffset + p.Size;

                for (int i = segs.Count - 1; i >= 0; i--)
                {
                    var s = segs[i];
                    if (!Overlaps(s.Start, s.EndExclusive, pStart, pEnd))
                        continue;

                    // This segment overlaps with a FixedSP placement - carve it out
                    segs.RemoveAt(i);

                    // Add back the non-overlapping portions
                    // Left portion: before the placement
                    if (s.Start < pStart)
                        segs.Insert(i, new Segment(s.Start, pStart, s.Kind));

                    // Right portion: after the placement
                    if (s.EndExclusive > pEnd)
                        segs.Add(new Segment(pEnd, s.EndExclusive, s.Kind));
                }
            }

            // Greedy allocate the rest into pool segments
            bool progress;
            do
            {
                progress = false;

                foreach (var level in PriorityOrder.Where(remaining.Contains).ToList())
                {
                    int size = levelToSize[level];

                    // If we allow extending, opportunistically extend if nothing fits but we still have levels
                    if (!TryAlloc(ref segs, size, align, out int off, out RegionKind kind))
                    {
                        if (allowExtendXex)
                        {
                            // create a new segment at EOF (aligned), then retry once
                            int oldLen = xex.Length;
                            int newLen = AlignUp(oldLen, 0x10);
                            newLen = AlignUp(newLen + Math.Max(size, extendChunkBytes), 0x10);

                            // Add segment for extension *planning*. (Actual extension happens in ApplyHybrid.)
                            segs.Add(new Segment(AlignUp(oldLen, align), newLen, RegionKind.ExtendedXex));

                            if (!TryAlloc(ref segs, size, align, out off, out kind))
                                continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    uint vaNew = FileOffsetToVa(off);
                    bool repack = (vaNew != GetOriginalVa(level));

                    placements.Add(new Placement(level, off, vaNew, size, kind, repack));
                    remaining.Remove(level);
                    progress = true;
                }

            } while (progress && remaining.Count > 0);

            // Anything left
            foreach (var l in remaining)
                notPlaced.Add(l);

            reportLines.Add("");
            reportLines.Add("PLACED:");
            foreach (var p in placements)
                reportLines.Add($"  {p.LevelName,-12} {p.Region,-10} file 0x{p.FileOffset:X}  VA 0x{p.NewVa:X8}  size {p.Size}  repack={(p.RequiresRepack ? "YES" : "no")}");

            if (notPlaced.Count > 0)
            {
                reportLines.Add("");
                reportLines.Add("NOT PLACED:");
                foreach (var l in notPlaced)
                    reportLines.Add($"  {l}");
            }

            return placements;
        }

        public static IReadOnlyDictionary<string, int> ComputeFixedRegionCaps(bool allowMpSpill)
        {
            var caps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < SoloRegions.Length; i++)
            {
                var cur = SoloRegions[i];
                int start = cur.OriginalFileOffset;

                int endExclusive;
                if (i < SoloRegions.Length - 1)
                    endExclusive = SoloRegions[i + 1].OriginalFileOffset;
                else
                    endExclusive = allowMpSpill ? SetupBlocksEndExclusive : MpHeadersStart; // Train

                caps[cur.Name] = Math.Max(0, endExclusive - start);
            }

            return caps;
        }

        // --------------------------------------------
        // APPLY (Hybrid)
        //
        // - Can extend the XEX file if placements include RegionKind.ExtendedXex.
        // - Writes blobs + patches SP pointers.
        // --------------------------------------------

        public static void ApplyHybrid(
            string inputXexPath,
            string outputXexPath,
            IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, byte[]> levelToBlob,
            bool allowExtendXex,
            out List<string> reportLines)
        {
            byte[] xex = File.ReadAllBytes(inputXexPath);

            // Determine required file length if extended placements exist
            int requiredLen = xex.Length;
            foreach (var p in placements)
            {
                int end = p.FileOffset + p.Size;
                if (end > requiredLen) requiredLen = end;
            }

            if (requiredLen > xex.Length)
            {
                if (!allowExtendXex)
                    throw new InvalidOperationException($"Plan requires extending XEX to 0x{requiredLen:X}, but extend option is disabled.");

                Array.Resize(ref xex, requiredLen);
                // new bytes are zero-initialized by Array.Resize
            }

            var patched = new List<string>();
            var skipped = new List<string>();
            var warnings = new List<string>();

            foreach (var p in placements)
            {
                if (!levelToBlob.TryGetValue(p.LevelName, out var blob) || blob == null || blob.Length == 0)
                {
                    skipped.Add($"SKIP: {p.LevelName} (missing blob)");
                    continue;
                }

                if (blob.Length != p.Size)
                {
                    warnings.Add($"WARN: {p.LevelName} blob size {blob.Length} != planned {p.Size}. Using blob size.");
                }

                int start = p.FileOffset;
                int end = start + blob.Length;

                if (Overlaps(start, end, SharedReadOnlyStart, SharedReadOnlyEndExclusive))
                    throw new InvalidOperationException($"Guard: write for {p.LevelName} touches shared read-only range.");

                if (end > xex.Length)
                    throw new InvalidOperationException($"Write for {p.LevelName} exceeds XEX length (end 0x{end:X}, len 0x{xex.Length:X}).");

                // If target is supposed to be blank area, we can warn if it's not
                if ((p.Region == RegionKind.EndOfXex || p.Region == RegionKind.ExtendedXex) && !IsAllZero(xex, start, Math.Min(blob.Length, 0x200)))
                {
                    warnings.Add($"WARN: Target for {p.LevelName} ({p.Region}) not blank at 0x{start:X}. ***WILL BREAK POSSIBLE***");
                }

                Buffer.BlockCopy(blob, 0, xex, start, blob.Length);

                if (!SpPointerOffsets.TryGetValue(p.LevelName, out int ptrOff))
                    throw new InvalidOperationException($"No SP pointer offset known for {p.LevelName}.");

                WriteBE32(xex, ptrOff, p.NewVa);

                patched.Add($"PATCH {p.LevelName,-12} [{p.Region}] -> file 0x{p.FileOffset:X}  VA 0x{p.NewVa:X8}  ({blob.Length} bytes)  ptr@0x{ptrOff:X}");
            }

            File.WriteAllBytes(outputXexPath, xex);

            reportLines = new List<string>
            {
                "",
                "=== XEX PATCH (HYBRID APPLY) ===",
                $"Input : {Path.GetFileName(inputXexPath)} ({xex.Length} bytes)",
                $"Output: {Path.GetFileName(outputXexPath)} ({new FileInfo(outputXexPath).Length} bytes)",
                "",
                "PATCHED:"
            };
            reportLines.AddRange(patched.Select(x => "  " + x));

            reportLines.Add("");
            reportLines.Add("SKIPPED:");
            reportLines.AddRange(skipped.Select(x => "  " + x));

            reportLines.Add("");
            reportLines.Add("WARNINGS:");
            reportLines.AddRange(warnings.Select(x => "  " + x));

            reportLines.Add("");
        }

        // --------------------------------------------
        // Split plan helper:
        // - Dynamically finds the optimal split point in PriorityOrder.
        // - XEX1 gets levels 0..splitIndex-1, XEX2 gets levels splitIndex..end.
        // - Tries to fit as many levels as possible in XEX1 (in order).
        // --------------------------------------------

        public static void PlanSplitAcrossTwoXex(
            byte[] xex,
            IReadOnlyDictionary<string, int> levelToSize,
            bool allowMp,
            bool allowEndOfXex,
            int endOfXexStart,
            bool allowExtendXex,
            int extendChunkBytes,
            int align,
            out IReadOnlyList<Placement> placementsXex1,
            out List<string> rep1,
            out List<string> remainingLevels,
            out IReadOnlyList<Placement> placementsXex2,
            out List<string> rep2)
        {
            // Filter to only levels we have sizes for
            var allLevels = PriorityOrder.Where(l => levelToSize.ContainsKey(l) && levelToSize[l] > 0).ToList();

            // Binary search for the optimal split point: find the maximum number of levels that fit in XEX1
            int bestSplit = 0;
            IReadOnlyList<Placement> bestPlacements = Array.Empty<Placement>();
            List<string> bestReport = new List<string>();

            // Try increasing numbers of levels until we find where it overflows
            for (int count = 1; count <= allLevels.Count; count++)
            {
                var candidateLevels = allLevels.Take(count).ToList();

                var testPlacements = PlanHybridPlacements(
                    xex: xex,
                    levelToSize: levelToSize,
                    candidateLevels: candidateLevels,
                    allowMp: allowMp,
                    allowEndOfXex: allowEndOfXex,
                    endOfXexStart: endOfXexStart,
                    allowExtendXex: allowExtendXex,
                    extendChunkBytes: extendChunkBytes,
                    align: align,
                    out var testReport,
                    out var notPlaced);

                // If all candidates were placed, this is a valid split point
                if (notPlaced.Count == 0)
                {
                    bestSplit = count;
                    bestPlacements = testPlacements;
                    bestReport = testReport;
                }
                else
                {
                    // We've found the overflow point - stop here
                    break;
                }
            }

            // XEX1 gets the first bestSplit levels
            placementsXex1 = bestPlacements;
            rep1 = bestReport;

            // Remaining levels go to XEX2
            remainingLevels = allLevels.Skip(bestSplit).ToList();

            if (remainingLevels.Count > 0)
            {
                rep1.Add("");
                rep1.Add($"=== SPLIT POINT: XEX1 contains {bestSplit} levels, XEX2 will contain {remainingLevels.Count} levels ===");
                rep1.Add("XEX2 levels: " + string.Join(", ", remainingLevels));
            }

            // XEX2: plan the remainder
            placementsXex2 = PlanHybridPlacements(
                xex: xex,
                levelToSize: levelToSize,
                candidateLevels: remainingLevels,
                allowMp: allowMp,
                allowEndOfXex: allowEndOfXex,
                endOfXexStart: endOfXexStart,
                allowExtendXex: allowExtendXex,
                extendChunkBytes: extendChunkBytes,
                align: align,
                out rep2,
                out var notPlaced2);

            if (notPlaced2.Count > 0)
            {
                rep2.Add("");
                rep2.Add("WARNING: Some levels could not be placed even in XEX2:");
                foreach (var l in notPlaced2)
                    rep2.Add($"  {l}");
            }
        }
    }
}