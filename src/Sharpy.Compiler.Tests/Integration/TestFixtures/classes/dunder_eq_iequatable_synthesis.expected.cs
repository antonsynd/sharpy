#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DunderEqIequatableSynthesis
{
    public static partial class DunderEqIequatableSynthesisModule
    {
        public static void Main()
        {
#line (13, 5) - (13, 20) 12 "dunder_eq_iequatable_synthesis.spy"
            var a = new global::DunderEqIequatableSynthesis.Point(1, 2);
#line (14, 5) - (14, 20) 12 "dunder_eq_iequatable_synthesis.spy"
            var b = new global::DunderEqIequatableSynthesis.Point(1, 2);
#line (15, 5) - (15, 20) 12 "dunder_eq_iequatable_synthesis.spy"
            var c = new global::DunderEqIequatableSynthesis.Point(3, 4);
#line (18, 5) - (18, 18) 12 "dunder_eq_iequatable_synthesis.spy"
            global::Sharpy.Builtins.Print(a == b);
#line (19, 5) - (19, 18) 12 "dunder_eq_iequatable_synthesis.spy"
            global::Sharpy.Builtins.Print(a == c);
#line (22, 5) - (22, 18) 12 "dunder_eq_iequatable_synthesis.spy"
            global::Sharpy.Builtins.Print(a != b);
#line (23, 5) - (23, 18) 12 "dunder_eq_iequatable_synthesis.spy"
            global::Sharpy.Builtins.Print(a != c);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Point")]
    public class Point : System.IEquatable<global::DunderEqIequatableSynthesis.Point>
    {
        public int X;
        public int Y;
        public virtual bool Equals(global::DunderEqIequatableSynthesis.Point other)
#line 9 "dunder_eq_iequatable_synthesis.spy"
        {
            if (other is null)
                return false;
#line (10, 9) - (10, 56) 12 "dunder_eq_iequatable_synthesis.spy"
            return this.X == other.X && this.Y == other.Y;
#line hidden
        }

        public static bool operator ==(Point left, global::DunderEqIequatableSynthesis.Point right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public Point(int x, int y)
#line 5 "dunder_eq_iequatable_synthesis.spy"
        {
#line (6, 9) - (6, 19) 12 "dunder_eq_iequatable_synthesis.spy"
            this.X = x;
#line (7, 9) - (7, 19) 12 "dunder_eq_iequatable_synthesis.spy"
            this.Y = y;
#line hidden
        }

        public static bool operator !=(Point left, global::DunderEqIequatableSynthesis.Point right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }
}
#line default
