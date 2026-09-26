// Snapshot: Ternary arms narrow (#1080) — the true arm reads through x.Unwrap()
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NarrowingTernary
{
    public static partial class NarrowingTernaryModule
    {
        public static int F(Optional<int> x)
        {
#line (6, 5) - (6, 42) 12 "narrowing_ternary.spy"
            return x.IsSome ? x.Unwrap() + 1 : 0;
#line hidden
        }

        public static void Main()
        {
#line (9, 5) - (9, 23) 12 "narrowing_ternary.spy"
            global::Sharpy.Builtins.Print(global::NarrowingTernary.NarrowingTernaryModule.F(Optional<int>.Some(41)));
#line (10, 5) - (10, 21) 12 "narrowing_ternary.spy"
            global::Sharpy.Builtins.Print(global::NarrowingTernary.NarrowingTernaryModule.F(Optional<int>.None));
#line hidden
        }
    }
}
#line default
