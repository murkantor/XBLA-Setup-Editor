using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    internal static class ToggleData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("No", 0x00),
            ("Yes", 0x01),
        };

        internal static Dictionary<string, int> Build()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Pairs)
                if (!d.ContainsKey(kv.Name)) d[kv.Name] = kv.Code;
            return d;
        }
    }
}
