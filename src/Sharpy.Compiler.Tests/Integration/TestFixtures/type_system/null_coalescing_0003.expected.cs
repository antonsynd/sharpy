#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.NullCoalescing0003
{
    public static class Program
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
            global::Sharpy.Core.Exports.Print(Y);
#line 19 "null_coalescing_0003.spy"
            global::Sharpy.Core.Exports.Print(B);
#line 22 "null_coalescing_0003.spy"
            global::Sharpy.Core.Exports.Print(DefaultName);
#line 25 "null_coalescing_0003.spy"
            global::Sharpy.Core.Exports.Print(Result);
        }
    }
}
