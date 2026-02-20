# XBLA Setup Editor — Developer Reference

A modding tool for GoldenEye 007 XBLA. Slop-coded with Claude.
Target runtime: .NET 8.0 Windows Forms.

---

## XEX File Memory Layout

All offsets are **file offsets**. VA = `0x8200D000 + fileOffset`.

```
0x000000  – 0xC7DF37   XEX header / other data (do not touch)
0xC7DF38  – 0xC9447F   Shared read-only setup data (untouchable)
0xC94480  – 0xDB8CBF   SP Setup pool  (BG Data pool — setups go here)
0xDB8CC0  – 0xDDFF5F   Multiplayer region (can overflow into if enabled)
0xDDFF60  – 0xF1B6CF   Other XEX data
0xF1B6D0  +            End-of-XEX extension point
```

Key constants in `XexSetupPatcher.cs`:
```
SharedReadOnlyEndExclusive = 0x00C94480
MpHeadersStart             = 0x00DB8CC0
SetupBlocksEndExclusive    = 0x00DDFF60
VaBase                     = 0x8200D000
EndOfXexDefaultStart       = 0x00F1B6D0
```

---

## SP Setup Pool (BG Data)

- **Pool range**: `0xC94480` – `0xDB8CC0`  (0x124440 = ~1.14 MB)
- **MP overflow**: `0xDB8CC0` – `0xDDFF60`  (0x272A0 = ~157 KB)
- Each level has a "fixed slot" at its original file offset; if the converted setup fits it goes there (no repack). Otherwise it lands in the pool.
- Cuba cannot be relocated — hardcoded VAs. Always placed at fixed offset `0xD39898`, size `0xE3A8`.
- Cuba only needs to follow Cradle in the XEX (credits scene), so it is added to XEX1 candidates only when Cradle is also there.

### Level ID table (BG Data region)

File: `MpSetupCompactor.cs` constants.
Region: `0x84AF90` – `0x84B7DF`  (38 entries × 0x38 bytes each)

Per-entry offsets:
```
+0x00  Level ID          uint32 BE
+0x04  Name Pointer      uint32 BE
+0x08  Scale             float  BE
+0x0C  Visibility        float  BE
+0x10  Unused            float  BE
+0x14  STAN Pointer      uint32 BE  ← updated by ApplyStanCore
+0x18  SP Setup Pointer  uint32 BE  ← updated by ApplyHybridCore
+0x1C  MP Setup Pointer  uint32 BE
+0x20  BG Data Pointer   uint32 BE  ← updated by FixBgPointers
+0x24  … rest unused
```

---

## BG Data Compaction (MpSetupCompactor)

The "BG Data region" (confusingly stored at `0x84C5F0` – `0xB00AC0`) contains MP-style setup entries that include BG pointers. Compacting removes unused entries (Library/Basement/Stack, Citadel, Caves, Complex, Temple) and shifts the rest down, freeing a tail that SP setups and STAN blobs can spill into.

Known layout in `MpSetupCompactor.KnownLayout` (25 entries).
`RegionStart = 0x84C5F0`, `RegionEnd = 0xB00AC0`.

---

## STAN (Clipping) Region

**STATUS: Not enough free pool space for practical use. UI option kept but rarely useful.**

Region spans `0x720588` – `0x84AF3B`.

### STAN blob structure
```
0x0000  uint32 BE  — total blob size (from byte 0 to end, inclusive)
0x0408  start of stans data
end-4   uint32 BE  — back-pointer (VA of blob start); padded so last nibble is 7 or F
```

### Fixed slot starts (AllStanSlotStarts in XexSetupPatcher.cs)
```
Library    0x720588   Archives   0x724720   Control    0x732090
Facility   0x744530   Stack(MP)  0x7591C0   Aztec      0x75D358
Caverns    0x76A088   Cradle     0x775880   Egyptian   0x77BBA0
Dam        0x7832C8   Depot      0x799298   Frigate    0x7A99B8
Temple(MP) 0x7B8C28   Bsmt(MP)   0x7BA9E0   Jungle     0x7BEB78
Cuba       0x7CF6E0   Caves(MP)  0x7D14D8   Streets    0x7D52D0
Complex(MP)0x7DF740   Runway     0x7E40D8   Bunker1    0x7E83D0
Bunker2    0x7F11D8   Surface1   0x7FC680   Surface2*  0x810B58
sho*       0x825030   Silo       0x8258D8   Statue     0x83A310
Train      0x845400
```
`* Surface2 shares Surface1's STAN pointer; its slot + sho = free pool`

### SP-relevant STAN fixed slots (StanSoloRegions)
Archives, Control, Facility, Aztec, Caverns, Cradle, Egyptian, Dam, Depot,
Frigate, Jungle, Cuba, Streets, Runway, Bunker (1), Bunker (2), Surface (1),
Silo, Statue, Train.
(Surface (2) always mirrors Surface (1) — excluded from independent planning.)

### STAN free pool segments (StanPoolSegments)
```
0x7595B8 – 0x75D358   tail of Stack slot  (0x3DA0 =  15,776 bytes)
0x810B58 – 0x8258D8   Surface2 + sho      (0x14D80 = 85,376 bytes)
Total: ~99 KB — tight for a full 20-level mod
```

### STAN pointer offsets in Level ID table
Each entry at `StanPointerOffsets[levelName]` = file offset of +0x14 in that level's table row.
Equals `SpPointerOffset - 4` for every level.

### STAN application notes
- `ApplyStanCore` writes blobs, updates STAN pointers, fixes back-pointers.
- `FixStanBackPointer` scans backward through the blob for the old start VA and rewrites it.
- After placing Surface (1), its final VA is also mirrored into Surface (2)'s STAN pointer.

---

## Menu & Briefing System

```
Menu entries:   0x71E570 – 0x71E8B7   stride 12 bytes
                [Ptr 4][FolderTextID 2][IconTextID 2][LevelID 4]
Briefings:      0x71DF60 – 0x71E350   21 entries × 0x30 bytes
```

- `BuildMenuStructIndex` scans menu region for non-zero LevelIDs.
- Positional fallback: `MENU_XEX_START + i * 12` (needed for CE XEXes that zero out some slots).
- `OriginalBriefingIndices` static map used as fallback when dynamic scan misses rearranged slots.

---

## Architecture — Shared State

### XEX shared state (IXexTab)

The central pattern: `MainForm` loads the XEX once and shares it across tabs.

```
MainForm  -->  XexLoaded event      -->  tab.OnXexLoaded(data, path)
tab       -->  XexModified event    -->  MainForm.OnTabXexModified()
MainForm  -->  tab.OnXexDataUpdated(data)  -->  all other tabs
Save      -->  tab.GetModifiedXexData()  -->  write to disk
```

### 21990 shared state (I21990Tab)

```
MainForm  -->  File21990Loaded event  -->  tab.On21990Loaded(data, path)
```

---

## Source Files

### `Program.cs`
Entry point. Sets `HighDpiMode.PerMonitorV2`, calls `ApplicationConfiguration.Initialize()`, runs `MainForm`.

---

### `IXexTab.cs`
Defines `IXexTab`, `I21990Tab`, `XexModifiedEventArgs`, `XexLoadedEventArgs`, `File21990LoadedEventArgs`.

**`interface IXexTab`**
| Member | Description |
|--------|-------------|
| `string TabDisplayName` | Shown in tab header and `XexModifiedEventArgs.Source` |
| `bool HasUnsavedChanges` | Pending changes flag |
| `void OnXexLoaded(byte[] xexData, string path)` | Called on first load |
| `void OnXexDataUpdated(byte[] xexData)` | Lightweight re-broadcast — does NOT reset tab state |
| `void OnXexUnloaded()` | Called on unload |
| `byte[]? GetModifiedXexData()` | Returns modified data and clears `HasUnsavedChanges`, or null if unchanged |

---

### `DpiHelper.cs`
| Member | Description |
|--------|-------------|
| `static int Scale(Control c, int value)` | Scales 96-DPI pixel value to current DPI |
| `static float GetScaleFactor(Control c)` | Raw scale factor (1.0 = 100%, 1.5 = 150%) |

Uses `GetDpiForSystem()` P/Invoke with GDI+ fallback. Result cached. All forms use `AutoScaleMode.None` to prevent double-scaling. **All pixel values in UI must be wrapped in `DpiHelper.Scale(this, value)`. Font sizes × `DpiHelper.GetScaleFactor(this)`.**

---

### `TooltipTexts.cs`
All `public const string` tooltip text, organized in nested static classes: `StrEditor`, `SetupPatching`, `File21990`, `XexExtender`, `MpWeaponSets`, `WeaponStats`, `MainForm`. Always add tooltips here rather than inline strings.

---

### `XdeltaHelper.cs`
| Member | Description |
|--------|-------------|
| `static bool IsXdeltaAvailable()` | True if `xdelta3.exe` exists in app dir |
| `static bool CreatePatch(byte[] original, string modifiedPath, string patchOutput, out string error)` | Runs `xdelta3 -f -e -s` |
| `static void OfferCreatePatch(IWin32Window owner, byte[] original, string savedPath)` | Full UI flow: prompt → save dialog → create patch |
| `static void OfferCreateSplitPatches(IWin32Window owner, byte[] original, string path1, string path2)` | Creates two patches for split-XEX mods |

---

### `StrFile.cs`
Types: `StrEntry` (`byte Bank`, `byte Id`, `string Text`), `StrFile`

| Member | Description |
|--------|-------------|
| `static StrFile Load(string path)` | Parses STR/ADB binary |
| `byte[] SaveToBytes()` | Serialises back to binary |
| `List<StrEntry> Entries` | Parsed entries |

**Gotchas:** Offset `0x0163` is intentionally NOT patched — patching it crashes the game. Text offsets in file are ÷2 (×2 to get byte offset). Encoding is big-endian UTF-16BE. FFFF terminator found via heuristic scan.

---

### `Data/DataHelper.cs`
`static Dictionary<string, int> BuildDictionary(IEnumerable<(string Name, int Code)> pairs)` — case-insensitive, deduplicates by first occurrence.

---

### `Data/AmmoCountData.cs` / `Data/ToggleData.cs` / `Data/ScaleData.cs` / `Data/WeaponData.cs` / `Data/AmmoTypeData.cs` / `Data/PropData.cs`
Static lookup tables for combo box population. Each exposes `(string Name, int Code)[] Pairs` and `static Dictionary<string, int> Build()`.

- `WeaponData`: 32 weapons, IDs `0x00`–`0x20`. Tank = `0x20`.
- `AmmoTypeData`: None, 9mm, Rifle, Cartridges, Grenades, Rockets, RemoteMines, ProxMines, TimedMines, Knife, GrenadeRounds, Magnum, GoldenBullets, WatchLaser, Tank.
- `PropData`: prop model IDs `0x60` (no model) through `0xE7`.

---

### `Data/BeginnerRulesData.cs`
| Member | Description |
|--------|-------------|
| `static readonly Dictionary<string, string> WeaponToAmmoType` | Weapon name → ammo type name |
| `static readonly Dictionary<string, string> WeaponToDefaultAmmoCount` | Weapon name → default ammo count |
| `static HashSet<string> PropNames` | Lazy-init; names of weapons that should have a prop model |

---

### `Data/File21990Parser.cs`
Types: `File21990Parser`, nested: `MenuEntry`, `StageMusicEntry`, `SkyEntry21990`, `SkyEntryXex`

| Member | Description |
|--------|-------------|
| `static File21990Parser Load(string path / byte[] data)` | Parses 21990 file |
| `int ApplyMenuDataToXex(byte[] xexData, List<string> log)` | Patches folder/icon text IDs in XEX |
| `int ApplySkyData(byte[] xexData, List<string> log, bool skyToFog, bool n64Fog)` | Patches 48 sky entries |
| `int ApplyMusicData(byte[] xexData, List<string> log)` | Patches mission music table |
| `int ApplyMissionBriefings(byte[] xexData, List<string> log)` | Patches briefing indices |
| `static SkyEntryXex ConvertToXexFormat(...)` | Converts single sky entry; applies `XblaFogRatios` compensation |
| `void GenerateComparisonLog(List<string> log)` | Diagnostic comparison log |

**Key constants:** `SKY_DATA_START_21990 = 0x24080`, `SKY_ENTRY_SIZE_21990 = 0x5C`, `SKY_ENTRY_COUNT = 48`, `SKY_DATA_START_XEX = 0x84B860`, `MUSIC_MISSION_TABLE_OFFSET = 0xDE1A30`, `BRIEFING_XEX_START = 0x71DF60`

**Gotcha:** Cuba (`0x36`) excluded from menu entries. `XblaFogRatios` dict maps `levelId → (FarRatio, NearRatio)` for per-level fog distance compensation.

---

### `Data/WeaponStatsParser.cs`
Types: `WeaponStatsParser`, nested: `WeaponStatsEntry`, `WeaponModelEntry`, `AmmoReserveEntry`

| Member | Description |
|--------|-------------|
| `static WeaponStatsParser LoadFromXex(byte[] xexData)` | Reads all three tables |
| `static WeaponStatsParser LoadFrom21990(byte[] data)` | N64 field remapping via `FromN64Bytes` |
| `static WeaponStatsParser LoadFrom21990WithXblaLayout(byte[] data)` | Debug: reads 21990 with XBLA field positions |
| `void ApplyToXex(byte[] xexData, List<string>? log)` | Writes all tables back |
| `List<WeaponStatsEntry> WeaponStats` | 47 entries at `0x4134D8`, 0x70 bytes each |
| `List<WeaponModelEntry> WeaponModels` | 89 entries at `0x414968`, 0x38 bytes each |
| `List<AmmoReserveEntry> AmmoReserves` | 30 entries at `0x416C84`, 0x0C bytes each |

**CRITICAL — RecoilSpeed:** Bytes `0x44–0x47` in `WeaponStatsEntry` are **NOT** an IEEE float — they are raw timing/frame-delay bytes. Do NOT read/write as float. Preserved verbatim in all import/export. N64 field layout differs from XBLA; always use `FromN64Bytes` when importing from 21990.

---

### `Data/MPWeaponSetParser.cs`
Types: `MPWeaponSetParser`, nested: `WeaponEntry`, `WeaponSet`, `SelectListEntry`

| Member | Description |
|--------|-------------|
| `static MPWeaponSetParser LoadFromXex(byte[] xexData)` | Reads weapon sets and select list |
| `void ApplyToXex(byte[] xexData, List<string>? log)` | Writes back |
| `List<WeaponSet> WeaponSets` | 15 sets at `0x417728`, 0x40 bytes each |
| `List<SelectListEntry> SelectList` | 16 entries at `0x417AE8` |

`SelectListEntry.WeaponSetIndex`: index 0 → -1 (Random), 1–15 → set 0–14.

---

### `Data/XexSetupPatcher.cs`
Types: `XexSetupPatcher` (static), `SoloRegion` (record), `Placement` (record), `RegionKind` (enum)

**Core SP and STAN setup placement engine.** See constants table in XEX Memory Layout section above.

| Member | Description |
|--------|-------------|
| `static readonly SoloRegion[] SoloRegions` | 21 SP levels with original file offsets |
| `static readonly SoloRegion[] StanSoloRegions` | 20 STAN blob slots |
| `static readonly string[] PriorityOrder` | 21 levels in processing priority order |
| `static readonly Dictionary<string, int> SpPointerOffsets` | Level → SP pointer file offset |
| `static readonly Dictionary<string, int> StanPointerOffsets` | Level → STAN pointer file offset |
| `static IReadOnlyList<Placement> PlanHybridPlacements(...)` | Plans SP placement across fixed slots / pool / extension |
| `static void PlanSplitAcrossTwoXex(...)` | Splits levels across two XEX files |
| `static IReadOnlyList<Placement> PlanStanPlacements(...)` | Plans STAN blob placement |
| `static void ApplyHybrid(...)` | Writes blobs, updates SP+STAN pointers, reconfigures menu/briefings |
| `static List<string> GenerateSpaceUsageReport(...)` | Human-readable space usage |

Static lookup tables: `LevelNameToId`, `LevelImageIds`, `LevelBriefBase`, `VanillaMenuOrder`, `OriginalBriefingIndices`

---

### `Data/XexExtender.cs`
**EXPERIMENTAL. Capped at ~32 KB for GoldenEye XBLA due to Xenia page-table validation.**

| Member | Description |
|--------|-------------|
| `static XexAnalysis Analyze(byte[] xexData)` | Parses block table; computes `MaxExtensionSize` |
| `static (byte[]? ModifiedXex, ExtensionResult Result) Extend(byte[] xexData, byte[] newData, ...)` | Updates last block's `data_size` only; does NOT touch `image_size` |
| `static uint GetAppendAddress(byte[] xexData)` | Memory address where new data starts |

`BLOCK_ENTRIES_OFFSET = 0x1C08`, `DATA_START_OFFSET = 0x3000`, `XEX_BASE_ADDRESS = 0x82000000`. Increasing `image_size` crashes Xenia.

---

### `Data/XEXArmorRemover.cs`
| Member | Description |
|--------|-------------|
| `static ScanResult ScanForArmor(byte[] xexData, List<string>? log)` | Finds armor blocks in setup region |
| `static byte[] RemoveArmor(byte[] xexData, ScanResult scanResult, List<string>? log)` | Zeroes armor records; file size unchanged |

Region `0xC7DF38`–`0xDDFF5F`. Signature: `{ 0x00, 0x01, 0x80, 0x00, 0x15, 0x00 }` + image byte at offset+6 = `0x73` or `0x74`. Record size = `0x88`.

---

### `Data/MpSetupCompactor.cs`
**Two-step, one-shot.** Compact first, then FixBgPointers — in that order, exactly once.

| Member | Description |
|--------|-------------|
| `static readonly MpSetupEntry[] KnownLayout` | 25 entries from `0x84C5F0` to `0xB00AC0` |
| `static readonly string[] DefaultRemove` | Library/Basement/Stack, Citadel, Caves, Complex, Temple |
| `static byte[] Compact(...)` | Clones XEX, zeroes entire region, writes kept entries from `RegionStart` |
| `static void FixBgPointers(...)` | Adjusts BG VA pointers in Level ID Setup table; zeroes pointers into removed entries |

`LevelIdSetupStart = 0x84AF90`, stride `0x38`, BG pointer at `+0x20` per entry.

---

### Controls

#### `Controls/StrEditorControl.cs`
Standalone STR/ADB editor. **Does NOT implement `IXexTab`**. Bank/ID formatted as hex. Add auto-increments ID; bank wraps on `0xFF` overflow.

#### `Controls/SetupPatchingControl.cs`  `TabDisplayName = "Setup Patching"`
Most complex tab. Options: PatchXex, CompactMpFirst, PatchStan, AllowMp, AllowExtendXex, ForceRepack, SplitTwoXex.

- Cuba blob always extracted from loaded XEX via SP pointer — never re-converted by `setupconv.exe`
- `BatchFolderName` property used by `MainForm` to suggest save filename suffix
- `BuildBlobsForPlacements()`: handles repack (re-runs `setupconv.exe` with new VA) vs. no-repack
- STAN extraction: reads `StanSoloRegions`, sizes from blob's first 4 bytes (BE32 "size from here to end")

#### `Controls/File21990ImporterControl.cs`  `TabDisplayName = "Skies, Fog and Music"`
Implements `IXexTab` + `I21990Tab`. **Stores `_originalXexData` snapshot on `OnXexLoaded` — restores from snapshot before each Apply to prevent fog stacking.**

#### `Controls/WeaponStatsControl.cs`  `TabDisplayName = "Weapon Stats"`
Three sub-tabs: Weapon Stats (47 rows, 33 cols), Weapon Models (89 rows, 17 cols), Ammo Reserve (30 rows, 4 cols). Double-buffering enabled via reflection. `NotifyXexModified()` on every cell change (live updates). `AutoApplyWeaponStats()` triggered immediately on 21990 load if XEX already loaded. `PreserveRamAddrs` copies RAM addresses from original XEX parser after import.

#### `Controls/XexExtenderControl.cs`  `TabDisplayName = "XEX Extender"`
Logs `"This tool DOES NOT WORK correctly and WILL BREAK YOUR XEX FILE."` on construction. The `_chkRecalcSha1` checkbox has minimal effect.

#### `Controls/MPWeaponSetControl.cs`  `TabDisplayName = "Multiplayer Weapon Sets"`
`TEXT_FOLDER_OFFSET = 0x0000A3AC` — 3-char language code. Beginner mode auto-fills ammo/prop from `BeginnerRulesData`; guarded by `_isApplyingBeginnerDefaults`. Armor removal via `XEXArmorRemover` at save time. `EnsureComboBoxContains()` prevents display errors for unknown codes. Live updates via `NotifyXexModified()`.

#### `Controls/MpSetupCompactorControl.cs`  `TabDisplayName = "BG Data Compactor"`
Two-step UI: Compact → Fix BG Pointers. "Fix BG Pointers" button disables itself after use (one-shot).

---

### `MainForm.cs`
Central hub. Tabs in display order: STR Editor, Multiplayer Weapon Sets, Setup Patching, Skies Fog and Music, Weapon Stats, XEX Extender, BG Data Compactor.

| Member | Description |
|--------|-------------|
| `byte[]? GetXexData()` | Current in-memory XEX |
| `byte[]? GetOriginalXexData()` | Unmodified original (for xdelta patches) |
| `string? GetXexPath()` | Loaded XEX path |
| `void ApplyXexChanges(byte[] data, string source)` | Apply changes from external callers |

Save always uses "Save As". Suggested filename uses `BatchFolderName` suffix when set. `CollectTabModifications()` called just before write to capture deferred changes. Offers xdelta patch after save. Shows usage agreement on startup. Easter egg: small invisible button upper-right opens credits.

---

### Standalone Forms (Legacy / Debug)

These pre-date the tabbed `MainForm` and operate on files independently (no shared state). Launched from `Data/ToolLauncherForm.cs`.

| Form | Equivalent to |
|------|---------------|
| `StrEditorForm.cs` | `StrEditorControl` |
| `Data/File21990ImporterForm.cs` | `File21990ImporterControl` — **no stacking prevention** |
| `Data/MPWeaponSetEditorForm.cs` | `MPWeaponSetControl` — saves in place (with optional backup) |
| `Data/XexExtenderForm.cs` | `XexExtenderControl` — no "DOES NOT WORK" warning |
| `Data/WeaponStatsEditorForm.cs` | `WeaponStatsControl` — saves in place |
| `Data/SetupPatchingForm.cs` | `SetupPatchingControl` — compact standalone batch patcher |

---

## Cross-File Gotchas

1. **Cuba is never re-converted.** Extracted from XEX via SP pointer. Contains embedded absolute VAs — cannot be relocated.
2. **RecoilSpeed bytes `0x44–0x47` are NOT a float.** Raw timing bytes. Preserve verbatim.
3. **N64 vs XBLA field layout differs.** Use `FromN64Bytes` when importing weapon stats from 21990.
4. **21990 fog stacking.** `File21990ImporterControl` restores from `_originalXexData` snapshot before each Apply. Standalone `File21990ImporterForm` does not — each Apply on a fresh copy.
5. **MpSetupCompactor is two-step, one-shot.** Compact → FixBgPointers, in order, exactly once.
6. **Surface (2) STAN always mirrors Surface (1).** `PlanStanPlacements` excludes Surface (2) from outputs; `ApplyStanCore` writes Surface (1)'s final VA into Surface (2)'s pointer.
7. **XEX extension capped at ~32 KB.** Xenia page-table validation. Never touch `image_size`.
8. **STR offset `0x0163` intentionally not patched.** Patching it crashes the game.
9. **Text folder code** at `0x0000A3AC` is 3 printable ASCII chars (e.g., `"ENG"`). Written by `MPWeaponSetControl` and `MPWeaponSetEditorForm`.
10. **DPI scaling.** All pixel values → `DpiHelper.Scale(this, value)`. Font sizes × `DpiHelper.GetScaleFactor(this)`. `AutoScaleMode.None` on all forms.

---

## Terminology (UI labels)

| Term | Meaning |
|------|---------|
| BG Data | The compactable region / its pool (NOT multiplayer gameplay) |
| STAN Data | Clipping/collision blob region |
| Multiplayer | Actual MP game mode (weapon sets, etc.) |
| Multiplayer region overflow | SP setups spilling into the MP headers area |
| SP Setup pool | Synonym for BG Data pool in older code comments |

---

## RegionKind Enum

```csharp
FixedSP, SpPool, MpPool, EndOfXex, ExtendedXex,
CompactedMpTail,   // freed BG Data tail (shared by SP setups + STAN)
FixedStan, StanPool
```

---

## Cuba Special Cases

- Cannot be converted by `setupconv.exe` (crashes).
- Blob is extracted directly from the loaded XEX via its SP pointer.
- Cannot be relocated (embedded absolute VA references).
- Only needs to exist in XEX1 when Cradle is also in XEX1 (credits only accessible after Cradle).
- In split mode: if Cradle doesn't fit in XEX1, both Cradle and Cuba fall to XEX2.

---

## Third-Party Tools

| Tool | License | Purpose |
|------|---------|---------|
| `setupconv.exe` | MIT (Carnivorous) | Converts N64 setup → XBLA format; call: `setupconv.exe <in> <out> <VA_hex>` |
| `xdelta3.exe` | GPL-2.0+ | Creates binary diff patches for distribution |

---

# GoldenEye XBLA XEX — Full Offset Reference

All offsets are **file offsets** (hex). VA = `0x8200D000 + fileOffset`.

---

## Text / String Regions

```
6284   – 663B   Weapon & Item Names
663C   – 6677   Ejected Casing Names
7670   – 872F   Prop Names
8730   – 882B   Briefing Names
8B28   – 8EB3   Character Names
8FF8   – 903F   Mission # and Part
90F0   – 91A3   Nintendo Replacement Text
9C24   – 9DA7   Level Details (name, scale, other)
A378   – A46B   Level/Text Internal Names
A4D0   – A503   Broken Build Message
1926C  – 198CB  Music Names
198CC  – 1AB93  Sound Effect Names
1AC74  – 1ACA3  Music File Locations
1ACA4  – 1ACD3  Sound File Locations
1AD68  – 1AE03  Bullet Impact Names
1AE68  – 1AF73  Level Photo Names
1AF74  – 1B15F  Monitor Screen Names
1B160  – 1B26B  Character Photo Names (incomplete)
1B54C  – 1B633  Ammo Icon Names
1DB70  – 1DFBF  Player Profile Names
```

---

## Pad Room / STAN Values (Unused N64 data)

```
A504   – 189EB  Pad Room/Stan Values
  A504  – B433   (0xF30)
  EC90  – FE7B   (0x11EC)
  11464 – 11D9F  (0x93C)
  1435C – 14783  (0x428)
  14AB0 – 1511B  (0x66C)
  1677C – 17727  (0xFAC)
```

---

## Bond Cuffs (Outfit/Head Pointers)

```
B0AA2   -Suit?
B0AA6   -Head?

B0B1E   Boiler (body)
B0B52   Suit (body)
B0B5A   Jungle (body)
B0B62   Snowsuit (body)
B0B6A   Tuxedo (body)
B0B52   Suit (body)
B0BA2   Tuxedo Bonus (body)

B0BEE   Boiler Head
B0BF6   Suit/Snowsuit Head
B0BFE   Jungle Head
B0C06   Tuxedo Head
```

---

## Weapon / Item Data

### Weapon ID Equipped Props
```
C4C74  – C4DD7   ID Table [IDs 0x00–0x58]
C4DD8  – C4F1B   Props
```

### Enemy Rockets
```
CC453   Normal Gun ID
CC456   Normal Gun Prop
12FAE3  Spawned-in Guard Gun ID
12FAE6  Spawned-in Guard Gun Prop
```

### Weapon Statistics: `4134D8` – `414967`  (0x70 per entry)
```
0x00  Muzzle Flash Extension
0x04  On-Screen X Position
0x08  On-Screen Y Position
0x0C  On-Screen Z Position
0x10  Aim Upward Shift
0x14  Aim Downward Shift
0x18  Aim Left/Right Shift
0x1E  Ammunition Type
0x20  Magazine Size
0x22  Fire – Automatic
0x23  Fire – Single Shot
0x24  Penetration
0x25  Sound Trigger Rate
0x26  Sound Effect
0x28  Ejected Casings (RAM address)
0x2C  Damage
0x30  Inaccuracy
0x34  Scope
0x38  Crosshair Speed
0x3C  Weapon Aim/Lock-On Speed
0x40  Sway
0x44  Recoil Speed
0x48  Recoil Backward
0x4C  Recoil Upward
0x50  Recoil Bolt
0x54  Volume to AI, Single Shot
0x58  Volume to AI, Multiple Shots
0x5C  Volume to AI, Active Fire over Distance?
0x60  Volume to AI, Baseline 1?
0x64  Volume to AI, Baseline 2?
0x68  Force of Impact
0x6C  Flags
```

### Weapon Model Data: `414968` – `415CDF`  (0x38 per entry)
```
0x00  Model Details RAM Address
0x04  G_Z Text String RAM Address
0x08  Has G_Z Model [00000000=Yes, 00000001=No]
0x0C  Statistics RAM Address
0x10  Name, Upper Watch Equipped
0x12  Name, Lower Watch Equipped
0x14  X Position, Watch Equipped
0x18  Y Position, Watch Equipped
0x1C  Z Position, Watch Equipped
0x20  X Rotation
0x24  Y Rotation
0x28  Name, Weapon of Choice
0x2A  Name, Inventory List
0x2C  X Position, Inventory List
0x30  Y Position, Inventory List
0x34  Z Position, Inventory List
```

### Ammo Icons and Reserve Capacity: `416C84` – `416DEB`  (0xC per entry)
```
0x00  Icon Offset (float)
0x04  Max Reserve Capacity
0x08  Pointer (N64: screen bank offset)
```

### MP Weapon Sets – Weapons: `417728` – `417AE7`  (0x8 per weapon, 8 weapons per set = 0x40 per set)
```
0x00  Weapon ID
0x01  Ammo Type
0x02  Ammo Count
0x03  Weapon Toggle (00=no prop, 01=has prop)
0x04  Prop ID
0x06  Scale
```

### MP Weapon Sets – Select List: `417AE8` – `417BA7`
```
0x00  Text ID
0x04  Address to Weapons
0x08  Flags (00000000 / 01000000 / 01010000)

Order: Random, Slappers Only!, Pistols, Throwing Knives, Automatics,
       Power Weapons, Sniper Rifles, Grenades, Remote Mines, Grenade Launchers,
       Timed Mines, Proximity Mines, Rockets, Lasers, Golden Gun, Hunting Knives
```

---

## Bond Misc

```
4170C7  Bond Phase
4170CB  Bond Invisible (?)
```

---

## Prop Data

### Prop Model Data: `419098` – `5AF347`  (P_Z model data)

### Prop Setup List: `5B21E0` – `5B372F`  (0x10 per entry)
```
0x00  Pointer to model data
0x04  Pointer to internal name string
0x08  Scale (float)
0x0C  Null
```

### Prop Explosion Type List: `5B3730` – `5B3884`  (0x1 per entry)

### Prop Explosion Random Seeds: `5B3888` – `5B3D73`  (0xC per entry)

---

## Character Data

### Character Model Details: `5C19DC` – `5C2C9B`  (0x3C per entry)

### Character Model Data: `5C2C9C` – `71C01F`  (C_Z model data)

### Character Model Setup Table: `71C020` – `71CB83`  (81 entries, 0x24 each)
```
0x00  Pointer (type data, images/matrices/05s, draw distance, etc.)
0x04  Internal Name Pointer
0x08  Pointer
0x0C  Pointer
0x10  Scale (float)
0x14  Ground Offset (float)
0x18  Gender (0=female, 1=male)
0x19  Is/Has Head (0=no, 1=yes)
0x1C  Null
0x20  Null
```

### Headwear Size/Position Table: `71CB88` – `71DB47`  (0x90 per character, 0x18 per style; supports head IDs 2A–45)
```
0x00  X Offset    0x04  Y Offset    0x08  Z Offset
0x0C  X Scale     0x10  Y Scale     0x14  Z Scale
```

---

## Menu & UI Data

### Mission Briefings: `71DF60` – `71E34F`  (21 entries, 0x30 each)
```
0x00  Background Text Entry
0x02  M Brief Text Entry
0x04  Q Branch Text Entry
0x06  Moneypenny Text Entry
0x08–0x2E  Objectives 1–10: [Text Entry 2][Difficulty 2] each
```

### Mission Select Menu: `71E570` – `71E8B7`
(Mixed format: mission chapter entries vs. level entries — see patcher code)

### MP Game Lengths: `71E8B8` – `71E917`  (8 blocks, 0xC each)
```
0x00  Text ID    0x04  Time    0x08  Score
```

### MP Level Select Menu: `71E918` – `71EA7F`  (15 entries, 0x18 each)
```
0x00  Level Name Text Entry (selected)
0x02  Level Name Text Entry (photo)
0x04  Photograph (overridden by level icon table)
0x08  Level ID (FFFFFFFF = random)
0x0C  Unlocked After Mission # (FFFFFFFF = always)
0x10  Minimum Players
0x14  Maximum Players
```

### MP Character Select Menu: `71EA80` – `71ED7F`  (64 entries, 0xC each)
```
0x00  Name Text Entry
0x02  Gender (0=female, 1=male)
0x03  Photograph ID (00=Brosnan, 01=Natalya, 02=Trevelyan, 03=Xenia,
      04=Ourumov, 05=Boris, 06=Valentin, 07=Mishkin, 08=May Day,
      09=Jaws, 0A=Oddjob, 0B=Baron Samedi, 0C=Jungle Commando,
      0D=Arctic Commando, 0E=Civilian 2, 0F=Civilian 4,
      10=Helicopter Pilot, 11=Janus Marine, 12=Moonraker Elite,
      13=Naval Officer, 14=Russian Infantry, 15=Russian Soldier,
      16=Siberian Guard Black, 17=Siberian Guard Brown,
      18=Siberian Special Forces, 19=St. Petersburg Guard, 1A=Who?)
0x04  Body
0x06  Head
0x08  P.O.V. (float)
```

### MP Handicap Health: `71ED80` – `71EDD7`  (11 entries, 0x8 each)
```
0x00  Text Entry    0x02  Handicap Value (float)
```

### MP Controller Types: `71EDD8` – `71EDE7`
```
0x00  Text ID    0x02  Entry Number (00/01/02/03)    0x03  Always 01
```

### MP Sight/Auto-Aim Options: `71EDE8` – `71EDF7`  [UNUSED]

### MP Additional Match Options: `71EDFC` –
```
9D42  Autoaim Disabled
9D44  Crosshairs Disabled
9D48  Unequal Character Heights
```

### Cheat Target Times: `71EE28` – `71EE9F`  (20 entries, 0x6 each)
```
0x00  Agent Time    0x02  Secret Agent Time    0x04  00 Agent Time
```

### Cast Roll: `71EEA0` – `71F147`  (34 entries, 0x14 each)
```
0x00  Body    0x04  Head    0x08–0x0C  Text Entries (×3)
0x10  Intro or End Credits (00=intro, 01=end)
```

### How To Play Text: `71F5AC` – `71F5DB`  (4 pages × 0xC each)

### Main Menu Choices: `71F710` – `71F72F`  (includes 2 unused: LEADERBOARDS, UNLOCK FULL GAME)

### Main Menu Choice Descriptions: `71F734` – `71F753`

### Multiplayer Modes Menu Names: `FAB60` – `FABFB`

---

## Cheat System

### Cheat Unlock By Mission Table: `E7D64` – `E7EA3`
```
E7D64  Extra Multiplayer Characters    E7D68  Invincible
E7D6C  All Guns                        E7D70  Maximum Ammo
E7D74  -invalid-                       E7D78  Deactivate Invincibility
E7D7C  Line Mode                       E7D80  Super 2x Health
E7D84  Super 2x Armour                 E7D88  Bond Invisible
E7D8C  Infinite Ammo                   E7D90  DK Mode
E7D94  Extra Weapons                   E7D98  Tiny Bond
E7D9C  Paintball Mode                  E7DA0  Super 10x Health
E7DA4  Magnum                          E7DA8  Laser
E7DAC  Golden Gun                      E7DB0  Silver PP7
E7DB4  Gold PP7                        E7DB8  Invisible [Multi]
E7DBC  No Radar [Multi]                E7DC0  Turbo Mode
E7DC4  Debug Position Display          E7DC8  Fast Animation
E7DCC  Slow Animation                  E7DD0  Enemy Rockets
E7DD4  2x Rocket Launcher              E7DD8  2x Grenade Launcher
E7DDC  2x RC-P90                       E7DE0  2x Throwing Knife
E7DE4  2x Hunting Knife                E7DE8  2x Laser
— button-code-only cheats —
E7E8C  Unknown?                        E7E90  Invulnerable Chars
E7E94  Stick Insects                   E7E98  Vaseline-o-Vision
E7E9C  Fresco Mode                     E7EA0  Unlock All Cheats
```

### Cheat Unlock Entries (target times / completion): `E7EA4` – `E8137`
```
820ECEA4  Facility (Target Time)     820ECEC0  Egypt (Target Time)
820ECEDC  Frigate (Target Time)      820ECEF8  Statue (Target Time)
820ECF14  Archives (Target Time)     820ECF30  Control (Target Time)
820ECF4C  Runway (Target Time)       820ECF68  Surface 2 (Target Time)
820ECF84  Depot (Target Time)        820ECFA0  Dam (Target Time)
820ECFBC  Train (Target Time)        820ECFD8  Cradle (Target Time)
820ECFF4  Streets (Target Time)      820ED010  Bunker 1 (Target Time)
820ED02C  Surface 1 (Target Time)    820ED048  Caverns (Target Time)
820ED064  Bunker 2 (Target Time)     820ED080  Jungle (Target Time)
820ED09C  Aztec (Target Time)        820ED0B8  Silo (Target Time)
820ED0D4  Cradle (Completion)        820ED0EC  Aztec (Completion)
820ED104  Egypt (Completion)         820ED11C  Not Unlocked
```

### MP Character Unlock
```
EA342   Which mission to complete for unlock (0x11)
EA352   Unlock after Cradle
EA35E   Normal unlock
        0x08 = Normal   0x21 = Cradle   0x40 = Bonus

101C56  System Link MP Characters (0x21)
```

### Cheat Option Button Sequences: `DDFF60` – `DE033B`  (0x14 each)

### Cheat Options Table: `DE0340` – `DE07DF`

### Cheat Option Activations Table: `132088` – `1321AF`

### Cheat Option Activations: `1321B0` – `1329FF`

### Cheat Option Deactivations Table: `132A00` – `132B27`

### Cheat Option Deactivations: `132B28` –

### Vaseline-o-Vision: `183E70`  (set to `60000000` to disable effect)

---

## Intro / End Cast Roll

### Bond Body + Head Variants
```
EAA56 + EAA5A  Suit
EAA62 + EAA66  Jungle
EAA6E + EAA72  Snowsuit
EAA7A + EAA7E  Tuxedo
EAAA2          Natalya
EAAC6          Trevelyan
```

### Cast Roll Weapons
```
EAB74 – EABA3  Single Handed
EABD4 – EAC1B  Dual Wielded
```

### Barrel Walk Intro Gun Shot SFX: `10487E`

---

## Audio

### Mission Music Table: `DE1A30` – `DE1AE7`  (23 entries, 0x8 each)
```
0x00  Level ID    0x02  Main Theme    0x04  Background    0x06  X Track
```

### MP Music Assortment: `DE1AF0` – `DE1B45`  (43 entries, 0x2 each — Song ID)

---

## Hit / Impact Data

```
DE1B48 – DE1C2F  Hits – Sounds & Images
DE1C30 – DE1C64  Hits – Type Table
DE1C64 – DE1C98  Hits – Names Table
```

### Hit Impact Texture Sizes: `DE13E8` – `DE14D7`  (0xC per entry)
```
0x0  Float (Horizontal Scale?)
0x4  Float (Vertical Scale?)
0x8  Other Values
```

---

## Effects

### Smoke Effects: `DE0DA0` – `DE0EA7`  (0x18 per entry)

### Explosion Details: `DE0EA8` – `DE13E7`  (0x40 per entry)

---

## Miscellaneous

### MP Awards: `DE0BE0` – `DE0C01`  (Text ID per entry)

### Gun Model Data: `DE1C98` – `EC7D7F`  (G_Z model data)

### Gun Model Setup: `EC7D80` – `EC92D3`  (0x3C per entry, alphabetical from "ak47")

### MP Profile Name Table: `EC9798` – `EC9A2F`  (pointers to text)

### Random Bodies List: `C7D8C8` – `C7D973`

### Random Female Heads List: `C7D974` – `C7D987`

### Random Male Heads List: `C7D988` – `C7D9EF`

### Pointers to Internal Names List: `C7D9F0` – `C7DAA3`  (sets text banks 00–B0)

### Global Action Block Data: `C7DAB8` – `C7DE9B`

### Global Action Blocks: `C7DEA0` – `C7DF2F`
```
0x00  Address Pointer    0x06  Action Block ID
```

---

## Sky / Fog Data

### Big Skies: `84B860` – `84C50F`  (original 0x5C, new 0x38 per entry)
Includes MP skies for Egyptian, Runway variants, and 5A menu (logo/cast backgrounds).
```
0x00  Level ID
0x02  Blend Multiplier
0x04  Far Fog
0x06  Near Fog
0x08  Max Obj Visibility
0x0A  Far Obj Obfuscation Distance
0x0C  Far Intensity
0x0E  Near Intensity
0x10  Sky Colour R/G/B
0x13  Cloud Enable
0x14  Cloud/Ceiling Height
0x16  ? (water image offset?)
0x17  Cloud Colour R/G/B
0x1A  Water Enable
0x1B  ?
0x1C  Water/Bottom Height
0x1E  Water Image Offset
0x1F  Water Colour R/G/B
0x22  No use? [formerly concavity on N64]
0x23  ?
0x24  ? (XBLA New Near Fog?)
0x28  Fog Colour R/G/B  (NEW ONLY)
0x2B  ?
0x2C  ? (XBLA New Far Fog? — frequently 000124F8)
0x30  ? (affects architecture, models, first-person weapons) [float; higher = guns clip into camera]
0x34  ? (all use B8D1B717)
```

### Small Skies: `84C510` – `84C5EF`  (both 0x38)

---

## Level Setup Regions (Full Map)

### Level ID Data (Legacy, no longer used by CE): `407368` – `40812F`
CE version uses `84AF90` – `84B7DF` instead.

### Level ID Setup (CE Only): `84AF90` – `84B7DF`  (0x38 per entry)
```
0x00  Level ID            0x04  Name Pointer (ark, dest, tra, etc.)
0x08  Scale (float)       0x0C  Visibility (float)
0x10  Unused (float)      0x14  STAN Pointer
0x18  SP Setup Pointer    0x1C  MP Setup Pointer
0x20  BG Data Pointer     0x24+ unknown/unused
```

### STAN (Clipping) Files: `720588` – `84AF3B`
(See STAN section above for slot table and blob structure.)

### Background Files (BG Data): `84C5F0` – `B00ABF`
(Portal data, room positions, visibility. Room data can be simplified.)
```
84C5F0 (828595F0)  Library, Basement & Stack  [0x9F60]
856550 (82863550)  Archives     [0x25AF0]    87C040 (82889040)  Control      [0x2E380]
8AA3C0 (828B73C0)  Facility     [0x30F80]    8DB340 (828E8340)  Aztec        [0x21A50]
8FCD90 (82909D90)  Citadel      [0x5530]     9022C0 (8290F2C0)  Caverns      [0x244F0]
9267B0 (829337B0)  Cradle       [0x10350]    936B00 (82943B00)  Egypt        [0x156B0]
94C1B0 (829591B0)  Dam          [0x301A0]    97C350 (82989350)  Depot        [0x2C970]
9A8CC0 (829B5CC0)  Frigate      [0x2D9C0]    9D6680 (829E3680)  Temple       [0x4870]
9DAEF0 (829E7EF0)  Jungle       [0x150F0]    9EFFE0 (829FCFE0)  Cuba         [0xFA0]
9F0F80 (829FDF80)  Caves        [0x6E50]     9F7DD0 (82A04DD0)  Streets      [0x19C30]
A11A00 (82A1EA00)  Complex      [0x9610]     A1B010 (82A28010)  Runway       [0xA3D0]
A253E0 (82A323E0)  Bunker i     [0x10DF0]    A361D0 (82A431D0)  Bunker ii    [0x1ADA0]
A50F70 (82A5DF70)  Surface i&ii [0x1C5D0]    A6D540 (82A7A540)  Silo         [0x50F40]
ABE480 (82ACB480)  Statue       [0x220D0]    AE0550 (82AED550)  Train        [0x20570]
```

### Mission Campaign Headers (SP Setup Pointers)
```
C94480 (82CA1480)  Archives     CA4CF8 (82CB1CF8)  Control      CBB470 (82CC8470)  Facility
CCC988 (82CD9988)  Aztec        CE2420 (82CEF420)  Caverns      CE7B08 (82CF4B08)  Cradle
CF0BD8 (82CFDBD8)  Egypt        D045F0 (82D115F0)  Dam          D11A40 (82D1EA40)  Depot
D1F3E8 (82D2C3E8)  Frigate      D37440 (82D44440)  Jungle       D39898 (82D46898)  Cuba
D47C40 (82D54C40)  Streets      D4F238 (82D5C238)  Runway       D589C0 (82D659C0)  Bunker 1
D67C10 (82D74C10)  Bunker 2     D787E8 (82D857E8)  Surface 1    D86CD0 (82D93CD0)  Surface 2
D9AAC8 (82DA7AC8)  Silo         DA18C0 (82DAE8C0)  Statue       DB4C50 (82DC1C50)  Train
```

### Multiplayer Headers
```
DB8CC0 (82DC5CC0)  Library MP   [0x4070]    DBB640 (82DC8640)  Archives MP  [0x2980]
DBE990 (82DCB990)  Facility MP  [0x3350]    DC2048 (82DCF048)  Stack MP     [0x36B8]
DC4D18 (82DD1D18)  Caverns MP   [0x2CD0]    DC6508 (82DD3508)  Egypt MP     [0x17F0]
DC9B00 (82DD6B00)  Dam MP       [0x35F8]    DCD038 (82DDA038)  Depot MP     [0x3538]
DD2970 (82DDF970)  Frigate MP   [0x5938]    DD49D8 (82DE19D8)  Temple MP    [0x2068]
DD69A0 (82DE39A0)  Basement MP  [0x1FC8]    DD81F8 (82DE51F8)  Caves MP     [0x1858]
DD9D38 (82DE6D38)  Complex MP   [0x1B40]    DDC028 (82DE9028)  Runway MP    [0x22F0]
DDFF18 (82DECF18)  Bunker 2 MP  [0x3EF0]
```
