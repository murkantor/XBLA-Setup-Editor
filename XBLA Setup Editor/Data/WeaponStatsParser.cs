using System;
using System.Collections.Generic;
using System.IO;
using XBLA_Setup_Editor.Data;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Parses and patches Weapon Statistics, Weapon Models, and Ammo Reserve data in GoldenEye XBLA XEX files.
    /// </summary>
    public sealed class WeaponStatsParser
    {
        // XEX offsets for Weapon Statistics
        public const int WEAPON_STATS_START_XEX = 0x4134D8;
        public const int WEAPON_STATS_END_XEX = 0x414967;
        public const int WEAPON_STATS_ENTRY_SIZE = 0x70;  // 112 bytes
        public const int WEAPON_STATS_COUNT = 47;

        // XEX offsets for Weapon Model Data
        public const int WEAPON_MODEL_START_XEX = 0x414968;
        public const int WEAPON_MODEL_END_XEX = 0x415CDF;
        public const int WEAPON_MODEL_ENTRY_SIZE = 0x38;  // 56 bytes
        public const int WEAPON_MODEL_COUNT = 89;

        // XEX offsets for Ammo Reserve Capacity
        public const int AMMO_RESERVE_START_XEX = 0x416C84;
        public const int AMMO_RESERVE_END_XEX = 0x416DEB;
        public const int AMMO_RESERVE_ENTRY_SIZE = 0x0C;  // 12 bytes
        public const int AMMO_RESERVE_COUNT = 30;

        // N64 21990 file offsets
        public const int WEAPON_STATS_START_21990 = 0x11704;
        public const int WEAPON_STATS_END_21990 = 0x12B94;
        public const int WEAPON_MODEL_START_21990 = 0x12B94;
        public const int WEAPON_MODEL_END_21990 = 0x13F0C;
        public const int AMMO_RESERVE_START_21990 = 0x1515C;
        public const int AMMO_RESERVE_END_21990 = 0x152C4;

        /// <summary>
        /// Represents a weapon statistics entry (0x70 bytes).
        /// XBLA layout at 0x4134D8 - 0x414967
        /// </summary>
        public sealed class WeaponStatsEntry
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }

            // 0x00-0x1D: Position and visual fields
            public float MuzzleFlashExtension { get; set; }      // 0x00
            public float OnScreenXPosition { get; set; }         // 0x04
            public float OnScreenYPosition { get; set; }         // 0x08
            public float OnScreenZPosition { get; set; }         // 0x0C
            public float AimUpwardShift { get; set; }            // 0x10
            public float AimDownwardShift { get; set; }          // 0x14
            public float AimLeftRightShift { get; set; }         // 0x18
            public ushort Padding1C { get; set; }                // 0x1C (padding before ammo type)

            // 0x1E-0x2B: Ammo and firing
            public ushort AmmunitionType { get; set; }           // 0x1E
            public ushort MagazineSize { get; set; }             // 0x20
            public byte FireAutomatic { get; set; }              // 0x22
            public byte FireSingleShot { get; set; }             // 0x23
            public byte Penetration { get; set; }                // 0x24
            public byte SoundTriggerRate { get; set; }           // 0x25
            public ushort SoundEffect { get; set; }              // 0x26
            public uint EjectedCasingsRAM { get; set; }          // 0x28

            // 0x2C-0x6F: Combat stats
            public float Damage { get; set; }                    // 0x2C
            public float Inaccuracy { get; set; }                // 0x30
            public float Scope { get; set; }                     // 0x34
            public float CrosshairSpeed { get; set; }            // 0x38
            public float WeaponAimLockOnSpeed { get; set; }      // 0x3C
            public float Sway { get; set; }                      // 0x40

            // 0x44-0x47: RecoilSpeed - NOT A FLOAT! Contains timing/frame delay bytes
            public byte RecoilSpeedByte0 { get; set; }           // 0x44 - Fire rate timing byte 0
            public byte RecoilSpeedByte1 { get; set; }           // 0x45 - Fire rate timing byte 1
            public byte RecoilSpeedByte2 { get; set; }           // 0x46 - Fire rate timing byte 2
            public byte RecoilSpeedByte3 { get; set; }           // 0x47 - Fire rate timing byte 3

            /// <summary>
            /// Backward compatibility property - RecoilSpeed exposed as float for UI compatibility.
            /// WARNING: This field contains 4 bytes of timing data, NOT a proper IEEE float!
            /// Setting this will reinterpret the float bits as raw bytes to preserve timing data.
            /// DO NOT perform mathematical operations on this value - it will produce incorrect results.
            /// </summary>
            public float RecoilSpeed
            {
                get
                {
                    // Pack the 4 bytes and reinterpret as float (for UI compatibility)
                    byte[] bytes = new byte[] { RecoilSpeedByte0, RecoilSpeedByte1, RecoilSpeedByte2, RecoilSpeedByte3 };
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                    return BitConverter.ToSingle(bytes, 0);
                }
                set
                {
                    // Reinterpret float bits as raw bytes (preserves timing data)
                    byte[] bytes = BitConverter.GetBytes(value);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                    RecoilSpeedByte0 = bytes[0];
                    RecoilSpeedByte1 = bytes[1];
                    RecoilSpeedByte2 = bytes[2];
                    RecoilSpeedByte3 = bytes[3];
                }
            }

            public float RecoilBackward { get; set; }            // 0x48
            public float RecoilUpward { get; set; }              // 0x4C
            public float RecoilBolt { get; set; }                // 0x50
            public float VolumeToAISingleShot { get; set; }      // 0x54
            public float VolumeToAIMultipleShots { get; set; }   // 0x58
            public float VolumeToAIActiveFire { get; set; }      // 0x5C
            public float VolumeToAIBaseline1 { get; set; }       // 0x60
            public float VolumeToAIBaseline2 { get; set; }       // 0x64
            public float ForceOfImpact { get; set; }             // 0x68
            public uint Flags { get; set; }                      // 0x6C

            public string WeaponName => GetWeaponName(Index);

            public byte[] ToBytes()
            {
                var data = new byte[WEAPON_STATS_ENTRY_SIZE];
                WriteFloatBE(data, 0x00, MuzzleFlashExtension);
                WriteFloatBE(data, 0x04, OnScreenXPosition);
                WriteFloatBE(data, 0x08, OnScreenYPosition);
                WriteFloatBE(data, 0x0C, OnScreenZPosition);
                WriteFloatBE(data, 0x10, AimUpwardShift);
                WriteFloatBE(data, 0x14, AimDownwardShift);
                WriteFloatBE(data, 0x18, AimLeftRightShift);
                WriteU16BE(data, 0x1C, Padding1C);
                WriteU16BE(data, 0x1E, AmmunitionType);
                WriteU16BE(data, 0x20, MagazineSize);
                data[0x22] = FireAutomatic;
                data[0x23] = FireSingleShot;
                data[0x24] = Penetration;
                data[0x25] = SoundTriggerRate;
                WriteU16BE(data, 0x26, SoundEffect);
                WriteU32BE(data, 0x28, EjectedCasingsRAM);
                WriteFloatBE(data, 0x2C, Damage);
                WriteFloatBE(data, 0x30, Inaccuracy);
                WriteFloatBE(data, 0x34, Scope);
                WriteFloatBE(data, 0x38, CrosshairSpeed);
                WriteFloatBE(data, 0x3C, WeaponAimLockOnSpeed);
                WriteFloatBE(data, 0x40, Sway);

                // Write RecoilSpeed as raw bytes (NOT as float)
                data[0x44] = RecoilSpeedByte0;
                data[0x45] = RecoilSpeedByte1;
                data[0x46] = RecoilSpeedByte2;
                data[0x47] = RecoilSpeedByte3;

                WriteFloatBE(data, 0x48, RecoilBackward);
                WriteFloatBE(data, 0x4C, RecoilUpward);
                WriteFloatBE(data, 0x50, RecoilBolt);
                WriteFloatBE(data, 0x54, VolumeToAISingleShot);
                WriteFloatBE(data, 0x58, VolumeToAIMultipleShots);
                WriteFloatBE(data, 0x5C, VolumeToAIActiveFire);
                WriteFloatBE(data, 0x60, VolumeToAIBaseline1);
                WriteFloatBE(data, 0x64, VolumeToAIBaseline2);
                WriteFloatBE(data, 0x68, ForceOfImpact);
                WriteU32BE(data, 0x6C, Flags);
                return data;
            }

            public static WeaponStatsEntry FromBytes(byte[] data, int offset, int index)
            {
                return new WeaponStatsEntry
                {
                    Index = index,
                    FileOffset = offset,
                    MuzzleFlashExtension = ReadFloatBE(data, offset + 0x00),
                    OnScreenXPosition = ReadFloatBE(data, offset + 0x04),
                    OnScreenYPosition = ReadFloatBE(data, offset + 0x08),
                    OnScreenZPosition = ReadFloatBE(data, offset + 0x0C),
                    AimUpwardShift = ReadFloatBE(data, offset + 0x10),
                    AimDownwardShift = ReadFloatBE(data, offset + 0x14),
                    AimLeftRightShift = ReadFloatBE(data, offset + 0x18),
                    Padding1C = ReadU16BE(data, offset + 0x1C),
                    AmmunitionType = ReadU16BE(data, offset + 0x1E),
                    MagazineSize = ReadU16BE(data, offset + 0x20),
                    FireAutomatic = data[offset + 0x22],
                    FireSingleShot = data[offset + 0x23],
                    Penetration = data[offset + 0x24],
                    SoundTriggerRate = data[offset + 0x25],
                    SoundEffect = ReadU16BE(data, offset + 0x26),
                    EjectedCasingsRAM = ReadU32BE(data, offset + 0x28),
                    Damage = ReadFloatBE(data, offset + 0x2C),
                    Inaccuracy = ReadFloatBE(data, offset + 0x30),
                    Scope = ReadFloatBE(data, offset + 0x34),
                    CrosshairSpeed = ReadFloatBE(data, offset + 0x38),
                    WeaponAimLockOnSpeed = ReadFloatBE(data, offset + 0x3C),
                    Sway = ReadFloatBE(data, offset + 0x40),

                    // Read RecoilSpeed as raw bytes (NOT as float)
                    RecoilSpeedByte0 = data[offset + 0x44],
                    RecoilSpeedByte1 = data[offset + 0x45],
                    RecoilSpeedByte2 = data[offset + 0x46],
                    RecoilSpeedByte3 = data[offset + 0x47],

                    RecoilBackward = ReadFloatBE(data, offset + 0x48),
                    RecoilUpward = ReadFloatBE(data, offset + 0x4C),
                    RecoilBolt = ReadFloatBE(data, offset + 0x50),
                    VolumeToAISingleShot = ReadFloatBE(data, offset + 0x54),
                    VolumeToAIMultipleShots = ReadFloatBE(data, offset + 0x58),
                    VolumeToAIActiveFire = ReadFloatBE(data, offset + 0x5C),
                    VolumeToAIBaseline1 = ReadFloatBE(data, offset + 0x60),
                    VolumeToAIBaseline2 = ReadFloatBE(data, offset + 0x64),
                    ForceOfImpact = ReadFloatBE(data, offset + 0x68),
                    Flags = ReadU32BE(data, offset + 0x6C)
                };
            }

            /// <summary>
            /// Creates an XBLA WeaponStatsEntry from N64 21990 data with field remapping.
            /// </summary>
            public static WeaponStatsEntry FromN64Bytes(byte[] data, int offset, int index)
            {
                // N64 structure is different from XBLA - fields are rearranged
                // N64 layout:
                // 0x00: destruction_amount (float) -> Damage
                // 0x04: ingame_pos_x (float) -> OnScreenXPosition
                // 0x08: ingame_pos_y (float) -> OnScreenYPosition
                // 0x0C: ingame_pos_z (float) -> OnScreenZPosition
                // 0x10: recoil_back (float) -> RecoilBackward
                // 0x14: aim_shifts_down (float) -> AimDownwardShift
                // 0x18: aim_shifts_lr (float) -> AimLeftRightShift
                // 0x1C: sound_trigger_rate (byte) -> SoundTriggerRate
                // 0x1D-0x1E: padding
                // 0x1F: object_penetration (byte) -> Penetration
                // 0x20: unused (byte)
                // 0x21: magazine_size (byte) -> MagazineSize
                // 0x22: fire_rate_auto (byte) -> FireAutomatic
                // 0x23: fire_rate_single (byte) -> FireSingleShot
                // 0x24: ammo_type (byte) -> AmmunitionType
                // 0x25: padding
                // 0x26: sound_id (ushort) -> SoundEffect
                // 0x28: ejected_casings_ptr (uint) -> EjectedCasingsRAM
                // 0x2C: inaccuracy (float) -> Inaccuracy
                // 0x30: volume_to_ai_min (float) -> VolumeToAISingleShot
                // 0x34: zoom (float) -> Scope
                // 0x38: crosshair_speed (float) -> CrosshairSpeed
                // 0x3C: weapon_aim_speed (float) -> WeaponAimLockOnSpeed
                // 0x40: muzzle_flash_ext (float) -> MuzzleFlashExtension
                // 0x44-0x47: recoil_speed_bytes[4] -> RecoilSpeedBytes (as raw bytes, NOT float)
                // 0x48: aim_shifts_up (float) -> AimUpwardShift
                // 0x4C: recoil_up (float) -> RecoilUpward
                // 0x50: recoil_speed_float (float) -> RecoilSpeed (DEPRECATED - using bytes at 0x44 instead)
                // 0x54: sway (float) -> Sway
                // 0x58: volume_to_ai_max (float) -> VolumeToAIMultipleShots
                // 0x5C: unknown_5c (float) -> VolumeToAIActiveFire
                // 0x60: rapid_fire_alertness (float) -> VolumeToAIBaseline1
                // 0x64: unknown_64 (float) -> VolumeToAIBaseline2
                // 0x68: force_of_impact (float) -> ForceOfImpact
                // 0x6C: item_bitflags (uint) -> Flags

                return new WeaponStatsEntry
                {
                    Index = index,
                    FileOffset = offset,
                    // Map N64 fields to XBLA positions
                    Damage = ReadFloatBE(data, offset + 0x00),              // N64 destruction_amount
                    OnScreenXPosition = ReadFloatBE(data, offset + 0x04),   // N64 ingame_pos_x
                    OnScreenYPosition = ReadFloatBE(data, offset + 0x08),   // N64 ingame_pos_y
                    OnScreenZPosition = ReadFloatBE(data, offset + 0x0C),   // N64 ingame_pos_z
                    RecoilBackward = ReadFloatBE(data, offset + 0x10),      // N64 recoil_back
                    AimDownwardShift = ReadFloatBE(data, offset + 0x14),    // N64 aim_shifts_down
                    AimLeftRightShift = ReadFloatBE(data, offset + 0x18),   // N64 aim_shifts_lr
                    SoundTriggerRate = data[offset + 0x1C],                 // N64 sound_trigger_rate
                    Penetration = data[offset + 0x1F],                      // N64 object_penetration
                    MagazineSize = data[offset + 0x21],                     // N64 magazine_size (byte->ushort)
                    FireAutomatic = data[offset + 0x22],                    // N64 fire_rate_auto
                    FireSingleShot = data[offset + 0x23],                   // N64 fire_rate_single
                    AmmunitionType = data[offset + 0x24],                   // N64 ammo_type (byte->ushort)
                    SoundEffect = ReadU16BE(data, offset + 0x26),           // N64 sound_id
                    EjectedCasingsRAM = ReadU32BE(data, offset + 0x28),     // N64 ejected_casings_ptr
                    Inaccuracy = ReadFloatBE(data, offset + 0x2C),          // N64 inaccuracy
                    VolumeToAISingleShot = ReadFloatBE(data, offset + 0x30),// N64 volume_to_ai_min
                    Scope = ReadFloatBE(data, offset + 0x34),               // N64 zoom
                    CrosshairSpeed = ReadFloatBE(data, offset + 0x38),      // N64 crosshair_speed
                    WeaponAimLockOnSpeed = ReadFloatBE(data, offset + 0x3C),// N64 weapon_aim_speed
                    MuzzleFlashExtension = ReadFloatBE(data, offset + 0x40),// N64 muzzle_flash_ext

                    // Read RecoilSpeed as raw bytes from N64 (NOT as float)
                    RecoilSpeedByte0 = data[offset + 0x44],
                    RecoilSpeedByte1 = data[offset + 0x45],
                    RecoilSpeedByte2 = data[offset + 0x46],
                    RecoilSpeedByte3 = data[offset + 0x47],

                    AimUpwardShift = ReadFloatBE(data, offset + 0x48),      // N64 aim_shifts_up
                    RecoilUpward = ReadFloatBE(data, offset + 0x4C),        // N64 recoil_up
                    // Skip 0x50 (N64 recoil_speed_float) - we use the bytes at 0x44 instead
                    RecoilBolt = ReadFloatBE(data, offset + 0x50),          // N64 recoil_speed_float (or could be RecoilBolt)
                    Sway = ReadFloatBE(data, offset + 0x54),                // N64 sway
                    VolumeToAIMultipleShots = ReadFloatBE(data, offset + 0x58), // N64 volume_to_ai_max
                    VolumeToAIActiveFire = ReadFloatBE(data, offset + 0x5C),// N64 unknown_5c
                    VolumeToAIBaseline1 = ReadFloatBE(data, offset + 0x60), // N64 rapid_fire_alertness
                    VolumeToAIBaseline2 = ReadFloatBE(data, offset + 0x64), // N64 unknown_64
                    ForceOfImpact = ReadFloatBE(data, offset + 0x68),       // N64 force_of_impact
                    Flags = ReadU32BE(data, offset + 0x6C),                 // N64 item_bitflags
                    Padding1C = 0                                           // Not in N64
                };
            }
        }

        /// <summary>
        /// Represents a weapon model entry (0x38 bytes).
        /// XBLA layout at 0x414968 - 0x415CDF
        /// </summary>
        public sealed class WeaponModelEntry
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }

            public uint ModelDetailsRAM { get; set; }            // 0x00 - Model Details RAM Address
            public uint GZTextStringRAM { get; set; }            // 0x04 - G_Z Text String RAM Address
            public uint HasGZModel { get; set; }                 // 0x08 - 0=Yes, 1=No
            public uint StatisticsRAM { get; set; }              // 0x0C - Statistics RAM Address
            public ushort NameUpperWatch { get; set; }           // 0x10 - Name, Upper Watch Equipped
            public ushort NameLowerWatch { get; set; }           // 0x12 - Name, Lower Watch Equipped
            public float WatchEquippedX { get; set; }            // 0x14 - X Position, Watch Equipped
            public float WatchEquippedY { get; set; }            // 0x18 - Y Position, Watch Equipped
            public float WatchEquippedZ { get; set; }            // 0x1C - Z Position, Watch Equipped
            public float XRotation { get; set; }                 // 0x20 - X Rotation
            public float YRotation { get; set; }                 // 0x24 - Y Rotation
            public ushort NameWeaponOfChoice { get; set; }       // 0x28 - Name, Weapon of Choice
            public ushort NameInventoryList { get; set; }        // 0x2A - Name, Inventory List
            public float InventoryListX { get; set; }            // 0x2C - X Position, Inventory List
            public float InventoryListY { get; set; }            // 0x30 - Y Position, Inventory List
            public float InventoryListZ { get; set; }            // 0x34 - Z Position, Inventory List

            public byte[] ToBytes()
            {
                var data = new byte[WEAPON_MODEL_ENTRY_SIZE];
                WriteU32BE(data, 0x00, ModelDetailsRAM);
                WriteU32BE(data, 0x04, GZTextStringRAM);
                WriteU32BE(data, 0x08, HasGZModel);
                WriteU32BE(data, 0x0C, StatisticsRAM);
                WriteU16BE(data, 0x10, NameUpperWatch);
                WriteU16BE(data, 0x12, NameLowerWatch);
                WriteFloatBE(data, 0x14, WatchEquippedX);
                WriteFloatBE(data, 0x18, WatchEquippedY);
                WriteFloatBE(data, 0x1C, WatchEquippedZ);
                WriteFloatBE(data, 0x20, XRotation);
                WriteFloatBE(data, 0x24, YRotation);
                WriteU16BE(data, 0x28, NameWeaponOfChoice);
                WriteU16BE(data, 0x2A, NameInventoryList);
                WriteFloatBE(data, 0x2C, InventoryListX);
                WriteFloatBE(data, 0x30, InventoryListY);
                WriteFloatBE(data, 0x34, InventoryListZ);
                return data;
            }

            public static WeaponModelEntry FromBytes(byte[] data, int offset, int index)
            {
                return new WeaponModelEntry
                {
                    Index = index,
                    FileOffset = offset,
                    ModelDetailsRAM = ReadU32BE(data, offset + 0x00),
                    GZTextStringRAM = ReadU32BE(data, offset + 0x04),
                    HasGZModel = ReadU32BE(data, offset + 0x08),
                    StatisticsRAM = ReadU32BE(data, offset + 0x0C),
                    NameUpperWatch = ReadU16BE(data, offset + 0x10),
                    NameLowerWatch = ReadU16BE(data, offset + 0x12),
                    WatchEquippedX = ReadFloatBE(data, offset + 0x14),
                    WatchEquippedY = ReadFloatBE(data, offset + 0x18),
                    WatchEquippedZ = ReadFloatBE(data, offset + 0x1C),
                    XRotation = ReadFloatBE(data, offset + 0x20),
                    YRotation = ReadFloatBE(data, offset + 0x24),
                    NameWeaponOfChoice = ReadU16BE(data, offset + 0x28),
                    NameInventoryList = ReadU16BE(data, offset + 0x2A),
                    InventoryListX = ReadFloatBE(data, offset + 0x2C),
                    InventoryListY = ReadFloatBE(data, offset + 0x30),
                    InventoryListZ = ReadFloatBE(data, offset + 0x34)
                };
            }
        }

        /// <summary>
        /// Represents an ammo reserve entry (0x0C bytes).
        /// XBLA layout at 0x416C84 - 0x416DEB
        /// </summary>
        public sealed class AmmoReserveEntry
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }

            public float IconOffset { get; set; }                // 0x00 - Icon Offset
            public uint MaxReserveCapacity { get; set; }         // 0x04 - Max Reserve Capacity
            public uint Pointer { get; set; }                    // 0x08 - Pointer (N64 used screen bank offset)

            public byte[] ToBytes()
            {
                var data = new byte[AMMO_RESERVE_ENTRY_SIZE];
                WriteFloatBE(data, 0x00, IconOffset);
                WriteU32BE(data, 0x04, MaxReserveCapacity);
                WriteU32BE(data, 0x08, Pointer);
                return data;
            }

            public static AmmoReserveEntry FromBytes(byte[] data, int offset, int index)
            {
                return new AmmoReserveEntry
                {
                    Index = index,
                    FileOffset = offset,
                    IconOffset = ReadFloatBE(data, offset + 0x00),
                    MaxReserveCapacity = ReadU32BE(data, offset + 0x04),
                    Pointer = ReadU32BE(data, offset + 0x08)
                };
            }
        }

        // Parsed data collections
        public List<WeaponStatsEntry> WeaponStats { get; } = new();
        public List<WeaponModelEntry> WeaponModels { get; } = new();
        public List<AmmoReserveEntry> AmmoReserves { get; } = new();

        /// <summary>
        /// Loads all weapon data from XEX file data.
        /// </summary>
        public static WeaponStatsParser LoadFromXex(byte[] xexData)
        {
            var parser = new WeaponStatsParser();
            parser.ParseXex(xexData);
            return parser;
        }

        /// <summary>
        /// Loads all weapon data from an XEX file path.
        /// </summary>
        public static WeaponStatsParser LoadFromFile(string path)
        {
            var xexData = File.ReadAllBytes(path);
            return LoadFromXex(xexData);
        }

        /// <summary>
        /// Loads weapon stats from N64 21990 file data and converts to XBLA format.
        /// </summary>
        public static WeaponStatsParser LoadFrom21990(byte[] data21990)
        {
            var parser = new WeaponStatsParser();
            parser.Parse21990(data21990);
            return parser;
        }

        /// <summary>
        /// Loads weapon stats from N64 21990 file path and converts to XBLA format.
        /// </summary>
        public static WeaponStatsParser LoadFrom21990File(string path)
        {
            var data = File.ReadAllBytes(path);
            return LoadFrom21990(data);
        }

        /// <summary>
        /// Loads weapon stats from 21990 file assuming XBLA field layout (for debugging).
        /// </summary>
        public static WeaponStatsParser LoadFrom21990WithXblaLayout(byte[] data21990)
        {
            var parser = new WeaponStatsParser();
            parser.Parse21990WithXblaLayout(data21990);
            return parser;
        }

        private void ParseXex(byte[] xexData)
        {
            WeaponStats.Clear();
            WeaponModels.Clear();
            AmmoReserves.Clear();

            if (xexData.Length < AMMO_RESERVE_END_XEX)
                throw new InvalidOperationException($"XEX file too small. Expected at least {AMMO_RESERVE_END_XEX} bytes.");

            // Parse weapon statistics
            for (int i = 0; i < WEAPON_STATS_COUNT; i++)
            {
                int offset = WEAPON_STATS_START_XEX + (i * WEAPON_STATS_ENTRY_SIZE);
                WeaponStats.Add(WeaponStatsEntry.FromBytes(xexData, offset, i));
            }

            // Parse weapon models
            for (int i = 0; i < WEAPON_MODEL_COUNT; i++)
            {
                int offset = WEAPON_MODEL_START_XEX + (i * WEAPON_MODEL_ENTRY_SIZE);
                WeaponModels.Add(WeaponModelEntry.FromBytes(xexData, offset, i));
            }

            // Parse ammo reserves
            for (int i = 0; i < AMMO_RESERVE_COUNT; i++)
            {
                int offset = AMMO_RESERVE_START_XEX + (i * AMMO_RESERVE_ENTRY_SIZE);
                AmmoReserves.Add(AmmoReserveEntry.FromBytes(xexData, offset, i));
            }
        }

        private void Parse21990(byte[] data21990)
        {
            WeaponStats.Clear();
            WeaponModels.Clear();
            AmmoReserves.Clear();

            if (data21990.Length < AMMO_RESERVE_END_21990)
                throw new InvalidOperationException($"21990 file too small. Expected at least {AMMO_RESERVE_END_21990} bytes.");

            // Parse weapon statistics from N64 21990 format and convert to XBLA format
            for (int i = 0; i < WEAPON_STATS_COUNT; i++)
            {
                int offset = WEAPON_STATS_START_21990 + (i * WEAPON_STATS_ENTRY_SIZE);
                WeaponStats.Add(WeaponStatsEntry.FromN64Bytes(data21990, offset, i));
            }

            // Parse weapon models (assuming same structure for now)
            for (int i = 0; i < WEAPON_MODEL_COUNT; i++)
            {
                int offset = WEAPON_MODEL_START_21990 + (i * WEAPON_MODEL_ENTRY_SIZE);
                WeaponModels.Add(WeaponModelEntry.FromBytes(data21990, offset, i));
            }

            // Parse ammo reserves (assuming same structure for now)
            for (int i = 0; i < AMMO_RESERVE_COUNT; i++)
            {
                int offset = AMMO_RESERVE_START_21990 + (i * AMMO_RESERVE_ENTRY_SIZE);
                AmmoReserves.Add(AmmoReserveEntry.FromBytes(data21990, offset, i));
            }
        }

        private void Parse21990WithXblaLayout(byte[] data21990)
        {
            WeaponStats.Clear();
            WeaponModels.Clear();
            AmmoReserves.Clear();

            if (data21990.Length < AMMO_RESERVE_END_21990)
                throw new InvalidOperationException($"21990 file too small. Expected at least {AMMO_RESERVE_END_21990} bytes.");

            // Parse weapon statistics using XBLA field layout (same as FromBytes)
            for (int i = 0; i < WEAPON_STATS_COUNT; i++)
            {
                int offset = WEAPON_STATS_START_21990 + (i * WEAPON_STATS_ENTRY_SIZE);
                WeaponStats.Add(WeaponStatsEntry.FromBytes(data21990, offset, i));
            }

            // Parse weapon models
            for (int i = 0; i < WEAPON_MODEL_COUNT; i++)
            {
                int offset = WEAPON_MODEL_START_21990 + (i * WEAPON_MODEL_ENTRY_SIZE);
                WeaponModels.Add(WeaponModelEntry.FromBytes(data21990, offset, i));
            }

            // Parse ammo reserves
            for (int i = 0; i < AMMO_RESERVE_COUNT; i++)
            {
                int offset = AMMO_RESERVE_START_21990 + (i * AMMO_RESERVE_ENTRY_SIZE);
                AmmoReserves.Add(AmmoReserveEntry.FromBytes(data21990, offset, i));
            }
        }

        /// <summary>
        /// Applies all current data to XEX data (parsed field-by-field).
        /// </summary>
        public void ApplyToXex(byte[] xexData, List<string>? log = null)
        {
            log?.Add("=== Applying Weapon Stats Data ===");

            if (xexData.Length < AMMO_RESERVE_END_XEX)
                throw new InvalidOperationException($"XEX file too small. Expected at least {AMMO_RESERVE_END_XEX} bytes.");

            // Write weapon statistics
            for (int i = 0; i < WeaponStats.Count && i < WEAPON_STATS_COUNT; i++)
            {
                int offset = WEAPON_STATS_START_XEX + (i * WEAPON_STATS_ENTRY_SIZE);
                var bytes = WeaponStats[i].ToBytes();
                Array.Copy(bytes, 0, xexData, offset, WEAPON_STATS_ENTRY_SIZE);
            }
            log?.Add($"  Written {WeaponStats.Count} weapon stats entries");

            // Write weapon models
            for (int i = 0; i < WeaponModels.Count && i < WEAPON_MODEL_COUNT; i++)
            {
                int offset = WEAPON_MODEL_START_XEX + (i * WEAPON_MODEL_ENTRY_SIZE);
                var bytes = WeaponModels[i].ToBytes();
                Array.Copy(bytes, 0, xexData, offset, WEAPON_MODEL_ENTRY_SIZE);
            }
            log?.Add($"  Written {WeaponModels.Count} weapon model entries");

            // Write ammo reserves
            for (int i = 0; i < AmmoReserves.Count && i < AMMO_RESERVE_COUNT; i++)
            {
                int offset = AMMO_RESERVE_START_XEX + (i * AMMO_RESERVE_ENTRY_SIZE);
                var bytes = AmmoReserves[i].ToBytes();
                Array.Copy(bytes, 0, xexData, offset, AMMO_RESERVE_ENTRY_SIZE);
            }
            log?.Add($"  Written {AmmoReserves.Count} ammo reserve entries");

            log?.Add("Applied all weapon data.");
        }

        /// <summary>
        /// Gets a weapon name by index, falling back to "Unknown XX" for unmapped indices.
        /// </summary>
        public static string GetWeaponName(int index)
        {
            foreach (var (name, code) in WeaponData.Pairs)
            {
                if (code == index)
                    return name;
            }
            return $"Unknown {index:X2}";
        }

        // --- Big-endian helper methods ---
        private static ushort ReadU16BE(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
        private static uint ReadU32BE(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        private static void WriteU16BE(byte[] b, int o, ushort v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
        private static void WriteU32BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        private static float ReadFloatBE(byte[] b, int o)
        {
            // Copy big-endian bytes, reverse to little-endian if needed
            byte[] floatBytes = new byte[] { b[o], b[o + 1], b[o + 2], b[o + 3] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(floatBytes);
            return BitConverter.ToSingle(floatBytes, 0);
        }

        private static void WriteFloatBE(byte[] b, int o, float v)
        {
            byte[] floatBytes = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(floatBytes);
            Array.Copy(floatBytes, 0, b, o, 4);
        }
    }
}
