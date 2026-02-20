// =============================================================================
// LanIdHelper.cs - LAN Compatibility ID for GoldenEye XBLA XEX
// =============================================================================
// GoldenEye XBLA uses a 16-byte value at file offset 0x1D1C as a LAN (system
// link) compatibility identifier. When two consoles connect over system link,
// this value must match — mismatched mods cannot join the same lobby.
//
// This helper computes a deterministic MD5 hash over all MP Weapon Set data,
// the text folder code, and an armor-removal flag, then writes it to 0x1D1C.
// The same mod always produces the same LAN ID; different mods differ.
//
// Hash input (1156 bytes total):
//   960 B  — Weapon sets       0x417728–0x417AE7
//   192 B  — Select list       0x417AE8–0x417BA7
//     3 B  — Text folder code  0xA3AC–0xA3AE
//     1 B  — Armor flag        0x01 = armor removed, 0x00 = armor present
// =============================================================================

using System;
using System.Security.Cryptography;
using XBLA_Setup_Editor.Controls;

namespace XBLA_Setup_Editor.Data
{
    internal static class LanIdHelper
    {
        public const int OFFSET = 0x1D1C;
        public const int SIZE = 16;

        private const int TEXT_FOLDER_LEN = 3;

        // Derived sizes (rely on MPWeaponSetParser constants)
        private const int WEAPONS_SIZE = MPWeaponSetParser.SELECT_LIST_START - MPWeaponSetParser.WEAPONS_START; // 960
        private const int SELECT_SIZE  = MPWeaponSetParser.SELECT_LIST_END - MPWeaponSetParser.SELECT_LIST_START + 1; // 192

        /// <summary>
        /// Computes the 16-byte LAN ID hash from the current XEX state.
        /// </summary>
        /// <param name="xexData">XEX bytes (weapon/select-list data must already be applied).</param>
        /// <param name="armorRemoved">
        /// When provided, the armor flag is set directly from this value (fast path — no scan).
        /// Pass <c>true</c> if armor will be / has been removed, <c>false</c> if it is present.
        /// When <c>null</c> (default), the method scans the XEX to determine the flag — use this
        /// on the save path after armor removal has already been applied.
        /// </param>
        public static byte[] Compute(byte[] xexData, bool? armorRemoved = null)
        {
            // 960 + 192 + 3 + 1 = 1156 bytes
            var input = new byte[WEAPONS_SIZE + SELECT_SIZE + TEXT_FOLDER_LEN + 1];
            int pos = 0;

            // Weapon sets: 0x417728–0x417AE7
            Array.Copy(xexData, MPWeaponSetParser.WEAPONS_START, input, pos, WEAPONS_SIZE);
            pos += WEAPONS_SIZE;

            // Select list: 0x417AE8–0x417BA7
            Array.Copy(xexData, MPWeaponSetParser.SELECT_LIST_START, input, pos, SELECT_SIZE);
            pos += SELECT_SIZE;

            // Text folder code: 3 bytes at TEXT_FOLDER_OFFSET
            Array.Copy(xexData, MPWeaponSetControl.TEXT_FOLDER_OFFSET, input, pos, TEXT_FOLDER_LEN);
            pos += TEXT_FOLDER_LEN;

            // Armor flag: 0x01 = armor removed, 0x00 = armor present.
            // Fast path: caller supplies the value directly (live preview, no scan needed).
            // Slow path: scan the XEX — use after armor has already been removed on save.
            bool removed = armorRemoved ?? (XEXArmorRemover.ScanForArmor(xexData).ArmorBlocks.Count == 0);
            input[pos] = removed ? (byte)0x01 : (byte)0x00;

            return MD5.HashData(input);
        }

        /// <summary>Writes a 16-byte hash in-place to offset 0x1D1C.</summary>
        public static void Write(byte[] xexData, byte[] hash)
        {
            if (hash.Length != SIZE)
                throw new ArgumentException($"Hash must be {SIZE} bytes.", nameof(hash));
            Array.Copy(hash, 0, xexData, OFFSET, SIZE);
        }

        /// <summary>Reads the current 16-byte LAN ID from offset 0x1D1C.</summary>
        public static byte[] Read(byte[] xexData)
        {
            var result = new byte[SIZE];
            Array.Copy(xexData, OFFSET, result, 0, SIZE);
            return result;
        }

        /// <summary>Formats a hash as an uppercase space-separated hex string (e.g. "AA BB CC …").</summary>
        public static string ToHex(byte[] h) => BitConverter.ToString(h).Replace("-", " ");
    }
}
