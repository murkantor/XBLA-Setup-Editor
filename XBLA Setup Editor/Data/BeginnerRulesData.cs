namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Default weapon configurations for "Beginner Mode" auto-fill.
    /// Maps weapons to their appropriate ammo types and default counts.
    /// </summary>
    internal static class BeginnerRulesData
    {
        internal static readonly Dictionary<string, string> WeaponToAmmoType =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Nothing (No Pickup)", "None" },
                { "Unarmed", "None" },
                { "Hunting Knives", "Knife" },
                { "Throwing Knives", "Knife" },
                { "PP7", "9mm Ammo" },
                { "PP7 (Silenced)", "9mm Ammo" },
                { "DD44", "9mm Ammo" },
                { "Klobb", "9mm Ammo" },
                { "KF7", "Rifle Ammo" },
                { "ZMG", "9mm Ammo" },
                { "D5K", "9mm Ammo" },
                { "D5K (Silenced)", "9mm Ammo" },
                { "Phantom", "Rifle Ammo" },
                { "AR33", "Rifle Ammo" },
                { "RC-P90", "9mm Ammo" },
                { "Shotgun", "Cartridges" },
                { "Automatic Shotgun", "Cartridges" },
                { "Sniper Rifle", "Rifle Ammo" },
                { "Cougar Magnum", "Magnum Bullets" },
                { "Golden Gun", "Golden Bullets" },
                { "Silver PP7", "9mm Ammo" },
                { "Gold PP7", "9mm Ammo" },
                { "Moonraker Laser", "None" },
                { "Watch Laser", "Watch Laser" },
                { "Grenade Launcher", "Grenade Rounds" },
                { "Rocket Launcher", "Rockets" },
                { "Grenades", "Grenades" },
                { "Timed Mine", "Timed Mines" },
                { "Proximity Mine", "Proximity Mines" },
                { "Remote Mine", "Remote Mines" },
                { "Detonator", "None" },
                { "Tazer", "None" },
                { "Tank", "Tank" },
            };

        internal static readonly Dictionary<string, string> WeaponToDefaultAmmoCount =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Nothing (No Pickup)", "0" },
                { "Unarmed", "0" },
                { "Hunting Knives", "1" },
                { "Throwing Knives", "10" },
                { "PP7", "50" },
                { "PP7 (Silenced)", "50" },
                { "DD44", "50" },
                { "Klobb", "100" },
                { "KF7", "100" },
                { "ZMG", "100" },
                { "D5K", "100" },
                { "D5K (Silenced)", "100" },
                { "Phantom", "100" },
                { "AR33", "40" },
                { "RC-P90", "100" },
                { "Shotgun", "30" },
                { "Automatic Shotgun", "30" },
                { "Sniper Rifle", "50" },
                { "Cougar Magnum", "50" },
                { "Golden Gun", "10" },
                { "Silver PP7", "10" },
                { "Gold PP7", "10" },
                { "Moonraker Laser", "0" },
                { "Watch Laser", "100" },
                { "Grenade Launcher", "6" },
                { "Rocket Launcher", "6" },
                { "Grenades", "5" },
                { "Timed Mine", "5" },
                { "Proximity Mine", "5" },
                { "Remote Mine", "5" },
                { "Detonator", "0" },
                { "Tazer", "1" },
                { "Tank", "5" },
            };

        // Derived from WeaponData to avoid duplication - contains all valid weapon names for prop lookups
        private static HashSet<string>? _propNames;
        internal static HashSet<string> PropNames =>
            _propNames ??= new HashSet<string>(
                WeaponData.Pairs.Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);
    }
}
