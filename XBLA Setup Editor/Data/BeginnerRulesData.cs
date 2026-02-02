// =============================================================================
// BeginnerRulesData.cs - Auto-Fill Rules for Beginner Mode
// =============================================================================
// Provides default values for weapon configurations when "Beginner Mode"
// is enabled in the MP Weapon Set editor. This simplifies weapon set
// creation by automatically filling in appropriate ammo types and counts.
//
// PURPOSE:
// ========
// When a user selects a weapon in "Beginner Mode", the editor automatically:
// 1. Sets the correct ammo type for that weapon
// 2. Sets a reasonable default ammo count
// 3. Enables/disables the prop based on weapon type
//
// AMMO TYPE RULES:
// ================
// - Pistols/SMGs → "9mm Ammo"
// - Rifles → "Rifle Ammo"
// - Shotguns → "Cartridges"
// - Explosives → Matching explosive type
// - Melee/Special → "None"
//
// DEFAULT AMMO COUNTS:
// ====================
// Counts are balanced for typical multiplayer gameplay:
// - Pistols: 50 rounds
// - SMGs: 100 rounds
// - Rifles: 40-100 rounds
// - Shotguns: 30 shells
// - Explosives: 5-6 items
// - Knives: 1-10 depending on type
// =============================================================================

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Default weapon configurations for "Beginner Mode" auto-fill.
    /// Maps weapons to their appropriate ammo types and default counts.
    /// </summary>
    internal static class BeginnerRulesData
    {
        // =====================================================================
        // WEAPON TO AMMO TYPE MAPPING
        // =====================================================================

        /// <summary>
        /// Maps weapon names to their appropriate ammunition type names.
        /// Used for auto-filling the "Ammo Type" column in Beginner Mode.
        /// </summary>
        internal static readonly Dictionary<string, string> WeaponToAmmoType =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // No ammo weapons
                { "Nothing (No Pickup)", "None" },
                { "Unarmed", "None" },
                { "Moonraker Laser", "None" },  // Uses battery/infinite
                { "Detonator", "None" },        // Just a trigger device
                { "Tazer", "None" },

                // Melee weapons
                { "Hunting Knives", "Knife" },
                { "Throwing Knives", "Knife" },

                // 9mm pistols and SMGs
                { "PP7", "9mm Ammo" },
                { "PP7 (Silenced)", "9mm Ammo" },
                { "DD44", "9mm Ammo" },
                { "Klobb", "9mm Ammo" },
                { "ZMG", "9mm Ammo" },
                { "D5K", "9mm Ammo" },
                { "D5K (Silenced)", "9mm Ammo" },
                { "RC-P90", "9mm Ammo" },
                { "Silver PP7", "9mm Ammo" },
                { "Gold PP7", "9mm Ammo" },

                // Rifle ammo weapons
                { "KF7", "Rifle Ammo" },
                { "Phantom", "Rifle Ammo" },
                { "AR33", "Rifle Ammo" },
                { "Sniper Rifle", "Rifle Ammo" },

                // Shotguns
                { "Shotgun", "Cartridges" },
                { "Automatic Shotgun", "Cartridges" },

                // Special ammo types
                { "Cougar Magnum", "Magnum Bullets" },
                { "Golden Gun", "Golden Bullets" },
                { "Watch Laser", "Watch Laser" },

                // Explosives
                { "Grenade Launcher", "Grenade Rounds" },
                { "Rocket Launcher", "Rockets" },
                { "Grenades", "Grenades" },
                { "Timed Mine", "Timed Mines" },
                { "Proximity Mine", "Proximity Mines" },
                { "Remote Mine", "Remote Mines" },

                // Vehicle
                { "Tank", "Tank" },
            };

        // =====================================================================
        // WEAPON TO DEFAULT AMMO COUNT MAPPING
        // =====================================================================

        /// <summary>
        /// Maps weapon names to their default starting ammo counts.
        /// Used for auto-filling the "Ammo Count" column in Beginner Mode.
        /// </summary>
        /// <remarks>
        /// These values are balanced for typical multiplayer gameplay.
        /// High-capacity weapons get more ammo, powerful weapons get less.
        /// </remarks>
        internal static readonly Dictionary<string, string> WeaponToDefaultAmmoCount =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // No ammo
                { "Nothing (No Pickup)", "0" },
                { "Unarmed", "0" },
                { "Moonraker Laser", "0" },
                { "Detonator", "0" },

                // Melee
                { "Hunting Knives", "1" },   // Infinite use, just need 1
                { "Throwing Knives", "10" },
                { "Tazer", "1" },

                // Pistols (moderate ammo)
                { "PP7", "50" },
                { "PP7 (Silenced)", "50" },
                { "DD44", "50" },
                { "Cougar Magnum", "50" },
                { "Sniper Rifle", "50" },
                { "Silver PP7", "10" },      // Rare variants get less
                { "Gold PP7", "10" },
                { "Golden Gun", "10" },      // Very powerful, limited ammo

                // SMGs (high ammo capacity)
                { "Klobb", "100" },
                { "ZMG", "100" },
                { "D5K", "100" },
                { "D5K (Silenced)", "100" },
                { "Phantom", "100" },
                { "RC-P90", "100" },
                { "Watch Laser", "100" },

                // Rifles (moderate to high)
                { "KF7", "100" },
                { "AR33", "40" },

                // Shotguns
                { "Shotgun", "30" },
                { "Automatic Shotgun", "30" },

                // Explosives (limited supply)
                { "Grenade Launcher", "6" },
                { "Rocket Launcher", "6" },
                { "Grenades", "5" },
                { "Timed Mine", "5" },
                { "Proximity Mine", "5" },
                { "Remote Mine", "5" },

                // Vehicle
                { "Tank", "5" },             // Tank shells
            };

        // =====================================================================
        // PROP NAME LOOKUP
        // =====================================================================

        /// <summary>Cached set of valid weapon names that can have props.</summary>
        private static HashSet<string>? _propNames;

        /// <summary>
        /// Gets the set of all valid weapon names (derived from WeaponData).
        /// Used to determine if a weapon should have a prop model.
        /// </summary>
        internal static HashSet<string> PropNames =>
            _propNames ??= new HashSet<string>(
                WeaponData.Pairs.Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);
    }
}
