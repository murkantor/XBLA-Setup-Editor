namespace XBLA_Setup_Editor.Data
{
    internal static class ToggleData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("No", 0x00),
            ("Yes", 0x01),
        };

        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
