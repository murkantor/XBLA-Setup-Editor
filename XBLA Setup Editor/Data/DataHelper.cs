// =============================================================================
// DataHelper.cs - Shared Data Utilities
// =============================================================================
// Provides utility methods used across the Data layer for handling lookup
// tables and dictionaries.
//
// USAGE:
// ======
// The primary use is converting (Name, Code) pair arrays into dictionaries
// for efficient name-to-code lookups in the weapon/ammo/prop editor controls.
// =============================================================================

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// Static helper class providing shared data utilities.
    /// </summary>
    internal static class DataHelper
    {
        /// <summary>
        /// Builds a case-insensitive dictionary from (Name, Code) pairs.
        /// </summary>
        /// <param name="pairs">Enumerable of (Name, Code) tuples.</param>
        /// <returns>Dictionary mapping names (case-insensitive) to codes.</returns>
        /// <remarks>
        /// <para>
        /// Filters out empty names and handles duplicate names by taking
        /// the first occurrence only (using GroupBy).
        /// </para>
        /// <para>
        /// Used by WeaponData, AmmoTypeData, and PropData to create
        /// lookup dictionaries for the grid editors.
        /// </para>
        /// </remarks>
        internal static Dictionary<string, int> BuildDictionary(
            IEnumerable<(string Name, int Code)> pairs) =>
            pairs.Where(p => !string.IsNullOrWhiteSpace(p.Name))
                 .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                 .ToDictionary(g => g.Key, g => g.First().Code, StringComparer.OrdinalIgnoreCase);
    }
}
