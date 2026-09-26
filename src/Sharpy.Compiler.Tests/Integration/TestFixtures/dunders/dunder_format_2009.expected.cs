// Snapshot: def __format__ synthesizes System.IFormattable; the body moves into ToString(spec, formatProvider) (#2009)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DunderFormat2009
{
    public static partial class DunderFormat2009Module
    {
        public static void Main()
        {
#line (33, 5) - (33, 20) 12 "dunder_format_2009.spy"
            var m = new global::DunderFormat2009.Money(1250);
#line (34, 5) - (34, 18) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Str(m));
#line (35, 5) - (35, 18) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"{(global::Sharpy.PyFormat.Apply(m, ""))}"));
#line (36, 5) - (36, 22) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"{(global::Sharpy.PyFormat.Apply(m, ".2f"))}"));
#line (37, 5) - (37, 31) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Format(m, ">10.1f"));
#line (38, 5) - (38, 31) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Format("{:,.3f}", m));
#line (39, 5) - (39, 28) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"{(global::Sharpy.PyFormat.Apply(new global::DunderFormat2009.Loud(5), ".2f"))}"));
#line (40, 5) - (40, 30) 12 "dunder_format_2009.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"{(global::Sharpy.PyFormat.Apply(new global::DunderFormat2009.Tag("x"), "spec"))}"));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Money")]
    public class Money : System.IFormattable
    {
        public int Cents;
        public override string ToString()
#line 10 "dunder_format_2009.spy"
        {
#line (11, 9) - (11, 24) 12 "dunder_format_2009.spy"
            return "Money";
#line hidden
        }

        public virtual string ToString(string? spec, global::System.IFormatProvider? formatProvider = null)
#line 13 "dunder_format_2009.spy"
        {
            spec ??= "";
#line (14, 9) - (15, 42) 12 "dunder_format_2009.spy"
            if (spec == "")
#line hidden
            {
#line (15, 13) - (15, 42) 16 "dunder_format_2009.spy"
                return global::Sharpy.Builtins.Str((double)(this.Cents) / 100);
#line hidden
            }

#line (16, 9) - (16, 53) 12 "dunder_format_2009.spy"
            return "$" + global::Sharpy.Builtins.Format((double)(this.Cents) / 100, spec);
#line hidden
        }

        public Money(int cents)
#line 7 "dunder_format_2009.spy"
        {
#line (8, 9) - (8, 27) 12 "dunder_format_2009.spy"
            this.Cents = cents;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Loud")]
    public class Loud : global::DunderFormat2009.Money, System.IFormattable
    {
        public override string ToString(string? spec, global::System.IFormatProvider? formatProvider = null)
#line 21 "dunder_format_2009.spy"
        {
            spec ??= "";
#line (22, 9) - (22, 55) 12 "dunder_format_2009.spy"
            return global::Sharpy.StringExtensions.Upper(base.ToString(spec)) + "!";
#line hidden
        }

        public Loud(int cents) : base(cents)
        {
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Tag")]
    public struct Tag : System.IFormattable
    {
        public string Name;
        public string ToString(string? spec, global::System.IFormatProvider? formatProvider = null)
#line 28 "dunder_format_2009.spy"
        {
            spec ??= "";
#line (29, 9) - (29, 51) 12 "dunder_format_2009.spy"
            return "<" + this.Name + ":" + spec + ">";
#line hidden
        }

        public Tag(string name)
        {
            this.Name = name;
        }
    }
}
#line default
