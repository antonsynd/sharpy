// Snapshot: pins the substituted forwarder signatures (#1408) — the copy forwarder must read `Sharpy.List<int>`, the base clause's written argument, and never `Sharpy.List<T>`, the shared OPEN parameter of List[T]'s constructor. IntList declares no `__init__`, so all three forwarders here are synthesized.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClrGenericBaseForwarders1408
{
    public class IntList : System.Collections.Generic.List<int>
    {
        public IntList() : base()
        {
        }

        public IntList(int capacity) : base(capacity)
        {
        }

        public IntList(Sharpy.List<int> collection) : base(collection)
        {
        }
    }

    public static void Main()
    {
#line (22, 5) - (22, 28) 8 "clr_generic_base_forwarders_1408.spy"
        IntList m = new IntList();
#line (23, 5) - (23, 13) 8 "clr_generic_base_forwarders_1408.spy"
        m.Add(1);
#line (24, 5) - (24, 23) 8 "clr_generic_base_forwarders_1408.spy"
        System.Collections.Generic.List<int> ok = m;
#line (25, 5) - (25, 20) 8 "clr_generic_base_forwarders_1408.spy"
        global::Sharpy.Builtins.Print(ok.Count);
#line (27, 5) - (27, 33) 8 "clr_generic_base_forwarders_1408.spy"
        IntList sized = new IntList(8);
#line (28, 5) - (28, 23) 8 "clr_generic_base_forwarders_1408.spy"
        global::Sharpy.Builtins.Print(sized.Count);
#line (30, 5) - (30, 39) 8 "clr_generic_base_forwarders_1408.spy"
        IntList copied = new IntList(new Sharpy.List<int>() { 4, 5 });
#line (31, 5) - (31, 24) 8 "clr_generic_base_forwarders_1408.spy"
        global::Sharpy.Builtins.Print(copied.Count);
#line hidden
    }
}
#line default
