#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.NullableScoreEvaluator
{
    public static class Program
    {
        public static void Main()
        {
#line 29 "nullable_score_evaluator.spy"
            var evaluator = new ScoreEvaluator(60);
#line 31 "nullable_score_evaluator.spy"
            int? testScore1 = 75;
#line 32 "nullable_score_evaluator.spy"
            var result1 = evaluator.Evaluate(testScore1);
#line 33 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 34 "nullable_score_evaluator.spy"
            var status1 = evaluator.GetStatus(testScore1);
#line 35 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(status1);
#line 37 "nullable_score_evaluator.spy"
            int? testScore2 = 45;
#line 38 "nullable_score_evaluator.spy"
            var result2 = evaluator.Evaluate(testScore2);
#line 39 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 40 "nullable_score_evaluator.spy"
            var status2 = evaluator.GetStatus(testScore2);
#line 41 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(status2);
#line 43 "nullable_score_evaluator.spy"
            int? testScore3 = null;
#line 44 "nullable_score_evaluator.spy"
            var result3 = evaluator.Evaluate(testScore3);
#line 45 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(result3);
#line 46 "nullable_score_evaluator.spy"
            var status3 = evaluator.GetStatus(testScore3);
#line 47 "nullable_score_evaluator.spy"
            global::Sharpy.Core.Exports.Print(status3);
        }
    }

    public class ScoreEvaluator
    {
        public int PassingGrade;
        public string Evaluate(int? score)
        {
#line 11 "nullable_score_evaluator.spy"
            if (score == null)
            {
#line 12 "nullable_score_evaluator.spy"
                return "No score recorded";
            }
            else
            {
#line 14 "nullable_score_evaluator.spy"
                if (score >= this.PassingGrade)
                {
#line 15 "nullable_score_evaluator.spy"
                    return "Pass";
                }
                else
                {
#line 17 "nullable_score_evaluator.spy"
                    return "Fail";
                }
            }
        }

        public int GetStatus(int? score)
        {
#line 20 "nullable_score_evaluator.spy"
            if (score == null)
            {
#line 21 "nullable_score_evaluator.spy"
                return 0;
            }
            else
            {
#line 23 "nullable_score_evaluator.spy"
                if (score >= this.PassingGrade)
                {
#line 24 "nullable_score_evaluator.spy"
                    return 1;
                }
                else
                {
#line 26 "nullable_score_evaluator.spy"
                    return 2;
                }
            }
        }

        public ScoreEvaluator(int passing)
        {
#line 8 "nullable_score_evaluator.spy"
            this.PassingGrade = passing;
        }
    }
}
