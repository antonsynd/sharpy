#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace QuestionMarkNestedResult
{
    public static partial class QuestionMarkNestedResultModule
    {
        public static Result<int, string> Inner()
        {
#line (2, 5) - (2, 19) 12 "question_mark_nested_result.spy"
            return Result<int, string>.Ok(42);
#line hidden
        }

        public static Result<Result<int, string>, string> GetNested()
        {
#line (5, 5) - (5, 24) 12 "question_mark_nested_result.spy"
            return Result<Result<int, string>, string>.Ok(global::QuestionMarkNestedResult.QuestionMarkNestedResultModule.Inner());
#line hidden
        }

        public static Result<int, string> Process()
        {
            var __qm_0 = global::QuestionMarkNestedResult.QuestionMarkNestedResultModule.GetNested();
            if (__qm_0.IsErr)
                return Result<int, string>.Err(__qm_0.UnwrapErr());
            var __qm_1 = __qm_0.Unwrap();
            if (__qm_1.IsErr)
                return Result<int, string>.Err(__qm_1.UnwrapErr());
#line (8, 5) - (8, 31) 12 "question_mark_nested_result.spy"
            int val = __qm_1.Unwrap();
#line (9, 5) - (9, 20) 12 "question_mark_nested_result.spy"
            return Result<int, string>.Ok(val);
#line hidden
        }

        public static void Main()
        {
#line (12, 5) - (12, 23) 12 "question_mark_nested_result.spy"
            var result = global::QuestionMarkNestedResult.QuestionMarkNestedResultModule.Process();
#line (13, 5) - (17, 21) 12 "question_mark_nested_result.spy"
            switch (result)
#line hidden
            {
                case (true, var v, var _):
#line (15, 13) - (15, 21) 20 "question_mark_nested_result.spy"
                    global::Sharpy.Builtins.Print(v);
#line hidden
                    break;
                case (false, var _, var e):
#line (17, 13) - (17, 21) 20 "question_mark_nested_result.spy"
                    global::Sharpy.Builtins.Print(e);
#line hidden
                    break;
                default:
                    throw new System.InvalidOperationException("Unreachable: exhaustive match");
            }
        }
    }
}
#line default
