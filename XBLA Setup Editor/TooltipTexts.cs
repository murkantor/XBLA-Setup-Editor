// =============================================================================
// TooltipTexts.cs - Centralized Tooltip String Repository
// =============================================================================
// This file contains all tooltip text strings used throughout the application.
// Centralizing tooltips provides several benefits:
//
// 1. MAINTAINABILITY: All user-facing help text in one place
// 2. CONSISTENCY: Ensures uniform tone and terminology
// 3. LOCALIZATION: Easy to translate if needed in the future
// 4. DOCUMENTATION: Serves as a quick reference for UI elements
//
// ORGANIZATION:
// =============
// Tooltips are organized by the tab/control they appear in:
// - StrEditor: String database editor tooltips
// - SetupPatching: Level setup conversion tooltips
// - File21990: N64 configuration importer tooltips
// - XexExtender: XEX file extension tooltips (experimental)
// - MpWeaponSets: Multiplayer weapon set editor tooltips
// - WeaponStats: Weapon statistics editor tooltips
// - MainForm: Shared XEX/21990 control tooltips
//
// USAGE:
// ======
// Reference tooltips like: myControl.ToolTip = TooltipTexts.StrEditor.Open;
// =============================================================================

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Central repository of all tooltip strings for the application.
    /// Organized by tab/control for easy maintenance.
    /// </summary>
    public static class TooltipTexts
    {
        // =====================================================================
        // STR EDITOR TAB
        // =====================================================================
        // Tooltips for the string database (STR/ADB) editor control.
        // This tab allows editing localized game text.

        /// <summary>Tooltip texts for the STR Editor tab.</summary>
        public static class StrEditor
        {
            /// <summary>Open file button.</summary>
            public const string Open = "Open a .str or .adb string database file";

            /// <summary>Save file button.</summary>
            public const string Save = "Save changes to the current file";

            /// <summary>Save As button.</summary>
            public const string SaveAs = "Save the file with a new name";

            /// <summary>Add entry button.</summary>
            public const string AddEntry = "Add a new text entry with auto-incremented Bank/ID";

            /// <summary>Remove entry button.</summary>
            public const string RemoveEntry = "Remove the selected text entry";

            /// <summary>Bank column in grid.</summary>
            public const string BankColumn = "String bank number (hex). Groups related strings together";

            /// <summary>ID column in grid.</summary>
            public const string IdColumn = "String ID within the bank (hex). Unique identifier for this text";

            /// <summary>Text column in grid.</summary>
            public const string TextColumn = "The text string content";
        }

        // =====================================================================
        // SETUP PATCHING TAB
        // =====================================================================
        // Tooltips for the level setup conversion and patching control.
        // This tab converts N64 setup files to XBLA format and patches them
        // into the XEX file.

        /// <summary>Tooltip texts for the Setup Patching tab.</summary>
        public static class SetupPatching
        {
            /// <summary>Solo mode radio button.</summary>
            public const string SoloMode = "Solo mode converts single-player setups";

            /// <summary>Multi mode radio button.</summary>
            public const string MultiMode = "Multi mode converts multiplayer setups";

            /// <summary>Level selection dropdown.</summary>
            public const string LevelDropdown = "Select the level to convert. Each level has a specific memory offset";

            /// <summary>Memory offset display.</summary>
            public const string MemoryOffset = "The memory address where this level's setup data is loaded (read-only)";

            /// <summary>Input setup file path.</summary>
            public const string InputSetup = "The setup file to convert";

            /// <summary>Output setup file path.</summary>
            public const string OutputSetup = "The output .bin file path";

            /// <summary>Batch input directory.</summary>
            public const string BatchInputDir = "Folder containing setup files to batch convert (UsetuparchZ, etc.)";

            /// <summary>Batch output directory.</summary>
            public const string BatchOutputDir = "Folder where converted .bin files will be saved";

            /// <summary>Patch XEX checkbox.</summary>
            public const string PatchXex = "After batch conversion, patch the converted setups into the XEX file";

            /// <summary>Allow Multiplayer region overflow checkbox.</summary>
            public const string AllowMpPool = "Allow using the Multiplayer region overflow for solo levels. May cause issues in Multiplayer mode";

            /// <summary>Extend XEX checkbox.</summary>
            public const string ExtendXex = "If setups don't fit, extend the XEX file with additional memory";

            /// <summary>Force repack checkbox.</summary>
            public const string ForceRepack = "Repack setups to optimize memory layout. Recommended for large mods";

            /// <summary>Split two XEX checkbox.</summary>
            public const string SplitTwoXex = "If setups are too large, split across two separate XEX files";

            /// <summary>Input XEX file path.</summary>
            public const string InputXex = "The source XEX file to patch";

            /// <summary>First output XEX file path.</summary>
            public const string OutputXex1 = "The output XEX file (or first file if split)";

            /// <summary>Second output XEX file path.</summary>
            public const string OutputXex2 = "The second output XEX file (only used when split mode is enabled)";
        }

        // =====================================================================
        // 21990 IMPORT TAB
        // =====================================================================
        // Tooltips for the 21990 file importer control.
        // This tab imports N64 configuration data (sky colors, fog, music)
        // into the XBLA XEX file.

        /// <summary>Tooltip texts for the 21990 Import tab.</summary>
        public static class File21990
        {
            /// <summary>21990 file path input.</summary>
            public const string FilePath = "The N64 configuration file containing level data (sky, music, menu text)";

            /// <summary>Input XEX file path.</summary>
            public const string InputXex = "The XEX file to patch with 21990 data";

            /// <summary>Output XEX file path.</summary>
            public const string OutputXex = "The output XEX file path";

            /// <summary>Create backup checkbox.</summary>
            public const string CreateBackup = "Create a .backup copy of the XEX before patching";

            /// <summary>Apply menu data checkbox.</summary>
            public const string ApplyMenuData = "Import level names and descriptions from 21990 file";

            /// <summary>Apply sky/fog checkbox.</summary>
            public const string ApplySkyFog = "Import sky color and fog settings for each level";

            /// <summary>Sky to fog checkbox.</summary>
            public const string SkyToFog = "Also apply sky colors to fog (creates cohesive atmosphere)";

            /// <summary>N64 fog distances checkbox.</summary>
            public const string N64FogDistances = "Use original N64 fog distances instead of XBLA values";

            /// <summary>Apply music checkbox.</summary>
            public const string ApplyMusic = "Import music track assignments for each level";

            /// <summary>Analyze button.</summary>
            public const string Analyze = "Parse the 21990 file and display its contents";

            /// <summary>Apply patches button.</summary>
            public const string ApplyPatches = "Apply selected patches to the XEX file";
        }

        // =====================================================================
        // XEX EXTENDER TAB
        // =====================================================================
        // Tooltips for the experimental XEX extension control.
        // This tab attempts to extend the XEX file with additional data.
        // NOTE: This feature is marked as not fully working.

        /// <summary>Tooltip texts for the XEX Extender tab (experimental).</summary>
        public static class XexExtender
        {
            /// <summary>Input XEX file path.</summary>
            public const string InputXex = "The XEX file to extend";

            /// <summary>Data file to append.</summary>
            public const string DataFile = "Binary file to append to the XEX (will be accessible at shown address)";

            /// <summary>Output XEX file path.</summary>
            public const string OutputXex = "The output extended XEX file path";

            /// <summary>Recalculate SHA1 checkbox.</summary>
            public const string RecalcSha1 = "Update the XEX hash after extension (may affect compatibility)";

            /// <summary>Create backup checkbox.</summary>
            public const string CreateBackup = "Create a .backup copy before overwriting";

            /// <summary>Analyze button.</summary>
            public const string Analyze = "Analyze the XEX structure and show where new data will be placed";

            /// <summary>Use zero_size method checkbox.</summary>
            public const string UseZeroSize =
                "Convert the last block's zero_size (BSS zero memory) to data_size instead of consuming image_size headroom.\n" +
                "Not limited to ~32KB. Requires that the target zero region is not used by the game at runtime.\n" +
                "Run Analyze to see how much zero_size capacity is available and at which address.";

            /// <summary>Extend button.</summary>
            public const string Extend = "Extend the XEX file with the selected data";
        }

        // =====================================================================
        // MP WEAPON SETS TAB
        // =====================================================================
        // Tooltips for the multiplayer weapon set editor.
        // This tab allows editing the 16 weapon sets available in multiplayer,
        // including starting weapons, ammo, and prop models.

        /// <summary>Tooltip texts for the MP Weapon Sets tab.</summary>
        public static class MpWeaponSets
        {
            /// <summary>XEX file path input.</summary>
            public const string XexFile = "The XEX file containing weapon set data";

            /// <summary>Backup checkbox.</summary>
            public const string Backup = "Create a .backup copy of the XEX before saving";

            /// <summary>Remove armor button - detailed explanation of the operation.</summary>
            public const string RemoveArmor =
                "Scans XEX setup region (0xC7DF38-0xDDFF5F) and overwrites armor objects (type 0x15) with zeros.\n" +
                "File size remains unchanged - objects are NOPed in place.";

            /// <summary>Text folder input.</summary>
            public const string TextFolder =
                "3-letter folder code written into XEX at 0x0000A3AC.\n" +
                "Example: ENG, FRA, DEU, etc.";

            /// <summary>Weapon sets list.</summary>
            public const string WeaponSetsList = "Select a weapon set to edit. Each set defines starting weapons for a mode";

            /// <summary>Text ID column.</summary>
            public const string TextId = "Text string ID for the weapon set name (hex)";

            /// <summary>Beginner mode checkbox.</summary>
            public const string BeginnerMode = "Auto-fill ammo type, count, and prop settings based on weapon selection";

            /// <summary>Weapon column in grid.</summary>
            public const string WeaponColumn = "The weapon players start with in this slot";

            /// <summary>Ammo type column in grid.</summary>
            public const string AmmoTypeColumn = "Ammunition type for this weapon";

            /// <summary>Ammo count column in grid.</summary>
            public const string AmmoCountColumn = "Starting ammo count";

            /// <summary>Has prop column in grid.</summary>
            public const string HasPropColumn = "Whether this weapon appears as a pickup on the ground";

            /// <summary>Prop model column in grid.</summary>
            public const string PropModelColumn = "The 3D model used when weapon is on the ground";

            /// <summary>Scale column in grid.</summary>
            public const string ScaleColumn = "Size multiplier for the prop model";
        }

        // =====================================================================
        // WEAPON STATS TAB
        // =====================================================================
        // Tooltips for the weapon statistics editor.
        // This tab allows editing detailed weapon parameters like damage,
        // accuracy, recoil, magazine size, and more.

        /// <summary>Tooltip texts for the Weapon Stats tab.</summary>
        public static class WeaponStats
        {
            /// <summary>XEX file path input.</summary>
            public const string XexFile = "The XEX file containing weapon stats data";

            /// <summary>Backup checkbox.</summary>
            public const string Backup = "Create a .backup copy of the XEX before saving";

            /// <summary>Import 21990 button.</summary>
            public const string Import21990 = "Import weapon stats from an N64 21990 file";

            /// <summary>Use XBLA layout checkbox.</summary>
            public const string UseXblaLayout = "Use XBLA field positions when parsing 21990 (vs N64 layout)";

            /// <summary>Preserve RAM addresses checkbox.</summary>
            public const string PreserveRamAddrs = "Keep XBLA RAM addresses instead of importing N64 addresses";

            // -----------------------------------------------------------------
            // GRID COLUMN TOOLTIPS
            // -----------------------------------------------------------------
            // These describe each editable weapon statistic.

            /// <summary>Damage column.</summary>
            public const string Damage = "Damage dealt per hit";

            /// <summary>Magazine size column.</summary>
            public const string MagazineSize = "Number of rounds in a magazine";

            /// <summary>Ammo type column.</summary>
            public const string AmmoType = "Type of ammunition used";

            /// <summary>Fire auto column.</summary>
            public const string FireAuto = "Automatic fire capability flags";

            /// <summary>Fire single column.</summary>
            public const string FireSingle = "Single shot fire capability flags";

            /// <summary>Penetration column.</summary>
            public const string Penetration = "Armor penetration value";

            /// <summary>Inaccuracy column.</summary>
            public const string Inaccuracy = "Weapon inaccuracy/spread";

            /// <summary>Scope column.</summary>
            public const string Scope = "Scope zoom level";

            /// <summary>Crosshair speed column.</summary>
            public const string CrosshairSpeed = "Speed of crosshair movement";

            /// <summary>Lock-on speed column.</summary>
            public const string LockOnSpeed = "Speed of aim lock-on";

            /// <summary>Sway column.</summary>
            public const string Sway = "Weapon sway amount";

            /// <summary>Recoil backward column.</summary>
            public const string RecoilBackward = "Recoil force pushing backward";

            /// <summary>Recoil upward column.</summary>
            public const string RecoilUpward = "Recoil force pushing upward";

            /// <summary>Recoil bolt column.</summary>
            public const string RecoilBolt = "Bolt action recoil";

            /// <summary>Muzzle flash column.</summary>
            public const string MuzzleFlash = "Muzzle flash extension";

            /// <summary>Screen position column.</summary>
            public const string ScreenPosition = "On-screen weapon position (X/Y/Z)";

            /// <summary>Aim shift column.</summary>
            public const string AimShift = "Aim position shift values";

            /// <summary>Sound FX column.</summary>
            public const string SoundFx = "Sound effect ID";

            /// <summary>Sound rate column.</summary>
            public const string SoundRate = "Sound trigger rate";

            /// <summary>AI volume column.</summary>
            public const string AiVolume = "Volume heard by AI";

            /// <summary>Impact column.</summary>
            public const string Impact = "Force of impact on targets";

            /// <summary>Casings RAM column.</summary>
            public const string CasingsRam = "RAM address for ejected casings";

            /// <summary>Flags column.</summary>
            public const string Flags = "Weapon behavior flags";
        }

        // =====================================================================
        // MAIN FORM / SHARED CONTROLS
        // =====================================================================
        // Tooltips for the shared XEX and 21990 file controls in the main form.
        // These controls are shared across multiple tabs.

        /// <summary>Tooltip texts for the Main Form shared controls.</summary>
        public static class MainForm
        {
            /// <summary>XEX file path input.</summary>
            public const string XexFilePath = "Path to the XEX file shared across all tabs";

            /// <summary>Browse XEX button.</summary>
            public const string BrowseXex = "Browse for a XEX file to load";

            /// <summary>Load XEX button.</summary>
            public const string LoadXex = "Load the XEX file into memory for editing";

            /// <summary>Save XEX button.</summary>
            public const string SaveXex = "Save all changes to the XEX file";

            /// <summary>Create backup checkbox.</summary>
            public const string CreateBackup = "Create a .backup copy before saving";

            /// <summary>21990 file path input.</summary>
            public const string File21990Path = "Path to the 21990 file shared across 21990 Import and Weapon Stats tabs";

            /// <summary>Browse 21990 button.</summary>
            public const string Browse21990 = "Browse for a 21990 configuration file";

            /// <summary>Load 21990 button.</summary>
            public const string Load21990 = "Load the 21990 file and distribute to all tabs that use it";

            /// <summary>Status bar.</summary>
            public const string Status = "Current status of the loaded XEX and any modifications";
        }
    }
}
