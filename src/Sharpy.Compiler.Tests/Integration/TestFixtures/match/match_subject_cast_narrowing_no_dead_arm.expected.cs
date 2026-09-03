// Snapshot: #1370 — the isinstance-narrowed match subject is switched on RAW (no cast), so no arm is statically dead.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchSubjectCastNarrowingNoDeadArm
{
    public class Holder
    {
        public object Value;
        public Holder(object value)
#line 21 "match_subject_cast_narrowing_no_dead_arm.spy"
        {
#line (22, 9) - (22, 27) 12 "match_subject_cast_narrowing_no_dead_arm.spy"
            this.Value = value;
#line hidden
        }
    }

    public static void FromIdentifier(object r)
    {
#line (25, 5) - (25, 33) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        if (!(r is double))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (26, 5) - (32, 1) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        switch (r)
#line hidden
        {
            case double f:
#line (28, 13) - (28, 21) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print(f);
#line hidden
                break;
            default:
#line (30, 13) - (30, 27) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print("other");
#line hidden
                break;
        }
    }

    public static void FromIndex(Sharpy.Dict<string, object> d)
    {
#line (35, 5) - (35, 40) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        if (!(d["key"] is double))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (36, 5) - (42, 1) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        switch (d["key"])
#line hidden
        {
            case double fv:
#line (38, 13) - (38, 22) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print(fv);
#line hidden
                break;
            default:
#line (40, 13) - (40, 27) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print("other");
#line hidden
                break;
        }
    }

    public static void FromMember(Holder h)
    {
#line (43, 5) - (43, 39) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        if (!(h.Value is double))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (44, 5) - (50, 1) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        switch (h.Value)
#line hidden
        {
            case double mv:
#line (46, 13) - (46, 22) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print(mv);
#line hidden
                break;
            default:
#line (48, 13) - (48, 27) 16 "match_subject_cast_narrowing_no_dead_arm.spy"
                global::Sharpy.Builtins.Print("other");
#line hidden
                break;
        }
    }

    public static string FromMatchExpression(object r)
    {
#line (51, 5) - (51, 33) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        if (!(r is double))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (52, 5) - (56, 1) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        return r switch
#line hidden
        {
            double f => "expr-float",
            var _ => "expr-other"
        };
    }

    public static string FromOptionalMatchExpression(Optional<string> x)
    {
#line (62, 5) - (66, 1) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        if (x.IsSome)
#line hidden
        {
#line (63, 9) - (66, 1) 12 "match_subject_cast_narrowing_no_dead_arm.spy"
            return x.Unwrap() switch
#line hidden
            {
                string s => "opt " + s,
                var _ => "opt-other"
            };
        }

#line (66, 5) - (66, 23) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        return "opt-none";
#line hidden
    }

    public static void Main()
    {
#line (69, 5) - (69, 25) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        FromIdentifier(3.5d);
#line (71, 5) - (71, 31) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
#line hidden
        {
        };
#line (72, 5) - (72, 20) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        d["key"] = 1.25d;
#line (73, 5) - (73, 18) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        FromIndex(d);
#line (75, 5) - (75, 29) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        FromMember(new Holder(2.5d));
#line (76, 5) - (76, 39) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        global::Sharpy.Builtins.Print(FromMatchExpression(4.75d));
#line (77, 5) - (77, 54) 8 "match_subject_cast_narrowing_no_dead_arm.spy"
        global::Sharpy.Builtins.Print(FromOptionalMatchExpression(Optional<string>.Some("hi")));
#line hidden
    }
}
#line default
