// Snapshot: the generic-derived arm of #1408 — MyList<T> keeps its OWN T while the base is pinned at int, so the forwarder parameter is `Sharpy.List<int>`. Guessing the base's arguments from arity would write `Sharpy.List<T>` here and invert both #1287 warm-cache verdicts.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClrGenericBaseForwardersGenericDerived1408
{
    public class MyList<T> : System.Collections.Generic.List<int>
    {
        public MyList() : base()
        {
        }

        public MyList(int capacity) : base(capacity)
        {
        }

        public MyList(Sharpy.List<int> collection) : base(collection)
        {
        }
    }

    public static void Main()
    {
#line (16, 5) - (16, 36) 8 "clr_generic_base_forwarders_generic_derived_1408.spy"
        MyList<string> m = new MyList<string>();
#line (17, 5) - (17, 13) 8 "clr_generic_base_forwarders_generic_derived_1408.spy"
        m.Add(7);
#line (18, 5) - (18, 23) 8 "clr_generic_base_forwarders_generic_derived_1408.spy"
        System.Collections.Generic.List<int> ok = m;
#line (19, 5) - (19, 20) 8 "clr_generic_base_forwarders_generic_derived_1408.spy"
        global::Sharpy.Builtins.Print(ok.Count);
#line hidden
    }
}
#line default
