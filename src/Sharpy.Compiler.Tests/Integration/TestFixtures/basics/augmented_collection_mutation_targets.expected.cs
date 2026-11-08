// Snapshot: augmented assignment on list/set/dict lowers to the in-place mutator (#1614) for every Classify arm, the member/index targets and narrowed Optional/nullable receivers (local and member); `n += 2` stays a rebind
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AugmentedCollectionMutationTargets
{
    public static partial class AugmentedCollectionMutationTargetsModule
    {
        public static void Main()
        {
#line (24, 5) - (24, 13) 12 "augmented_collection_mutation_targets.spy"
            var xs = new Sharpy.List<int>()
#line hidden
            {
                1
            };
#line (25, 5) - (25, 18) 12 "augmented_collection_mutation_targets.spy"
            var xsAlias = xs;
#line (26, 5) - (26, 17) 12 "augmented_collection_mutation_targets.spy"
            xs.Extend(new Sharpy.List<int>() { 2, 3 });
#line (27, 5) - (27, 12) 12 "augmented_collection_mutation_targets.spy"
            xs.InPlaceRepeat(2);
#line (28, 5) - (28, 20) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(xsAlias);
#line (30, 5) - (30, 21) 12 "augmented_collection_mutation_targets.spy"
            var s = new Sharpy.Set<int>()
#line hidden
            {
                1,
                2,
                3,
                4
            };
#line (31, 5) - (31, 16) 12 "augmented_collection_mutation_targets.spy"
            var sAlias = s;
#line (32, 5) - (32, 16) 12 "augmented_collection_mutation_targets.spy"
            s.SymmetricDifferenceUpdate(new Sharpy.Set<int>() { 3, 9 });
#line (33, 5) - (33, 13) 12 "augmented_collection_mutation_targets.spy"
            s.DifferenceUpdate(new Sharpy.Set<int>() { 1 });
#line (34, 5) - (34, 13) 12 "augmented_collection_mutation_targets.spy"
            s.Update(new Sharpy.Set<int>() { 2 });
#line (35, 5) - (35, 19) 12 "augmented_collection_mutation_targets.spy"
            s.IntersectionUpdate(new Sharpy.Set<int>() { 2, 4, 9 });
#line (36, 5) - (36, 27) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sorted<int>(sAlias));
#line (38, 5) - (38, 17) 12 "augmented_collection_mutation_targets.spy"
            var d = new Sharpy.Dict<string, int>()
#line hidden
            {
                {
                    "a",
                    1
                }
            };
#line (39, 5) - (39, 16) 12 "augmented_collection_mutation_targets.spy"
            var dAlias = d;
#line (40, 5) - (40, 18) 12 "augmented_collection_mutation_targets.spy"
            d.Update(new Sharpy.Dict<string, int>() { { "b", 2 } });
#line (41, 5) - (41, 19) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(dAlias);
#line (43, 5) - (43, 17) 12 "augmented_collection_mutation_targets.spy"
            var h = new global::AugmentedCollectionMutationTargets.Holder();
#line (44, 5) - (44, 26) 12 "augmented_collection_mutation_targets.spy"
            var itemsAlias = h.Items;
#line (45, 5) - (45, 12) 12 "augmented_collection_mutation_targets.spy"
            h.Add();
#line (46, 5) - (46, 23) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(itemsAlias);
#line (48, 5) - (48, 42) 12 "augmented_collection_mutation_targets.spy"
            Sharpy.Dict<string, Sharpy.List<int>> m = new Sharpy.Dict<string, Sharpy.List<int>>()
#line hidden
            {
                {
                    "k",
                    new Sharpy.List<int>()
                    {
                        1
                    }
                }
            };
#line (49, 5) - (49, 19) 12 "augmented_collection_mutation_targets.spy"
            var inner = m["k"];
#line (50, 5) - (50, 18) 12 "augmented_collection_mutation_targets.spy"
            m["k"].Extend(new Sharpy.List<int>() { 5 });
#line (51, 5) - (51, 17) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(inner);
#line (53, 5) - (53, 33) 12 "augmented_collection_mutation_targets.spy"
            Optional<Sharpy.List<int>> opt = Optional<Sharpy.List<int>>.Some(new Sharpy.List<int>() { 1 });
#line (54, 5) - (57, 25) 12 "augmented_collection_mutation_targets.spy"
            if (opt.IsSome)
#line hidden
            {
#line (55, 9) - (55, 24) 16 "augmented_collection_mutation_targets.spy"
                var optAlias = opt.Unwrap();
#line (56, 9) - (56, 19) 16 "augmented_collection_mutation_targets.spy"
                opt.Unwrap().Extend(new Sharpy.List<int>() { 2 });
#line (57, 9) - (57, 25) 16 "augmented_collection_mutation_targets.spy"
                global::Sharpy.Builtins.Print(optAlias);
#line hidden
            }

#line (59, 5) - (59, 31) 12 "augmented_collection_mutation_targets.spy"
            Sharpy.List<int>? u = new Sharpy.List<int>()
#line hidden
            {
                7
            };
#line (60, 5) - (63, 23) 12 "augmented_collection_mutation_targets.spy"
            if (u is not null)
#line hidden
            {
#line (61, 9) - (61, 20) 16 "augmented_collection_mutation_targets.spy"
                var uAlias = u!;
#line (62, 9) - (62, 17) 16 "augmented_collection_mutation_targets.spy"
                u!.Extend(new Sharpy.List<int>() { 8 });
#line (63, 9) - (63, 23) 16 "augmented_collection_mutation_targets.spy"
                global::Sharpy.Builtins.Print(uAlias);
#line hidden
            }

#line (65, 5) - (65, 18) 12 "augmented_collection_mutation_targets.spy"
            var oAlias = h.O;
#line (66, 5) - (66, 21) 12 "augmented_collection_mutation_targets.spy"
            h.AddNarrowed();
#line (67, 5) - (67, 19) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(oAlias);
#line (69, 5) - (69, 10) 12 "augmented_collection_mutation_targets.spy"
            var n = 1;
#line (70, 5) - (70, 11) 12 "augmented_collection_mutation_targets.spy"
            n = n + 2;
#line (71, 5) - (71, 13) 12 "augmented_collection_mutation_targets.spy"
            global::Sharpy.Builtins.Print(n);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Holder")]
    public class Holder
    {
        public Sharpy.List<int> Items;
        public Sharpy.List<int>? O;
        public void Add()
#line 15 "augmented_collection_mutation_targets.spy"
        {
#line (16, 9) - (16, 26) 12 "augmented_collection_mutation_targets.spy"
            this.Items.Extend(new Sharpy.List<int>() { 2 });
#line hidden
        }

        public void AddNarrowed()
#line 18 "augmented_collection_mutation_targets.spy"
        {
#line (19, 9) - (20, 26) 12 "augmented_collection_mutation_targets.spy"
            if (this.O is not null)
#line hidden
            {
#line (20, 13) - (20, 26) 16 "augmented_collection_mutation_targets.spy"
                this.O!.Extend(new Sharpy.List<int>() { 2 });
#line hidden
            }
        }

        public Holder()
#line 11 "augmented_collection_mutation_targets.spy"
        {
#line (12, 9) - (12, 25) 12 "augmented_collection_mutation_targets.spy"
            this.Items = new Sharpy.List<int>()
#line hidden
            {
                1
            };
#line (13, 9) - (13, 21) 12 "augmented_collection_mutation_targets.spy"
            this.O = new Sharpy.List<int>()
#line hidden
            {
                1
            };
        }
    }
}
#line default
