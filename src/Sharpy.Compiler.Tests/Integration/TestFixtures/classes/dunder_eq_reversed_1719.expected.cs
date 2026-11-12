// Snapshot: reflected comparisons; == swaps to the right operand's dunder (ReflectedOperands, plan-92249e T1, #2443)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DunderEqReversed1719
{
    public static partial class DunderEqReversed1719Module
    {
        public static int F()
        {
#line (18, 5) - (18, 15) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print("f");
#line (19, 5) - (19, 14) 12 "dunder_eq_reversed_1719.spy"
            return 7;
#line hidden
        }

        public static void Main()
        {
#line (22, 5) - (22, 12) 12 "dunder_eq_reversed_1719.spy"
            var d = new global::DunderEqReversed1719.D();
#line (23, 5) - (23, 18) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(d == 1);
#line (24, 5) - (24, 18) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(d != 2);
#line (25, 5) - (25, 17) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(d < 3);
#line (26, 5) - (26, 20) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(global::DunderEqReversed1719.DunderEqReversed1719Module.F() is var __refl_0 && d == __refl_0);
#line (27, 5) - (27, 23) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(d == 1 && d == 5);
#line (28, 5) - (28, 26) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print(Optional<int>.Some(1) is var __refl_1 && new global::DunderEqReversed1719.S() == __refl_1);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "D")]
    public class D : System.IEquatable<int>
    {
        public virtual bool Equals(int other)
#line 5 "dunder_eq_reversed_1719.spy"
        {
#line (6, 9) - (6, 27) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print("eq", other);
#line (7, 9) - (7, 21) 12 "dunder_eq_reversed_1719.spy"
            return true;
#line hidden
        }

        public static bool operator ==(D left, int right)
        {
            return left?.Equals(right) ?? false;
        }

        public static bool operator <(D left, int right)
        {
#line (9, 9) - (9, 27) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print("lt", right);
#line (10, 9) - (10, 21) 12 "dunder_eq_reversed_1719.spy"
            return true;
#line hidden
        }

        public static bool operator !=(D left, int right)
        {
            return !(left?.Equals(right) ?? false);
        }

        public static bool operator >(D left, int right)
        {
            throw new TypeError("'>' not supported between instances of 'D' and the given type");
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "S")]
    public class S : System.IEquatable<Optional<int>>
    {
        public virtual bool Equals(Optional<int> other)
#line 13 "dunder_eq_reversed_1719.spy"
        {
#line (14, 9) - (14, 28) 12 "dunder_eq_reversed_1719.spy"
            global::Sharpy.Builtins.Print("seq", other);
#line (15, 9) - (15, 21) 12 "dunder_eq_reversed_1719.spy"
            return true;
#line hidden
        }

        public static bool operator ==(S left, Optional<int> right)
        {
            return left?.Equals(right) ?? false;
        }

        public static bool operator !=(S left, Optional<int> right)
        {
            return !(left?.Equals(right) ?? false);
        }
    }
}
#line default
