// Snapshot: pins the explicit-spelling emission (#1195/#1206) — lst.select[str](f) emits Select with explicit type arguments via GenerateBclExtensionMethodCall; the batch-AF staged path must leave this byte-identical. (The similarly named .cs file without .expected is gitignored /spy-emit scratch, not a snapshot.)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BclExtensionMethodExplicitArgs
{
    public static void Main()
    {
#line (11, 5) - (11, 22) 8 "bcl_extension_method_explicit_args.spy"
        var lst = new System.Collections.Generic.List<int>();
#line (12, 5) - (12, 15) 8 "bcl_extension_method_explicit_args.spy"
        lst.Add(3);
#line (13, 5) - (13, 15) 8 "bcl_extension_method_explicit_args.spy"
        lst.Add(4);
#line (16, 5) - (22, 1) 8 "bcl_extension_method_explicit_args.spy"
        foreach (var __loopVar_0 in lst.Select<int, string>(x => global::Sharpy.Builtins.Str(x)))
#line hidden
        {
            var s = __loopVar_0;
#line (17, 9) - (17, 17) 12 "bcl_extension_method_explicit_args.spy"
            global::Sharpy.Builtins.Print(s);
#line hidden
        }

#line (22, 5) - (26, 1) 8 "bcl_extension_method_explicit_args.spy"
        foreach (var __loopVar_1 in lst.Cast<int>())
#line hidden
        {
            var c = __loopVar_1;
#line (23, 9) - (23, 17) 12 "bcl_extension_method_explicit_args.spy"
            global::Sharpy.Builtins.Print(c);
#line hidden
        }

#line (26, 5) - (28, 1) 8 "bcl_extension_method_explicit_args.spy"
        foreach (var __loopVar_2 in lst.Select<int, string>(x => global::Sharpy.Builtins.Str(x * 2)))
#line hidden
        {
            var t = __loopVar_2;
#line (27, 9) - (27, 17) 12 "bcl_extension_method_explicit_args.spy"
            global::Sharpy.Builtins.Print(t);
#line hidden
        }
    }
}
#line default
