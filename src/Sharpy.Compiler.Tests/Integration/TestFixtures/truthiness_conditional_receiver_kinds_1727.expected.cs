#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class TruthinessConditionalReceiverKinds1727
{
    public static void Main()
    {
#line (8, 5) - (8, 23) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        bool flag = true;
#line (11, 5) - (11, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = true;
#line (12, 5) - (12, 22) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        string s = "hello";
#line (13, 5) - (13, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        string t = "";
#line (14, 5) - (18, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? s : t).Length > 0)
#line hidden
        {
#line (15, 9) - (15, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("str-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (17, 9) - (17, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("str-select-truthy:falsy");
#line hidden
        }

#line (18, 5) - (24, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? t : s).Length > 0)
#line hidden
        {
#line (19, 9) - (19, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("str-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (21, 9) - (21, 40) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("str-select-falsy:falsy");
#line hidden
        }

#line (24, 5) - (24, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = false;
#line (25, 5) - (25, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        int n = 42;
#line (26, 5) - (26, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        int m = 0;
#line (27, 5) - (31, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? m : n) != 0)
#line hidden
        {
#line (28, 9) - (28, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("int-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (30, 9) - (30, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("int-select-truthy:falsy");
#line hidden
        }

#line (31, 5) - (37, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? n : m) != 0)
#line hidden
        {
#line (32, 9) - (32, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("int-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (34, 9) - (34, 40) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("int-select-falsy:falsy");
#line hidden
        }

#line (37, 5) - (37, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = true;
#line (38, 5) - (38, 21) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        double f = 3.14d;
#line (39, 5) - (39, 20) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        double g = 0.0d;
#line (40, 5) - (44, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? f : g) != 0)
#line hidden
        {
#line (41, 9) - (41, 44) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("float-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (43, 9) - (43, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("float-select-truthy:falsy");
#line hidden
        }

#line (44, 5) - (50, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? g : f) != 0)
#line hidden
        {
#line (45, 9) - (45, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("float-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (47, 9) - (47, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("float-select-falsy:falsy");
#line hidden
        }

#line (50, 5) - (50, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = false;
#line (51, 5) - (51, 19) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        long ln = 42;
#line (52, 5) - (52, 18) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        long lm = 0;
#line (53, 5) - (57, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? lm : ln) != 0L)
#line hidden
        {
#line (54, 9) - (54, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("long-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (56, 9) - (56, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("long-select-truthy:falsy");
#line hidden
        }

#line (57, 5) - (63, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? ln : lm) != 0L)
#line hidden
        {
#line (58, 9) - (58, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("long-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (60, 9) - (60, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("long-select-falsy:falsy");
#line hidden
        }

#line (63, 5) - (63, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = true;
#line (64, 5) - (64, 26) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.Bytes bs = new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 });
#line (65, 5) - (65, 21) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.Bytes cs = new Sharpy.Bytes(new byte[] { });
#line (66, 5) - (70, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? bs : cs)).Count > 0)
#line hidden
        {
#line (67, 9) - (67, 44) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bytes-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (69, 9) - (69, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bytes-select-truthy:falsy");
#line hidden
        }

#line (70, 5) - (76, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? cs : bs)).Count > 0)
#line hidden
        {
#line (71, 9) - (71, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bytes-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (73, 9) - (73, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bytes-select-falsy:falsy");
#line hidden
        }

#line (76, 5) - (76, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = false;
#line (77, 5) - (77, 28) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
        {
            1,
            2
        };
#line (78, 5) - (78, 24) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.List<int> ys = new Sharpy.List<int>()
#line hidden
        {
        };
#line (79, 5) - (83, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? ys : xs)).Count > 0)
#line hidden
        {
#line (80, 9) - (80, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("list-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (82, 9) - (82, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("list-select-truthy:falsy");
#line hidden
        }

#line (83, 5) - (89, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? xs : ys)).Count > 0)
#line hidden
        {
#line (84, 9) - (84, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("list-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (86, 9) - (86, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("list-select-falsy:falsy");
#line hidden
        }

#line (89, 5) - (89, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = true;
#line (90, 5) - (90, 35) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.Dict<string, int> d1 = new Sharpy.Dict<string, int>()
#line hidden
        {
            {
                "a",
                1
            }
        };
#line (91, 5) - (91, 29) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Sharpy.Dict<string, int> d2 = new Sharpy.Dict<string, int>()
#line hidden
        {
        };
#line (92, 5) - (96, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? d1 : d2)).Count > 0)
#line hidden
        {
#line (93, 9) - (93, 43) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("dict-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (95, 9) - (95, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("dict-select-truthy:falsy");
#line hidden
        }

#line (96, 5) - (102, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if (((global::Sharpy.ISized)(flag ? d2 : d1)).Count > 0)
#line hidden
        {
#line (97, 9) - (97, 42) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("dict-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (99, 9) - (99, 41) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("dict-select-falsy:falsy");
#line hidden
        }

#line (102, 5) - (102, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = false;
#line (103, 5) - (103, 30) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        string? ns = "hello";
#line (104, 5) - (104, 27) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        string? nt = null;
#line (105, 5) - (109, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? nt : ns) != null)
#line hidden
        {
#line (106, 9) - (106, 47) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("nullable-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (108, 9) - (108, 46) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("nullable-select-truthy:falsy");
#line hidden
        }

#line (109, 5) - (115, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? ns : nt) != null)
#line hidden
        {
#line (110, 9) - (110, 46) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("nullable-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (112, 9) - (112, 45) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("nullable-select-falsy:falsy");
#line hidden
        }

#line (115, 5) - (115, 16) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = true;
#line (116, 5) - (116, 25) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Optional<int> o1 = Optional<int>.Some(42);
#line (117, 5) - (117, 23) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        Optional<int> o2 = Optional<int>.None;
#line (118, 5) - (122, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? o1 : o2).IsSome)
#line hidden
        {
#line (119, 9) - (119, 47) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("optional-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (121, 9) - (121, 46) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("optional-select-truthy:falsy");
#line hidden
        }

#line (122, 5) - (128, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? o2 : o1).IsSome)
#line hidden
        {
#line (123, 9) - (123, 46) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("optional-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (125, 9) - (125, 45) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("optional-select-falsy:falsy");
#line hidden
        }

#line (128, 5) - (128, 17) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        flag = false;
#line (129, 5) - (129, 35) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        BoolFlag tv = new BoolFlag(true);
#line (130, 5) - (130, 36) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        BoolFlag fv = new BoolFlag(false);
#line (131, 5) - (135, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? fv : tv).IsTrue)
#line hidden
        {
#line (132, 9) - (132, 48) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bool-conv-select-truthy:truthy");
#line hidden
        }
        else
        {
#line (134, 9) - (134, 47) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bool-conv-select-truthy:falsy");
#line hidden
        }

#line (135, 5) - (140, 1) 8 "truthiness_conditional_receiver_kinds_1727.spy"
        if ((flag ? tv : fv).IsTrue)
#line hidden
        {
#line (136, 9) - (136, 47) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bool-conv-select-falsy:truthy");
#line hidden
        }
        else
        {
#line (138, 9) - (138, 46) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            global::Sharpy.Builtins.Print("bool-conv-select-falsy:falsy");
#line hidden
        }
    }

    public class BoolFlag : Sharpy.IBoolConvertible
    {
        protected bool _Val;
        public virtual bool IsTrue
        {
            get
            {
#line (147, 9) - (147, 26) 16 "truthiness_conditional_receiver_kinds_1727.spy"
                return this._Val;
#line hidden
            }
        }

        public static bool operator true(BoolFlag value)
        {
            return value.IsTrue;
        }

        public BoolFlag(bool v)
#line 143 "truthiness_conditional_receiver_kinds_1727.spy"
        {
#line (144, 9) - (144, 22) 12 "truthiness_conditional_receiver_kinds_1727.spy"
            this._Val = v;
#line hidden
        }

        public static bool operator false(BoolFlag value)
        {
            return !value.IsTrue;
        }
    }
}
#line default
