#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DunderEqIequatableExplicit
{
    public class Foo : System.IEquatable<Foo>
    {
        public int Value;
        public virtual bool Equals(Foo other)
#line 9 "dunder_eq_iequatable_explicit.spy"
        {
            if (other is null)
                return false;
#line (10, 9) - (10, 42) 1 "dunder_eq_iequatable_explicit.spy"
            return this.Value == other.Value;
        }

        public static bool operator ==(Foo left, Foo right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public Foo(int value)
#line 6 "dunder_eq_iequatable_explicit.spy"
        {
#line (7, 9) - (7, 27) 1 "dunder_eq_iequatable_explicit.spy"
            this.Value = value;
        }

        public static bool operator !=(Foo left, Foo right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }

    public static void Main()
    {
#line (13, 5) - (13, 15) 1 "dunder_eq_iequatable_explicit.spy"
        var a = new Foo(1);
#line (14, 5) - (14, 15) 1 "dunder_eq_iequatable_explicit.spy"
        var b = new Foo(1);
#line (16, 5) - (16, 18) 1 "dunder_eq_iequatable_explicit.spy"
        global::Sharpy.Builtins.Print(a == b);
    }
}
