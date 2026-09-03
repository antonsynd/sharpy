// Snapshot: #1299 pattern fill from the narrowed subject survives #1370 (subject raw, Optional subject keeps .Unwrap()).
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchScrutineeIsinstanceNarrowed
{
    public class Box<T>
    {
        public T Value;
        public Box(T value)
#line 14 "match_scrutinee_isinstance_narrowed.spy"
        {
#line (15, 9) - (15, 27) 12 "match_scrutinee_isinstance_narrowed.spy"
            this.Value = value;
#line hidden
        }
    }

    public static void NarrowedUserGeneric(object o)
    {
#line (18, 5) - (27, 1) 8 "match_scrutinee_isinstance_narrowed.spy"
        if (o is Box<int>)
#line hidden
        {
#line (19, 9) - (24, 1) 12 "match_scrutinee_isinstance_narrowed.spy"
            switch (o)
#line hidden
            {
                case Box<int> { Value: var v }:
#line (21, 17) - (21, 25) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print(v);
#line hidden
                    break;
                default:
#line (23, 17) - (23, 31) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print("other");
#line hidden
                    break;
            }
        }
        else
        {
#line (25, 9) - (25, 27) 12 "match_scrutinee_isinstance_narrowed.spy"
            global::Sharpy.Builtins.Print("not a box");
#line hidden
        }
    }

    public static void NarrowedCollection(object o)
    {
#line (28, 5) - (37, 1) 8 "match_scrutinee_isinstance_narrowed.spy"
        if (o is Sharpy.List<int>)
#line hidden
        {
#line (29, 9) - (34, 1) 12 "match_scrutinee_isinstance_narrowed.spy"
            switch (o)
#line hidden
            {
                case Sharpy.List<int> _:
#line (31, 17) - (31, 38) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print("coupled-list");
#line hidden
                    break;
                default:
#line (33, 17) - (33, 31) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print("other");
#line hidden
                    break;
            }
        }
        else
        {
#line (35, 9) - (35, 28) 12 "match_scrutinee_isinstance_narrowed.spy"
            global::Sharpy.Builtins.Print("not a list");
#line hidden
        }
    }

    public static void NarrowedOptional(Optional<string> x)
    {
#line (40, 5) - (47, 1) 8 "match_scrutinee_isinstance_narrowed.spy"
        if (x.IsSome)
#line hidden
        {
#line (41, 9) - (47, 1) 12 "match_scrutinee_isinstance_narrowed.spy"
            switch (x.Unwrap())
#line hidden
            {
                case string s:
#line (43, 17) - (43, 25) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print(s);
#line hidden
                    break;
                default:
#line (45, 17) - (45, 31) 20 "match_scrutinee_isinstance_narrowed.spy"
                    global::Sharpy.Builtins.Print("other");
#line hidden
                    break;
            }
        }
    }

    public static void Main()
    {
#line (48, 5) - (48, 40) 8 "match_scrutinee_isinstance_narrowed.spy"
        NarrowedUserGeneric(new Box<int>(42));
#line (49, 5) - (49, 29) 8 "match_scrutinee_isinstance_narrowed.spy"
        NarrowedCollection(new Sharpy.List<int>() { 9 });
#line (50, 5) - (50, 34) 8 "match_scrutinee_isinstance_narrowed.spy"
        NarrowedOptional(Optional<string>.Some("hi"));
#line hidden
    }
}
#line default
