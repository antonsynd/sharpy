#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace QuestionMarkOptionalBasic
{
    public static partial class QuestionMarkOptionalBasicModule
    {
        public static Optional<int> Find(int x)
        {
#line (2, 5) - (3, 28) 12 "question_mark_optional_basic.spy"
            if (x > 0)
#line hidden
            {
#line (3, 9) - (3, 28) 16 "question_mark_optional_basic.spy"
                return Optional<int>.Some(x * 2);
#line hidden
            }

#line (4, 5) - (4, 19) 12 "question_mark_optional_basic.spy"
            return Optional<int>.None;
#line hidden
        }

        public static Optional<int> Process(int x)
        {
            var __qm_0 = global::QuestionMarkOptionalBasic.QuestionMarkOptionalBasicModule.Find(x);
            if (__qm_0.IsNone)
                return Optional<int>.None;
#line (7, 5) - (7, 25) 12 "question_mark_optional_basic.spy"
            int val = __qm_0.Unwrap();
#line (8, 5) - (8, 27) 12 "question_mark_optional_basic.spy"
            return Optional<int>.Some(val + 10);
#line hidden
        }

        public static void Main()
        {
#line (11, 5) - (11, 24) 12 "question_mark_optional_basic.spy"
            var result = global::QuestionMarkOptionalBasic.QuestionMarkOptionalBasicModule.Process(5);
#line (12, 5) - (16, 26) 12 "question_mark_optional_basic.spy"
            switch (result)
#line hidden
            {
                case (true, var v):
#line (14, 13) - (14, 21) 20 "question_mark_optional_basic.spy"
                    global::Sharpy.Builtins.Print(v);
#line hidden
                    break;
                case (false, var _):
#line (16, 13) - (16, 26) 20 "question_mark_optional_basic.spy"
                    global::Sharpy.Builtins.Print("none");
#line hidden
                    break;
                default:
                    throw new System.InvalidOperationException("Unreachable: exhaustive match");
            }
        }
    }
}
#line default
