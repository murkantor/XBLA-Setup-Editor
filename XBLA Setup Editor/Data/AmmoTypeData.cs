// =============================================================================
// AmmoTypeData.cs - Ammunition Type Lookup Table
// =============================================================================
// Maps ammunition type names to their internal ID codes used in GoldenEye XBLA.
// These IDs are used in the MP Weapon Set editor for ammo type selection.
//
// AMMO TYPE FORMAT:
// =================
// Each ammo type has a unique 8-bit ID.
// Most standard ammo types use IDs 0x00-0x0D.
// Special types like Tank use higher IDs (0xC1).
//
// NOTE: Not all weapons share ammo types. Some have unique ammo
// (e.g., Golden Gun uses Golden Bullets, Moonraker uses battery power).
// =============================================================================

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Contains ammunition type name-to-ID mappings for GoldenEye XBLA.
    /// Used by the MP Weapon Set editor for ammo type selection dropdowns.
    /// </summary>
    internal static class AmmoTypeData
    {
        /// <summary>
        /// Array of (Name, Code) pairs for all ammunition types.
        /// Names are human-readable, codes are the internal IDs.
        /// </summary>
        internal static readonly (string Name, int Code)[] Pairs =
        {
            // No ammo (melee, special weapons)
            ("None", 0x00),

            // Standard bullet types
            ("9mm Ammo", 0x01),              // Pistols, SMGs
            ("Rifle Ammo", 0x03),            // Rifles, sniper
            ("Cartridges", 0x04),            // Shotguns
            ("Magnum Bullets", 0x0C),        // Cougar Magnum
            ("Golden Bullets", 0x0D),        // Golden Gun (limited supply)

            // Explosive ordnance
            ("Grenades", 0x05),              // Throwable grenades
            ("Rockets", 0x06),               // Rocket launcher ammo
            ("Grenade Rounds", 0x0B),        // Grenade launcher ammo

            // Mines
            ("Remote Mines", 0x07),
            ("Proximity Mines", 0x08),
            ("Timed Mines", 0x09),

            // Special
            ("Knife", 0x0A),                 // Throwing knives ammo
            ("Watch Laser", 0x18),           // Watch laser energy
            ("Tank", 0xC1),                  // Tank shells
        };

        /// <summary>
        /// Builds a case-insensitive dictionary for name-to-code lookups.
        /// </summary>
        /// <returns>Dictionary mapping ammo type names to their codes.</returns>
        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
