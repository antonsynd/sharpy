#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace MatchEnumMember0001
{
    public static partial class MatchEnumMember0001Module
    {
        public static string Describe(int c)
        {
#line (7, 5) - (15, 30) 12 "match_enum_member_0001.spy"
            switch (c)
#line hidden
            {
                case var __spy_pm_0 when __spy_pm_0 == global::MatchEnumMember0001.Color.RED:
#line (9, 13) - (9, 26) 20 "match_enum_member_0001.spy"
                    return "red";
#line hidden
                case var __spy_pm_0 when __spy_pm_0 == global::MatchEnumMember0001.Color.GREEN:
#line (11, 13) - (11, 28) 20 "match_enum_member_0001.spy"
                    return "green";
#line hidden
                case var __spy_pm_0 when __spy_pm_0 == global::MatchEnumMember0001.Color.BLUE:
#line (13, 13) - (13, 27) 20 "match_enum_member_0001.spy"
                    return "blue";
#line hidden
                default:
#line (15, 13) - (15, 30) 20 "match_enum_member_0001.spy"
                    return "unknown";
#line hidden
            }
        }

        public static void Main()
        {
#line (18, 5) - (18, 23) 12 "match_enum_member_0001.spy"
            global::Sharpy.Builtins.Print(global::MatchEnumMember0001.MatchEnumMember0001Module.Describe(0));
#line (19, 5) - (19, 23) 12 "match_enum_member_0001.spy"
            global::Sharpy.Builtins.Print(global::MatchEnumMember0001.MatchEnumMember0001Module.Describe(1));
#line (20, 5) - (20, 23) 12 "match_enum_member_0001.spy"
            global::Sharpy.Builtins.Print(global::MatchEnumMember0001.MatchEnumMember0001Module.Describe(2));
#line (21, 5) - (21, 24) 12 "match_enum_member_0001.spy"
            global::Sharpy.Builtins.Print(global::MatchEnumMember0001.MatchEnumMember0001Module.Describe(99));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Color")]
    public class Color
    {
        public const int RED = 0;
        public const int GREEN = 1;
        public const int BLUE = 2;
    }
}
#line default
