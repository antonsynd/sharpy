#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.StructPointValueSemantics
{
    public static class Program
    {
        public static void Main()
        {
#line 16 "struct_point_value_semantics.spy"
            var p1 = new Point(10, 20);
#line 17 "struct_point_value_semantics.spy"
            var p2 = p1;
#line 18 "struct_point_value_semantics.spy"
            p2.Move(5, 5);
#line 20 "struct_point_value_semantics.spy"
            global::Sharpy.Core.Exports.Print(p1.X);
#line 21 "struct_point_value_semantics.spy"
            global::Sharpy.Core.Exports.Print(p1.Y);
#line 22 "struct_point_value_semantics.spy"
            global::Sharpy.Core.Exports.Print(p2.X);
#line 23 "struct_point_value_semantics.spy"
            global::Sharpy.Core.Exports.Print(p2.Y);
        }
    }

    public struct Point
    {
        public int X;
        public int Y;
        public void Move(int dx, int dy)
        {
#line 12 "struct_point_value_semantics.spy"
            this.X = this.X + dx;
#line 13 "struct_point_value_semantics.spy"
            this.Y = this.Y + dy;
        }

        public Point(int x, int y)
        {
#line 8 "struct_point_value_semantics.spy"
            this.X = x;
#line 9 "struct_point_value_semantics.spy"
            this.Y = y;
        }
    }
}
