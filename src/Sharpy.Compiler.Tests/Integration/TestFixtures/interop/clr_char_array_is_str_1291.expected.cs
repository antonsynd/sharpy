// Snapshot: pins that the char-to-str conversion (#1291) appears ONLY at the two producers — the `to_char_array()` calls — and nowhere else. Every downstream position emits ordinary string handling.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClrCharArrayIsStr1291
{
    public static void Take(string s)
    {
#line (15, 5) - (15, 13) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(s);
#line hidden
    }

    public static string FirstOf(string[] cs)
    {
#line (19, 5) - (19, 18) 8 "clr_char_array_is_str_1291.spy"
        return global::Sharpy.ArrayHelpers.GetItem(cs, 0);
#line hidden
    }

    public static string Echo(string c)
    {
#line (24, 5) - (24, 14) 8 "clr_char_array_is_str_1291.spy"
        return c;
#line hidden
    }

    public static void Main()
    {
#line (28, 5) - (28, 33) 8 "clr_char_array_is_str_1291.spy"
        var cs = global::System.Array.ConvertAll<char, string>("hello".ToCharArray(), char.ToString);
#line (31, 5) - (31, 20) 8 "clr_char_array_is_str_1291.spy"
        string x = global::Sharpy.ArrayHelpers.GetItem(cs, 0);
#line (32, 5) - (32, 13) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(x);
#line (35, 5) - (35, 21) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Upper(x));
#line (36, 5) - (36, 18) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(x.Length);
#line (39, 5) - (39, 31) 8 "clr_char_array_is_str_1291.spy"
        Sharpy.List<string> tail = global::Sharpy.Slice.GetSlice(cs, 1, 3, null);
#line (40, 5) - (40, 16) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(tail);
#line (41, 5) - (41, 20) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<string>(cs));
#line (42, 5) - (47, 1) 8 "clr_char_array_is_str_1291.spy"
        foreach (var __loopVar_0 in cs)
#line hidden
        {
            var c = __loopVar_0;
#line (43, 9) - (43, 21) 12 "clr_char_array_is_str_1291.spy"
            string ch = c;
#line (44, 9) - (44, 18) 12 "clr_char_array_is_str_1291.spy"
            global::Sharpy.Builtins.Print(ch);
#line hidden
        }

#line (47, 5) - (47, 16) 8 "clr_char_array_is_str_1291.spy"
        Take(global::Sharpy.ArrayHelpers.GetItem(cs, 1));
#line (48, 5) - (48, 24) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(FirstOf(cs));
#line (51, 5) - (51, 46) 8 "clr_char_array_is_str_1291.spy"
        string[] named = global::System.Array.ConvertAll<char, string>("ab".ToCharArray(), char.ToString);
#line (52, 5) - (52, 22) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(named));
#line (53, 5) - (53, 26) 8 "clr_char_array_is_str_1291.spy"
        global::Sharpy.Builtins.Print(Echo(global::Sharpy.ArrayHelpers.GetItem(named, 1)));
#line hidden
    }
}
#line default
