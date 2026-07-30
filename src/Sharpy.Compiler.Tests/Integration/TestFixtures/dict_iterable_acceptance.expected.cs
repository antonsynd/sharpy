// Snapshot: pins the #1154 DictKeys projection at all four #1159 acceptance sites — Sum, Any, All (identifier builtins) and StringExtensions.Join (member call) each receive n.Keys()/d.Keys(), never the bare dict.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DictIterableAcceptance
{
    public static void Main()
    {
#line (5, 5) - (5, 50) 8 "dict_iterable_acceptance.spy"
        Sharpy.Dict<int, string> n = new Sharpy.Dict<int, string>()
#line hidden
        {
            {
                1,
                "a"
            },
            {
                2,
                "b"
            },
            {
                3,
                "c"
            }
        };
#line (6, 5) - (6, 50) 8 "dict_iterable_acceptance.spy"
        Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
        {
            {
                "b",
                1
            },
            {
                "a",
                2
            },
            {
                "c",
                3
            }
        };
#line (9, 5) - (9, 18) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sum(n.Keys()));
#line (10, 5) - (10, 22) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sum(n.Keys(), 10));
#line (13, 5) - (13, 48) 8 "dict_iterable_acceptance.spy"
        Sharpy.Dict<double, string> f = new Sharpy.Dict<double, string>()
#line hidden
        {
            {
                1.5d,
                "a"
            },
            {
                2.5d,
                "b"
            }
        };
#line (14, 5) - (14, 18) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sum(f.Keys()));
#line (18, 5) - (18, 18) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Any(n.Keys()));
#line (19, 5) - (19, 18) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.All(n.Keys()));
#line (20, 5) - (20, 38) 8 "dict_iterable_acceptance.spy"
        Sharpy.Dict<int, string> falsy = new Sharpy.Dict<int, string>()
#line hidden
        {
            {
                0,
                "x"
            }
        };
#line (21, 5) - (21, 22) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Any(falsy.Keys()));
#line (22, 5) - (22, 22) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.All(falsy.Keys()));
#line (23, 5) - (23, 46) 8 "dict_iterable_acceptance.spy"
        Sharpy.Dict<int, string> mixed = new Sharpy.Dict<int, string>()
#line hidden
        {
            {
                0,
                "x"
            },
            {
                1,
                "y"
            }
        };
#line (24, 5) - (24, 22) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Any(mixed.Keys()));
#line (25, 5) - (25, 22) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.All(mixed.Keys()));
#line (28, 5) - (28, 24) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Join(", ", d.Keys()));
#line (29, 5) - (29, 23) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Join("-", d.Keys()));
#line (30, 5) - (30, 32) 8 "dict_iterable_acceptance.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Join(", ", global::Sharpy.Builtins.Sorted<string>(d.Keys())));
#line hidden
    }
}
#line default
