#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchEnumMember0001
{
    public class Color
    {
        public const int RED = 0;
        public const int GREEN = 1;
        public const int BLUE = 2;
    }

    public static string Describe(int c)
    {
#line (7, 5) - (17, 1) 1 "match_enum_member_0001.spy"
        switch (c)
        {
            case var __spy_pm_0 when __spy_pm_0 == Color.RED:
#line (9, 13) - (9, 26) 1 "match_enum_member_0001.spy"
                return "red";
            case var __spy_pm_0 when __spy_pm_0 == Color.GREEN:
#line (11, 13) - (11, 28) 1 "match_enum_member_0001.spy"
                return "green";
            case var __spy_pm_0 when __spy_pm_0 == Color.BLUE:
#line (13, 13) - (13, 27) 1 "match_enum_member_0001.spy"
                return "blue";
            default:
#line (15, 13) - (15, 30) 1 "match_enum_member_0001.spy"
                return "unknown";
        }
    }

    public static void Main()
    {
#line (18, 5) - (18, 23) 1 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(0));
#line (19, 5) - (19, 23) 1 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(1));
#line (20, 5) - (20, 23) 1 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(2));
#line (21, 5) - (21, 24) 1 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(99));
    }
}
