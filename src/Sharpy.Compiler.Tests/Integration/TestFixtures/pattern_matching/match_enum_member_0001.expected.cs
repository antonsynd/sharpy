#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
#line 7 "match_enum_member_0001.spy"
        switch (c)
        {
            case var __match0 when __match0 == Color.RED:
#line 9 "match_enum_member_0001.spy"
                return "red";
            case var __match0 when __match0 == Color.GREEN:
#line 11 "match_enum_member_0001.spy"
                return "green";
            case var __match0 when __match0 == Color.BLUE:
#line 13 "match_enum_member_0001.spy"
                return "blue";
            case var _:
#line 15 "match_enum_member_0001.spy"
                return "unknown";
        }
    }

    public static void Main()
    {
#line 18 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(0));
#line 19 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(1));
#line 20 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(2));
#line 21 "match_enum_member_0001.spy"
        global::Sharpy.Builtins.Print(Describe(99));
    }
}
