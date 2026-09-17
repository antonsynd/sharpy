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
    public static int Y = (global::NullCoalescing0003.X).UnwrapOr(42);
    public static Optional<int> A = Optional<int>.Some(100);
    public static int B = (global::NullCoalescing0003.A).UnwrapOr(999);
    public static Optional<string> Name = Optional<string>.None;
    public static string DefaultName = (global::NullCoalescing0003.Name).UnwrapOr("Guest");
    public static Optional<int> First = Optional<int>.None;
    public static Optional<int> Second = Optional<int>.None;
    public static int Third = 77;
    public static int Result = ((global::NullCoalescing0003.First).IsSome ? global::NullCoalescing0003.First : global::NullCoalescing0003.Second).UnwrapOr(global::NullCoalescing0003.Third);
    public static void Main()
    {
#line (16, 5) - (16, 13) 8 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(Y);
#line (19, 5) - (19, 13) 8 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(B);
#line (22, 5) - (22, 24) 8 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(DefaultName);
#line (25, 5) - (25, 18) 8 "null_coalescing_0003.spy"
        global::Sharpy.Builtins.Print(Result);
#line hidden
    }
}
#line default
