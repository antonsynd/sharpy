// Snapshot: Null coalescing operator (??)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class NullCoalescing0003
{
    public static int? X = null;
    public static int Y = X ?? 42;
    public static int? A = 100;
    public static int B = A ?? 999;
    public static string? Name = null;
    public static string DefaultName = Name ?? "Guest";
    public static int? First = null;
    public static int? Second = null;
    public static int Third = 77;
    public static int Result = First ?? Second ?? Third;
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
