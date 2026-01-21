using System;
using System.Collections.Generic;

namespace XBLA_Setup_Editor.Data
{
    internal static class UnkData
    {
        internal static Dictionary<string, int> Build()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "No", 0x00 }
            };
        }
    }
}
