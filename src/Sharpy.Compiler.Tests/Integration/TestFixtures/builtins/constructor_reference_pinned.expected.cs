// Snapshot: pins the pinned-constructor-reference emitter arm (#1211) — the collection families emit a constructor lambda whose shape is what generalizing that arm to N parameters must not disturb.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ConstructorReferencePinned
{
    public static partial class ConstructorReferencePinnedModule
    {
        public static global::System.Func<string, int> MakeParser()
        {
#line (6, 5) - (6, 16) 12 "constructor_reference_pinned.spy"
            return global::Sharpy.Builtins.Int;
#line hidden
        }

        public static int Apply(global::System.Func<string, int> fn, string s)
        {
#line (10, 5) - (10, 18) 12 "constructor_reference_pinned.spy"
            return fn(s);
#line hidden
        }

        public static void Main()
        {
#line (15, 5) - (15, 27) 12 "constructor_reference_pinned.spy"
            global::System.Func<string, int> g = global::Sharpy.Builtins.Int;
#line (16, 5) - (16, 19) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(g("42"));
#line (19, 5) - (19, 36) 12 "constructor_reference_pinned.spy"
            global::System.Func<Sharpy.Dict<string, int>> h = () => new Sharpy.Dict<string, int>();
#line (20, 5) - (20, 12) 12 "constructor_reference_pinned.spy"
            var d = h();
#line (21, 5) - (21, 15) 12 "constructor_reference_pinned.spy"
            d["a"] = 1;
#line (22, 5) - (22, 13) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(d);
#line (24, 5) - (24, 32) 12 "constructor_reference_pinned.spy"
            global::System.Func<Sharpy.List<int>> mk = () => new Sharpy.List<int>();
#line (25, 5) - (25, 14) 12 "constructor_reference_pinned.spy"
            var ys = mk();
#line (26, 5) - (26, 17) 12 "constructor_reference_pinned.spy"
            ys.Append(3);
#line (27, 5) - (27, 14) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(ys);
#line (29, 5) - (29, 30) 12 "constructor_reference_pinned.spy"
            global::System.Func<Sharpy.Set<int>> ms = () => new Sharpy.Set<int>();
#line (30, 5) - (30, 13) 12 "constructor_reference_pinned.spy"
            var s = ms();
#line (31, 5) - (31, 13) 12 "constructor_reference_pinned.spy"
            s.Add(7);
#line (32, 5) - (32, 13) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(s);
#line (35, 5) - (35, 29) 12 "constructor_reference_pinned.spy"
            global::System.Func<int, bool> b = global::Sharpy.Builtins.Bool;
#line (36, 5) - (36, 16) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(b(0));
#line (37, 5) - (37, 16) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(b(5));
#line (40, 5) - (40, 22) 12 "constructor_reference_pinned.spy"
            global::System.Func<string, int> p = global::ConstructorReferencePinned.ConstructorReferencePinnedModule.MakeParser();
#line (41, 5) - (41, 18) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(p("7"));
#line (44, 5) - (44, 27) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(global::ConstructorReferencePinned.ConstructorReferencePinnedModule.Apply(global::Sharpy.Builtins.Int, "5"));
#line (49, 5) - (49, 37) 12 "constructor_reference_pinned.spy"
            Sharpy.List<string> xs = new Sharpy.List<string>()
#line hidden
            {
                "3",
                "1",
                "2"
            };
#line (50, 5) - (50, 30) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Map(__callable_p_0 => global::Sharpy.Builtins.Int(__callable_p_0), xs)));
#line (53, 5) - (53, 32) 12 "constructor_reference_pinned.spy"
            Sharpy.List<int> src = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (54, 5) - (54, 49) 12 "constructor_reference_pinned.spy"
            global::System.Func<Sharpy.List<int>, Sharpy.List<int>> copyMaker = __ctor_source_1 => new Sharpy.List<int>(__ctor_source_1);
#line (55, 5) - (55, 27) 12 "constructor_reference_pinned.spy"
            global::Sharpy.Builtins.Print(copyMaker(src));
#line hidden
        }
    }
}
#line default
