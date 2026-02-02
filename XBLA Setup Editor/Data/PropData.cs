// =============================================================================
// PropData.cs - Prop/Model ID Lookup Table
// =============================================================================
// Maps weapon prop names to their 3D model IDs used in GoldenEye XBLA.
// These IDs determine which model is displayed when a weapon is placed
// on the ground as a pickup in multiplayer.
//
// PROP ID FORMAT:
// ===============
// Each prop has a unique 8-bit ID, typically in the 0xB8-0xE7 range.
// IDs below 0x60 are generally reserved or unused for weapon props.
//
// NOTE: Some weapons share the same prop ID:
// - Watch Laser, Tank, Detonator, Tazer all use 0xC6
// - Nothing and Unarmed both use 0x60 (no visible model)
//
// USAGE:
// ======
// When a weapon slot has "HasProp" enabled, the prop ID determines
// which model spawns on the ground at match start.
// =============================================================================

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Contains weapon prop name-to-model-ID mappings for GoldenEye XBLA.
    /// Used by the MP Weapon Set editor for prop model selection.
    /// </summary>
    internal static class PropData
    {
        /// <summary>
        /// Array of (Name, Code) pairs for all weapon props.
        /// Names match weapon names, codes are the 3D model IDs.
        /// </summary>
        /// <remarks>
        /// Some weapons share model IDs:
        /// - 0x60: Nothing/Unarmed (no visible model)
        /// - 0xC6: Watch Laser/Tank/Detonator/Tazer (shared small prop)
        /// </remarks>
        internal static readonly (string Name, int Code)[] Pairs =
        {
            // No visible prop
            ("Nothing (No Pickup)", 0x60),
            ("Unarmed", 0x60),

            // Rifle models
            ("KF7", 0xB8),
            ("AR33", 0xBC),
            ("Phantom", 0xC2),
            ("Sniper Rifle", 0xD2),

            // SMG models
            ("D5K", 0xBD),
            ("D5K (Silenced)", 0xCE),
            ("Klobb", 0xC1),
            ("ZMG", 0xC3),
            ("RC-P90", 0xC5),

            // Pistol models
            ("PP7", 0xBF),
            ("PP7 (Silenced)", 0xCC),
            ("DD44", 0xCD),
            ("Cougar Magnum", 0xBE),
            ("Golden Gun", 0xD0),
            ("Silver PP7", 0xE6),
            ("Gold PP7", 0xE7),

            // Shotgun models
            ("Shotgun", 0xC0),
            ("Automatic Shotgun", 0xCF),

            // Special weapon models
            ("Moonraker Laser", 0xBB),
            ("Grenade Launcher", 0xB9),
            ("Rocket Launcher", 0xD3),
            ("Hunting Knives", 0xBA),
            ("Throwing Knives", 0xD1),

            // Explosive models
            ("Grenades", 0xC4),
            ("Remote Mines", 0xC7),
            ("Proximity Mines", 0xC8),
            ("Timed Mines", 0xC9),

            // Shared model ID (small/misc props)
            ("Watch Laser", 0xC6),
            ("Tank", 0xC6),
            ("Detonator", 0xC6),
            ("Tazer", 0xC6),
        };

        /// <summary>
        /// Builds a case-insensitive dictionary for name-to-code lookups.
        /// </summary>
        /// <returns>Dictionary mapping prop names to their model codes.</returns>
        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
