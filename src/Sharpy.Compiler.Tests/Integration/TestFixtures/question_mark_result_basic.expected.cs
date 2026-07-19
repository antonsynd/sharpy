#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class QuestionMarkResultBasic
{
    public static Result<int, string> ParseInt(string s)
    {
#line (2, 5) - (4, 1) 8 "question_mark_result_basic.spy"
        if (s == "42")
#line hidden
        {
#line (3, 9) - (3, 23) 12 "question_mark_result_basic.spy"
            return Result<int, string>.Ok(42);
#line hidden
        }

#line (4, 5) - (4, 32) 8 "question_mark_result_basic.spy"
        return Result<int, string>.Err("not a number");
#line hidden
    }

    public static Result<int, string> Process(string s)
    {
        var __qm_0 = ParseInt(s);
        if (__qm_0.IsErr)
            return Result<int, string>.Err(__qm_0.UnwrapErr());
#line (7, 5) - (7, 30) 8 "question_mark_result_basic.spy"
        int val = __qm_0.Unwrap();
#line (8, 5) - (8, 24) 8 "question_mark_result_basic.spy"
        return Result<int, string>.Ok(val + 1);
#line hidden
    }

    public static void Main()
    {
#line (11, 5) - (11, 27) 8 "question_mark_result_basic.spy"
        var result = Process("42");
#line (12, 5) - (17, 1) 8 "question_mark_result_basic.spy"
        switch (result)
#line hidden
        {
            case (true, var v, var _):
#line (14, 13) - (14, 21) 16 "question_mark_result_basic.spy"
                global::Sharpy.Builtins.Print(v);
#line hidden
                break;
            case (false, var _, var e):
#line (16, 13) - (16, 21) 16 "question_mark_result_basic.spy"
                global::Sharpy.Builtins.Print(e);
#line hidden
                break;
            default:
                throw new System.InvalidOperationException("Unreachable: exhaustive match");
        }
    }
}
#line default
