// Snapshot: pins the D4 emitter invariant (#1206) — a staged no-type-args extension call keeps emitting the verbatim member call (lst.Select(...), no explicit type arguments); the MemberAccess-keyed fact must never reach GenerateBclExtensionMethodCall's LoweredTypeArgs spelling.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BclExtensionStaged1206
{
    public static void Main()
    {
#line (10, 5) - (10, 34) 8 "bcl_extension_staged_1206.spy"
        System.Collections.Generic.List<int> lst = new System.Collections.Generic.List<int>();
#line (11, 5) - (11, 15) 8 "bcl_extension_staged_1206.spy"
        lst.Add(3);
#line (12, 5) - (12, 15) 8 "bcl_extension_staged_1206.spy"
        lst.Add(4);
#line (13, 5) - (13, 15) 8 "bcl_extension_staged_1206.spy"
        lst.Add(5);
#line (16, 5) - (16, 46) 8 "bcl_extension_staged_1206.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<string>(lst.Select(x => global::Sharpy.Builtins.Str(x))));
#line (19, 5) - (19, 44) 8 "bcl_extension_staged_1206.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(lst.Where(x => x > 3)));
#line (24, 5) - (24, 68) 8 "bcl_extension_staged_1206.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(lst.Select(x => x * 2).Where(y => y > 6)));
#line (30, 5) - (30, 76) 8 "bcl_extension_staged_1206.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<string>(lst.SelectMany(x => new Sharpy.List<int>() { x, x }, (a, b) => global::Sharpy.Builtins.Str(a + b))));
#line hidden
    }
}
#line default
