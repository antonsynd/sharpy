#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class QuestionMarkOptionalBasic
{
    public static Optional<int> Find(int x)
    {
#line (2, 5) - (4, 1) 1 "question_mark_optional_basic.spy"
        if (x > 0)
        {
#line (3, 9) - (3, 28) 1 "question_mark_optional_basic.spy"
            return Optional<int>.Some(x * 2);
        }

#line (4, 5) - (4, 19) 1 "question_mark_optional_basic.spy"
        return Optional<int>.None;
    }

    public static Optional<int> Process(int x)
    {
        var __qm_0 = Find(x);
        if (__qm_0.IsNone)
            return Optional<int>.None;
#line (7, 5) - (7, 25) 1 "question_mark_optional_basic.spy"
        int val = __qm_0.Unwrap();
#line (8, 5) - (8, 27) 1 "question_mark_optional_basic.spy"
        return Optional<int>.Some(val + 10);
    }

    public static void Main()
    {
#line (11, 5) - (11, 24) 1 "question_mark_optional_basic.spy"
        var result = Process(5);
#line (12, 5) - (17, 1) 1 "question_mark_optional_basic.spy"
        switch (result)
        {
            case (true, var v):
#line (14, 13) - (14, 21) 1 "question_mark_optional_basic.spy"
                global::Sharpy.Builtins.Print(v);
                break;
            case (false, var _):
#line (16, 13) - (16, 26) 1 "question_mark_optional_basic.spy"
                global::Sharpy.Builtins.Print("none");
                break;
            default:
                throw new System.InvalidOperationException("Unreachable: exhaustive match");
        }
    }
}
