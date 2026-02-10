#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class DunderEqIequatableSynthesis
{
    public class Point : System.IEquatable<Point>
    {
        public int X;
        public int Y;
        public virtual bool Equals(Point other)
        {
#line 10 "dunder_eq_iequatable_synthesis.spy"
            return this.X == other.X && this.Y == other.Y;
        }

        public static bool operator ==(Point left, Point right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public Point(int x, int y)
        {
#line 6 "dunder_eq_iequatable_synthesis.spy"
            this.X = x;
#line 7 "dunder_eq_iequatable_synthesis.spy"
            this.Y = y;
        }

        public static bool operator !=(Point left, Point right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }

    public static void Main()
    {
#line 13 "dunder_eq_iequatable_synthesis.spy"
        var a = new Point(1, 2);
#line 14 "dunder_eq_iequatable_synthesis.spy"
        var b = new Point(1, 2);
#line 15 "dunder_eq_iequatable_synthesis.spy"
        var c = new Point(3, 4);
#line 18 "dunder_eq_iequatable_synthesis.spy"
        global::Sharpy.Builtins.Print(a == b);
#line 19 "dunder_eq_iequatable_synthesis.spy"
        global::Sharpy.Builtins.Print(a == c);
#line 22 "dunder_eq_iequatable_synthesis.spy"
        global::Sharpy.Builtins.Print(a != b);
#line 23 "dunder_eq_iequatable_synthesis.spy"
        global::Sharpy.Builtins.Print(a != c);
    }
}
