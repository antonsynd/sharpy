// Snapshot: Operator overloading via dunder methods (__str__, __add__)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DunderStrAdd
{
    public class Vector
    {
        public int X;
        public int Y;
        public override string ToString()
#line 12 "dunder_str_add.spy"
        {
#line (13, 9) - (13, 46) 1 "dunder_str_add.spy"
            return FormattableString.Invariant($"Vector({(this.X)}, {(this.Y)})");
        }

        public static Vector operator +(Vector left, Vector right)
        {
#line (16, 9) - (16, 59) 1 "dunder_str_add.spy"
            return new Vector(left.X + right.X, left.Y + right.Y);
        }

        public Vector(int x, int y)
#line 7 "dunder_str_add.spy"
        {
#line (8, 9) - (8, 19) 1 "dunder_str_add.spy"
            this.X = x;
#line (9, 9) - (9, 19) 1 "dunder_str_add.spy"
            this.Y = y;
        }
    }

    public static void Main()
    {
#line (19, 5) - (19, 31) 1 "dunder_str_add.spy"
        Vector v1 = new Vector(1, 2);
#line (20, 5) - (20, 31) 1 "dunder_str_add.spy"
        Vector v2 = new Vector(3, 4);
#line (21, 5) - (21, 26) 1 "dunder_str_add.spy"
        Vector v3 = v1 + v2;
#line (22, 5) - (22, 14) 1 "dunder_str_add.spy"
        global::Sharpy.Builtins.Print(v1);
#line (23, 5) - (23, 14) 1 "dunder_str_add.spy"
        global::Sharpy.Builtins.Print(v2);
#line (24, 5) - (24, 14) 1 "dunder_str_add.spy"
        global::Sharpy.Builtins.Print(v3);
    }
}
