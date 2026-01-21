namespace XBLA_Setup_Editor.Data
{
    internal static class AmmoCountData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("0", 0x00),
            ("1", 0x01),
            ("2", 0x02),
            ("3", 0x03),
            ("4", 0x04),
            ("5", 0x05),
            ("10", 0x0A),
            ("20", 0x14),
            ("30", 0x1E),
            ("50", 0x32),
            ("100", 0x64),
            ("255", 0xFF),
        };

        internal static Dictionary<string, int> Build() => DataHelper.BuildDictionary(Pairs);
    }
}
