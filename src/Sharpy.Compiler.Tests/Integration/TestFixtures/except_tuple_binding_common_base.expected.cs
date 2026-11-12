// Snapshot: except (A, B) as e binds at the common base with an is-alternation filter; a sibling under the base falls through (plan-92249e T1, #2443)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ExceptTupleBindingCommonBase
{
    public static partial class ExceptTupleBindingCommonBaseModule
    {
        public static void RaiseIt(int which)
        {
#line (38, 5) - (41, 28) 12 "except_tuple_binding_common_base.spy"
            if (which == 0)
#line hidden
            {
#line (39, 9) - (39, 30) 16 "except_tuple_binding_common_base.spy"
                throw new global::ExceptTupleBindingCommonBase.NotFound("nf");
#line hidden
            }
            else
            {
#line (41, 9) - (41, 28) 16 "except_tuple_binding_common_base.spy"
                throw new global::ExceptTupleBindingCommonBase.Denied("dn");
#line hidden
            }
        }

        public static void Main()
        {
#line (46, 5) - (50, 54) 12 "except_tuple_binding_common_base.spy"
            foreach (var __loopVar_0 in global::Sharpy.Builtins.Range(2))
#line hidden
            {
                var i = __loopVar_0;
#line (47, 9) - (50, 54) 16 "except_tuple_binding_common_base.spy"
                try
#line hidden
                {
#line (48, 13) - (48, 24) 20 "except_tuple_binding_common_base.spy"
                    global::ExceptTupleBindingCommonBase.ExceptTupleBindingCommonBaseModule.RaiseIt(i);
#line hidden
                }
                catch (AppError e) when (e is NotFound || e is Denied)
                {
#line (50, 13) - (50, 54) 20 "except_tuple_binding_common_base.spy"
                    global::Sharpy.Builtins.Print("caught", global::Sharpy.PyFormat.PyDunderName(global::Sharpy.Builtins.Type(e)), e.Code);
#line hidden
                }
            }

#line (53, 5) - (58, 29) 12 "except_tuple_binding_common_base.spy"
            try
#line hidden
            {
#line (54, 9) - (54, 29) 16 "except_tuple_binding_common_base.spy"
                throw new global::ExceptTupleBindingCommonBase.NotFound("x");
#line hidden
            }
            catch (NotFound e_1)
            {
#line (56, 9) - (56, 32) 16 "except_tuple_binding_common_base.spy"
                global::Sharpy.Builtins.Print("specific first");
#line hidden
            }
            catch (AppError e_2) when (e_2 is Denied)
            {
#line (58, 9) - (58, 29) 16 "except_tuple_binding_common_base.spy"
                global::Sharpy.Builtins.Print("unreachable");
#line hidden
            }

#line (61, 5) - (66, 29) 12 "except_tuple_binding_common_base.spy"
            try
#line hidden
            {
#line (62, 9) - (62, 29) 16 "except_tuple_binding_common_base.spy"
                throw new global::ExceptTupleBindingCommonBase.NotFound("y");
#line hidden
            }
            catch (AppError e_3) when (e_3 is NotFound || e_3 is Denied)
            {
#line (64, 9) - (64, 29) 16 "except_tuple_binding_common_base.spy"
                global::Sharpy.Builtins.Print("tuple first");
#line hidden
            }

#line (69, 5) - (75, 30) 12 "except_tuple_binding_common_base.spy"
            try
#line hidden
            {
#line (70, 9) - (73, 33) 16 "except_tuple_binding_common_base.spy"
                try
#line hidden
                {
#line (71, 13) - (71, 35) 20 "except_tuple_binding_common_base.spy"
                    throw new global::Sharpy.ValueError("v");
#line hidden
                }
                catch (AppError e_5) when (e_5 is NotFound || e_5 is Denied)
                {
#line (73, 13) - (73, 33) 20 "except_tuple_binding_common_base.spy"
                    global::Sharpy.Builtins.Print("unreachable");
#line hidden
                }
            }
            catch (global::Sharpy.ValueError)
            {
#line (75, 9) - (75, 30) 16 "except_tuple_binding_common_base.spy"
                global::Sharpy.Builtins.Print("fell through");
#line hidden
            }

#line (80, 5) - (86, 64) 12 "except_tuple_binding_common_base.spy"
            try
#line hidden
            {
#line (81, 9) - (84, 36) 16 "except_tuple_binding_common_base.spy"
                try
#line hidden
                {
#line (82, 13) - (82, 30) 20 "except_tuple_binding_common_base.spy"
                    throw new global::ExceptTupleBindingCommonBase.Other("o");
#line hidden
                }
                catch (AppError e_6) when (e_6 is NotFound || e_6 is Denied)
                {
#line (84, 13) - (84, 36) 20 "except_tuple_binding_common_base.spy"
                    global::Sharpy.Builtins.Print("wrongly caught");
#line hidden
                }
            }
            catch (AppError e_7)
            {
#line (86, 9) - (86, 64) 16 "except_tuple_binding_common_base.spy"
                global::Sharpy.Builtins.Print("sibling fell through", global::Sharpy.PyFormat.PyDunderName(global::Sharpy.Builtins.Type(e_7)), e_7.Code);
#line hidden
            }
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "AppError")]
    public class AppError : global::System.Exception
    {
        public int Code = 7;
        public AppError(string msg) : base(msg)
#line 18 "except_tuple_binding_common_base.spy"
        {
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "NotFound")]
    public class NotFound : global::ExceptTupleBindingCommonBase.AppError
    {
        public NotFound(string msg) : base(msg)
#line 23 "except_tuple_binding_common_base.spy"
        {
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Denied")]
    public class Denied : global::ExceptTupleBindingCommonBase.AppError
    {
        public Denied(string msg) : base(msg)
#line 28 "except_tuple_binding_common_base.spy"
        {
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Other")]
    public class Other : global::ExceptTupleBindingCommonBase.AppError
    {
        public Other(string msg) : base(msg)
#line 33 "except_tuple_binding_common_base.spy"
        {
        }
    }
}
