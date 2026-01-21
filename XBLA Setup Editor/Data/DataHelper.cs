namespace XBLA_Setup_Editor.Data
{
    internal static class DataHelper
    {
        internal static Dictionary<string, int> BuildDictionary(
            IEnumerable<(string Name, int Code)> pairs) =>
            pairs.Where(p => !string.IsNullOrWhiteSpace(p.Name))
                 .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                 .ToDictionary(g => g.Key, g => g.First().Code, StringComparer.OrdinalIgnoreCase);
    }
}
