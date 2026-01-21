using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    internal static class ScaleData
    {
        internal static readonly (string Name, int Code)[] Pairs =
        {
            ("None", 0x00),
            ("Normal", 0x01),
            ("Large", 0x02),
            ("Huge", 0x03),
            ("Why", 0x04),
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
