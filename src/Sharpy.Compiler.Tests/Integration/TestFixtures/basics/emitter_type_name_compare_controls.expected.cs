// Snapshot: emitter name-keyed type compares (plan-1faae3 Phase 0) — collection-literal element types, dict-spread comprehension, float32/decimal suffixes, array bridge, parenthesized `match (None)` cast
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace EmitterTypeNameCompareControls
{
    public static partial class EmitterTypeNameCompareControlsModule
    {
        public static int CountBytes(byte[] a)
        {
#line (8, 5) - (8, 19) 12 "emitter_type_name_compare_controls.spy"
            return global::Sharpy.Builtins.Len(a);
#line hidden
        }

        public static int CountInts(int[] a)
        {
#line (12, 5) - (12, 19) 12 "emitter_type_name_compare_controls.spy"
            return global::Sharpy.Builtins.Len(a);
#line hidden
        }

        public static void Main()
        {
#line (16, 5) - (16, 19) 12 "emitter_type_name_compare_controls.spy"
            var xs = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (17, 5) - (17, 24) 12 "emitter_type_name_compare_controls.spy"
            Sharpy.List<string> ys = new Sharpy.List<string>()
#line hidden
            {
            };
#line (18, 5) - (18, 25) 12 "emitter_type_name_compare_controls.spy"
            var d = new Sharpy.Dict<string, int>()
#line hidden
            {
                {
                    "a",
                    1
                },
                {
                    "b",
                    2
                }
            };
#line (19, 5) - (19, 15) 12 "emitter_type_name_compare_controls.spy"
            var s = new Sharpy.Set<int>()
#line hidden
            {
                4,
                5
            };
#line (20, 5) - (20, 24) 12 "emitter_type_name_compare_controls.spy"
            global::Sharpy.Builtins.Print(xs, ys, d, s);
#line hidden
            var __comp_0 = new Sharpy.Dict<string, int>();
            foreach (var __loopVar_1 in new Sharpy.List<Sharpy.Dict<string, int>>()
            {
                d,
                new Sharpy.Dict<string, int>()
                {
                    {
                        "c",
                        3
                    }
                }
            }

            )
            {
                var src = __loopVar_1;
                __comp_0.Update(src);
            }

#line (21, 5) - (21, 44) 12 "emitter_type_name_compare_controls.spy"
            global::Sharpy.Builtins.Print(__comp_0);
#line (22, 5) - (22, 22) 12 "emitter_type_name_compare_controls.spy"
            float f = 0.5f;
#line (23, 5) - (23, 23) 12 "emitter_type_name_compare_controls.spy"
            decimal h = 1.25m;
#line (24, 5) - (24, 22) 12 "emitter_type_name_compare_controls.spy"
            decimal k = 3.0m;
#line (25, 5) - (25, 19) 12 "emitter_type_name_compare_controls.spy"
            global::Sharpy.Builtins.Print(f, h, k);
#line (26, 5) - (26, 52) 12 "emitter_type_name_compare_controls.spy"
            global::Sharpy.Builtins.Print(global::EmitterTypeNameCompareControls.EmitterTypeNameCompareControlsModule.CountBytes((new Sharpy.Bytes(new byte[] { 97, 98, 99, 100 })).ToArray()), global::EmitterTypeNameCompareControls.EmitterTypeNameCompareControlsModule.CountInts((new Sharpy.List<int>() { 7, 8 }).ToArray()));
#line (27, 5) - (31, 27) 12 "emitter_type_name_compare_controls.spy"
            switch ((object?)(null))
#line hidden
            {
                case null:
#line (29, 13) - (29, 32) 20 "emitter_type_name_compare_controls.spy"
                    global::Sharpy.Builtins.Print("paren None");
#line hidden
                    break;
                default:
#line (31, 13) - (31, 27) 20 "emitter_type_name_compare_controls.spy"
                    global::Sharpy.Builtins.Print("other");
#line hidden
                    break;
            }

#line (32, 5) - (35, 1) 12 "emitter_type_name_compare_controls.spy"
            int v = (object?)(null) switch
#line hidden
            {
                null => 1,
                var _ => 2
            };
#line (35, 5) - (35, 13) 12 "emitter_type_name_compare_controls.spy"
            global::Sharpy.Builtins.Print(v);
#line hidden
        }
    }
}
#line default
