// Snapshot: Null coalescing operator (??)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NullCoalescing0003
{
    public static Optional<int> X = Optional<int>.None;
    public static int Y = (X).UnwrapOr(42);
    public static Optional<int> A = 100;
    public static int B = (A).UnwrapOr(999);
    public static Optional<Sharpy.Str> Name = Optional<Sharpy.Str>.None;
    public static Sharpy.Str DefaultName = (Name).UnwrapOr(((Sharpy.Str)"Guest"));
    public static Optional<int> First = Optional<int>.None;
    public static Optional<int> Second = Optional<int>.None;
    public static int Third = 77;
    public static int Result = ((First).IsSome ? First : Second).UnwrapOr(Third);
    public static void Main()
    {
#line 16 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(Y);
#line 19 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(B);
#line 22 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(DefaultName);
#line 25 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(Result);
    }
}
