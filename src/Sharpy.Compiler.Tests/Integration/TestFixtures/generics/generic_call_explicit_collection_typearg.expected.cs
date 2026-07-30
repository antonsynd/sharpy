#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class GenericCallExplicitCollectionTypearg
{
    public class Box<T>
    {
        public T Value;
        public Box(T value)
#line 8 "generic_call_explicit_collection_typearg.spy"
        {
#line (9, 9) - (9, 27) 12 "generic_call_explicit_collection_typearg.spy"
            this.Value = value;
#line hidden
        }
    }

    public static T Identity<T>(T x)
    {
#line (13, 5) - (13, 14) 8 "generic_call_explicit_collection_typearg.spy"
        return x;
#line hidden
    }

    public static T First<T>(Sharpy.List<T> xs)
    {
#line (18, 5) - (18, 37) 8 "generic_call_explicit_collection_typearg.spy"
        return Identity<Sharpy.List<T>>(xs)[0];
#line hidden
    }

    public static void Main()
    {
#line (22, 5) - (22, 49) 8 "generic_call_explicit_collection_typearg.spy"
        Sharpy.List<int> xs = Identity<Sharpy.List<int>>(new Sharpy.List<int>() { 1, 2 });
#line (23, 5) - (23, 17) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(xs.GetItemUnchecked(0));
#line (24, 5) - (24, 17) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(xs.GetItemUnchecked(1));
#line (25, 5) - (25, 70) 8 "generic_call_explicit_collection_typearg.spy"
        Sharpy.Dict<string, int> d = Identity<Sharpy.Dict<string, int>>(new Sharpy.Dict<string, int>() { { "a", 10 }, { "b", 20 } });
#line (26, 5) - (26, 18) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(d["a"]);
#line (27, 5) - (27, 18) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(d["b"]);
#line (28, 5) - (28, 43) 8 "generic_call_explicit_collection_typearg.spy"
        Sharpy.Set<int> s = Identity<Sharpy.Set<int>>(new Sharpy.Set<int>() { 3 });
#line (29, 5) - (29, 18) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(s));
#line (31, 5) - (31, 51) 8 "generic_call_explicit_collection_typearg.spy"
        Box<int> b = Identity<Box<int>>(new Box<int>(7));
#line (32, 5) - (32, 19) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(b.Value);
#line (33, 5) - (33, 30) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(First<int>(new Sharpy.List<int>() { 9, 8 }));
#line (35, 5) - (35, 72) 8 "generic_call_explicit_collection_typearg.spy"
        Sharpy.List<Sharpy.List<int>> nested = Identity<Sharpy.List<Sharpy.List<int>>>(new Sharpy.List<Sharpy.List<int>>() { new Sharpy.List<int>() { 1 }, new Sharpy.List<int>() { 2, 3 } });
#line (36, 5) - (36, 23) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(nested));
#line (37, 5) - (37, 24) 8 "generic_call_explicit_collection_typearg.spy"
        global::Sharpy.Builtins.Print(nested.GetItemUnchecked(1)[1]);
#line hidden
    }
}
#line default
