namespace XBLA_Setup_Editor.Data
{
    internal static class PropData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("Nothing (No Pickup)",0x60),
            ("Unarmed",0x60),
            ("KF7",0xB8),
            ("Grenade Launcher",0xB9),
            ("Hunting Knives",0xBA),
            ("Moonraker Laser",0xBB),
            ("AR33",0xBC),
            ("D5K",0xBD),
            ("Cougar Magnum",0xBE),
            ("PP7",0xBF),
            ("Shotgun",0xC0),
            ("Klobb",0xC1),
            ("Phantom",0xC2),
            ("ZMG",0xC3),
            ("Grenades",0xC4),
            ("RC-P90",0xC5),
            ("Watch Laser",0xC6),
            ("Tank",0xC6),
            ("Detonator",0xC6),
            ("Tazer",0xC6),
            ("Remote Mines",0xC7),
            ("Proximity Mines",0xC8),
            ("Timed Mines",0xC9),
            ("PP7 (Silenced)",0xCC),
            ("DD44",0xCD),
            ("D5K (Silenced)",0xCE),
            ("Automatic Shotgun",0xCF),
            ("Golden Gun",0xD0),
            ("Throwing Knives",0xD1),
            ("Sniper Rifle",0xD2),
            ("Rocket Launcher",0xD3),
            ("Silver PP7",0xE6),
            ("Gold PP7",0xE7),
        };

        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
