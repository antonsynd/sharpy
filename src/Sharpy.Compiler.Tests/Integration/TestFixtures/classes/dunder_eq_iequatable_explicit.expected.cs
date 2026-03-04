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
        {
            if (other is null)
                return false;
#line 10 "dunder_eq_iequatable_explicit.spy"
            return this.Value == other.Value;
        }

        public static bool operator ==(Foo left, Foo right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public Foo(int value)
        {
#line 7 "dunder_eq_iequatable_explicit.spy"
            this.Value = value;
        }

        public static bool operator !=(Foo left, Foo right)
        {
            return !(left?.Equals(right) ?? right is null);
        }
    }

    public static void Main()
    {
#line 13 "dunder_eq_iequatable_explicit.spy"
        var a = new Foo(1);
#line 14 "dunder_eq_iequatable_explicit.spy"
        var b = new Foo(1);
#line 16 "dunder_eq_iequatable_explicit.spy"
        global::Sharpy.Builtins.Print(a == b);
    }
}
