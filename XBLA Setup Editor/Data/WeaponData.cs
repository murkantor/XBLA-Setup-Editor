// =============================================================================
// WeaponData.cs - Weapon ID Lookup Table
// =============================================================================
// Maps weapon names to their internal ID codes used in GoldenEye XBLA.
// These IDs are used in the MP Weapon Set editor for weapon selection.
//
// WEAPON ID FORMAT:
// =================
// Each weapon has a unique 8-bit ID (0x00 - 0xFF).
// IDs 0x00-0x20 cover all standard GoldenEye weapons.
//
// USAGE:
// ======
// - WeaponData.Pairs: Array of (Name, Code) for dropdown population
// - WeaponData.Build(): Creates dictionary for name-to-code lookups
// =============================================================================

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Contains weapon name-to-ID mappings for GoldenEye XBLA.
    /// Used by the MP Weapon Set editor for weapon selection dropdowns.
    /// </summary>
    internal static class WeaponData
    {
        /// <summary>
        /// Array of (Name, Code) pairs for all weapons.
        /// Names are human-readable, codes are the internal IDs used by the game.
        /// </summary>
        internal static readonly (string Name, int Code)[] Pairs =
        {
            // Special entries
            ("Nothing (No Pickup)", 0x00),  // Empty slot / no weapon
            ("Unarmed", 0x01),              // Hand-to-hand combat

            // Melee weapons
            ("Hunting Knives", 0x02),
            ("Throwing Knives", 0x03),

            // Pistols
            ("PP7", 0x04),                  // Standard sidearm
            ("PP7 (Silenced)", 0x05),       // Silenced variant
            ("DD44", 0x06),                 // Dostovei
            ("Cougar Magnum", 0x12),
            ("Golden Gun", 0x13),           // One-hit kill pistol
            ("Silver PP7", 0x14),
            ("Gold PP7", 0x15),

            // Submachine guns
            ("Klobb", 0x07),                // Low damage, high spread
            ("ZMG", 0x09),                  // 9mm submachine gun
            ("D5K", 0x0A),                  // Deutsche SMG
            ("D5K (Silenced)", 0x0B),
            ("Phantom", 0x0C),

            // Rifles
            ("KF7", 0x08),                  // Soviet assault rifle
            ("AR33", 0x0D),                 // Assault rifle
            ("RC-P90", 0x0E),               // High capacity SMG
            ("Sniper Rifle", 0x11),

            // Shotguns
            ("Shotgun", 0x0F),
            ("Automatic Shotgun", 0x10),

            // Special weapons
            ("Moonraker Laser", 0x16),
            ("Watch Laser", 0x17),
            ("Tazer", 0x1F),

            // Explosives
            ("Grenade Launcher", 0x18),
            ("Rocket Launcher", 0x19),
            ("Grenades", 0x1A),
            ("Timed Mine", 0x1B),
            ("Proximity Mine", 0x1C),
            ("Remote Mine", 0x1D),
            ("Detonator", 0x1E),            // For remote mines

            // Vehicle
            ("Tank", 0x20),
        };

        /// <summary>
        /// Builds a case-insensitive dictionary for name-to-code lookups.
        /// </summary>
        /// <returns>Dictionary mapping weapon names to their codes.</returns>
        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
