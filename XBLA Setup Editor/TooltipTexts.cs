namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Central repository of all tooltip strings for the application.
    /// Organized by tab/control for easy maintenance.
    /// </summary>
    public static class TooltipTexts
    {
        // ========================================
        // STR Editor Tab
        // ========================================
        public static class StrEditor
        {
            public const string Open = "Open a .str or .adb string database file";
            public const string Save = "Save changes to the current file";
            public const string SaveAs = "Save the file with a new name";
            public const string AddEntry = "Add a new text entry with auto-incremented Bank/ID";
            public const string RemoveEntry = "Remove the selected text entry";
            public const string BankColumn = "String bank number (hex). Groups related strings together";
            public const string IdColumn = "String ID within the bank (hex). Unique identifier for this text";
            public const string TextColumn = "The text string content";
        }

        // ========================================
        // Setup Patching Tab
        // ========================================
        public static class SetupPatching
        {
            public const string SoloMode = "Solo mode converts single-player setups";
            public const string MultiMode = "Multi mode converts multiplayer setups";
            public const string LevelDropdown = "Select the level to convert. Each level has a specific memory offset";
            public const string MemoryOffset = "The memory address where this level's setup data is loaded (read-only)";
            public const string InputSetup = "The setup file to convert";
            public const string OutputSetup = "The output .bin file path";
            public const string BatchInputDir = "Folder containing setup files to batch convert (UsetuparchZ, etc.)";
            public const string BatchOutputDir = "Folder where converted .bin files will be saved";
            public const string PatchXex = "After batch conversion, patch the converted setups into the XEX file";
            public const string AllowMpPool = "Allow using multiplayer memory pool for solo levels. May cause issues in MP mode";
            public const string ExtendXex = "If setups don't fit, extend the XEX file with additional memory";
            public const string ForceRepack = "Repack setups to optimize memory layout. Recommended for large mods";
            public const string SplitTwoXex = "If setups are too large, split across two separate XEX files";
            public const string InputXex = "The source XEX file to patch";
            public const string OutputXex1 = "The output XEX file (or first file if split)";
            public const string OutputXex2 = "The second output XEX file (only used when split mode is enabled)";
        }

        // ========================================
        // 21990 Import Tab
        // ========================================
        public static class File21990
        {
            public const string FilePath = "The N64 configuration file containing level data (sky, music, menu text)";
            public const string InputXex = "The XEX file to patch with 21990 data";
            public const string OutputXex = "The output XEX file path";
            public const string CreateBackup = "Create a .backup copy of the XEX before patching";
            public const string ApplyMenuData = "Import level names and descriptions from 21990 file";
            public const string ApplySkyFog = "Import sky color and fog settings for each level";
            public const string SkyToFog = "Also apply sky colors to fog (creates cohesive atmosphere)";
            public const string N64FogDistances = "Use original N64 fog distances instead of XBLA values";
            public const string ApplyMusic = "Import music track assignments for each level";
            public const string Analyze = "Parse the 21990 file and display its contents";
            public const string ApplyPatches = "Apply selected patches to the XEX file";
        }

        // ========================================
        // XEX Extender Tab
        // ========================================
        public static class XexExtender
        {
            public const string InputXex = "The XEX file to extend";
            public const string DataFile = "Binary file to append to the XEX (will be accessible at shown address)";
            public const string OutputXex = "The output extended XEX file path";
            public const string RecalcSha1 = "Update the XEX hash after extension (may affect compatibility)";
            public const string CreateBackup = "Create a .backup copy before overwriting";
            public const string Analyze = "Analyze the XEX structure and show where new data will be placed";
            public const string Extend = "Extend the XEX file with the selected data";
        }

        // ========================================
        // MP Weapon Sets Tab
        // ========================================
        public static class MpWeaponSets
        {
            public const string XexFile = "The XEX file containing weapon set data";
            public const string Backup = "Create a .backup copy of the XEX before saving";
            public const string RemoveArmor =
                "Scans XEX setup region (0xC7DF38-0xDDFF5F) and overwrites armor objects (type 0x15) with zeros.\n" +
                "File size remains unchanged - objects are NOPed in place.";
            public const string TextFolder =
                "3-letter folder code written into XEX at 0x0000A3AC.\n" +
                "Example: ENG, FRA, DEU, etc.";
            public const string WeaponSetsList = "Select a weapon set to edit. Each set defines starting weapons for a mode";
            public const string TextId = "Text string ID for the weapon set name (hex)";
            public const string BeginnerMode = "Auto-fill ammo type, count, and prop settings based on weapon selection";
            public const string WeaponColumn = "The weapon players start with in this slot";
            public const string AmmoTypeColumn = "Ammunition type for this weapon";
            public const string AmmoCountColumn = "Starting ammo count";
            public const string HasPropColumn = "Whether this weapon appears as a pickup on the ground";
            public const string PropModelColumn = "The 3D model used when weapon is on the ground";
            public const string ScaleColumn = "Size multiplier for the prop model";
        }

        // ========================================
        // Weapon Stats Tab
        // ========================================
        public static class WeaponStats
        {
            public const string XexFile = "The XEX file containing weapon stats data";
            public const string Backup = "Create a .backup copy of the XEX before saving";
            public const string Import21990 = "Import weapon stats from an N64 21990 file";
            public const string UseXblaLayout = "Use XBLA field positions when parsing 21990 (vs N64 layout)";
            public const string PreserveRamAddrs = "Keep XBLA RAM addresses instead of importing N64 addresses";

            // Grid columns
            public const string Damage = "Damage dealt per hit";
            public const string MagazineSize = "Number of rounds in a magazine";
            public const string AmmoType = "Type of ammunition used";
            public const string FireAuto = "Automatic fire capability flags";
            public const string FireSingle = "Single shot fire capability flags";
            public const string Penetration = "Armor penetration value";
            public const string Inaccuracy = "Weapon inaccuracy/spread";
            public const string Scope = "Scope zoom level";
            public const string CrosshairSpeed = "Speed of crosshair movement";
            public const string LockOnSpeed = "Speed of aim lock-on";
            public const string Sway = "Weapon sway amount";
            public const string RecoilBackward = "Recoil force pushing backward";
            public const string RecoilUpward = "Recoil force pushing upward";
            public const string RecoilBolt = "Bolt action recoil";
            public const string MuzzleFlash = "Muzzle flash extension";
            public const string ScreenPosition = "On-screen weapon position (X/Y/Z)";
            public const string AimShift = "Aim position shift values";
            public const string SoundFx = "Sound effect ID";
            public const string SoundRate = "Sound trigger rate";
            public const string AiVolume = "Volume heard by AI";
            public const string Impact = "Force of impact on targets";
            public const string CasingsRam = "RAM address for ejected casings";
            public const string Flags = "Weapon behavior flags";
        }

        // ========================================
        // Main Form / Shared XEX/21990 Controls
        // ========================================
        public static class MainForm
        {
            public const string XexFilePath = "Path to the XEX file shared across all tabs";
            public const string BrowseXex = "Browse for a XEX file to load";
            public const string LoadXex = "Load the XEX file into memory for editing";
            public const string SaveXex = "Save all changes to the XEX file";
            public const string CreateBackup = "Create a .backup copy before saving";
            public const string File21990Path = "Path to the 21990 file shared across 21990 Import and Weapon Stats tabs";
            public const string Browse21990 = "Browse for a 21990 configuration file";
            public const string Load21990 = "Load the 21990 file and distribute to all tabs that use it";
            public const string Status = "Current status of the loaded XEX and any modifications";
        }
    }
}
