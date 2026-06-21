#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class QuestionMarkNestedResult
{
    public static Result<int, string> Inner()
    {
#line (2, 5) - (2, 19) 1 "question_mark_nested_result.spy"
        return Result<int, string>.Ok(42);
    }

    public static Result<Result<int, string>, string> GetNested()
    {
#line (5, 5) - (5, 24) 1 "question_mark_nested_result.spy"
        return Result<Result<int, string>, string>.Ok(Inner());
    }

    public static Result<int, string> Process()
    {
        var __qm_0 = GetNested();
        if (__qm_0.IsErr)
            return Result<int, string>.Err(__qm_0.UnwrapErr());
        var __qm_1 = __qm_0.Unwrap();
        if (__qm_1.IsErr)
            return Result<int, string>.Err(__qm_1.UnwrapErr());
#line (8, 5) - (8, 31) 1 "question_mark_nested_result.spy"
        int val = __qm_1.Unwrap();
#line (9, 5) - (9, 20) 1 "question_mark_nested_result.spy"
        return Result<int, string>.Ok(val);
    }

    public static void Main()
    {
#line (12, 5) - (12, 23) 1 "question_mark_nested_result.spy"
        var result = Process();
#line (13, 5) - (18, 1) 1 "question_mark_nested_result.spy"
        switch (result)
        {
            case (true, var v, var _):
#line (15, 13) - (15, 21) 1 "question_mark_nested_result.spy"
                global::Sharpy.Builtins.Print(v);
                break;
            case (false, var _, var e):
#line (17, 13) - (17, 21) 1 "question_mark_nested_result.spy"
                global::Sharpy.Builtins.Print(e);
                break;
            default:
                throw new System.InvalidOperationException("Unreachable: exhaustive match");
        }
    }
}
