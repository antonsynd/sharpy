// Snapshot: Struct with value semantics
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class StructPointValueSemantics
{
    public struct Point
    {
        public int X;
        public int Y;
        public void Move(int dx, int dy)
#line 11 "struct_point_value_semantics.spy"
        {
#line (12, 9) - (12, 21) 1 "struct_point_value_semantics.spy"
            this.X = this.X + dx;
#line (13, 9) - (13, 21) 1 "struct_point_value_semantics.spy"
            this.Y = this.Y + dy;
        }

        public Point(int x, int y)
#line 7 "struct_point_value_semantics.spy"
        {
#line (8, 9) - (8, 19) 1 "struct_point_value_semantics.spy"
            this.X = x;
#line (9, 9) - (9, 19) 1 "struct_point_value_semantics.spy"
            this.Y = y;
        }
    }

    public static void Main()
    {
#line (16, 5) - (16, 23) 1 "struct_point_value_semantics.spy"
        var p1 = new Point(10, 20);
#line (17, 5) - (17, 12) 1 "struct_point_value_semantics.spy"
        var p2 = p1;
#line (18, 5) - (18, 18) 1 "struct_point_value_semantics.spy"
        p2.Move(5, 5);
#line (20, 5) - (20, 16) 1 "struct_point_value_semantics.spy"
        global::Sharpy.Builtins.Print(p1.X);
#line (21, 5) - (21, 16) 1 "struct_point_value_semantics.spy"
        global::Sharpy.Builtins.Print(p1.Y);
#line (22, 5) - (22, 16) 1 "struct_point_value_semantics.spy"
        global::Sharpy.Builtins.Print(p2.X);
#line (23, 5) - (23, 16) 1 "struct_point_value_semantics.spy"
        global::Sharpy.Builtins.Print(p2.Y);
    }
}
