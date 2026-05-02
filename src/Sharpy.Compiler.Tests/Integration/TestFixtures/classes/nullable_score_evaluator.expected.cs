// Snapshot: Nullable types in class fields and methods
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NullableScoreEvaluator
{
    public class ScoreEvaluator
    {
        public int PassingGrade;
        public string Evaluate(Optional<int> score)
#line 10 "nullable_score_evaluator.spy"
        {
#line (11, 9) - (19, 1) 1 "nullable_score_evaluator.spy"
            if (score.IsNone)
            {
#line (12, 13) - (12, 40) 1 "nullable_score_evaluator.spy"
                return "No score recorded";
            }
            else
            {
#line (14, 13) - (19, 1) 1 "nullable_score_evaluator.spy"
                if (score.Unwrap() >= this.PassingGrade)
                {
#line (15, 17) - (15, 31) 1 "nullable_score_evaluator.spy"
                    return "Pass";
                }
                else
                {
#line (17, 17) - (17, 31) 1 "nullable_score_evaluator.spy"
                    return "Fail";
                }
            }
        }

        public int GetStatus(Optional<int> score)
#line 19 "nullable_score_evaluator.spy"
        {
#line (20, 9) - (28, 1) 1 "nullable_score_evaluator.spy"
            if (score.IsNone)
            {
#line (21, 13) - (21, 22) 1 "nullable_score_evaluator.spy"
                return 0;
            }
            else
            {
#line (23, 13) - (28, 1) 1 "nullable_score_evaluator.spy"
                if (score.Unwrap() >= this.PassingGrade)
                {
#line (24, 17) - (24, 26) 1 "nullable_score_evaluator.spy"
                    return 1;
                }
                else
                {
#line (26, 17) - (26, 26) 1 "nullable_score_evaluator.spy"
                    return 2;
                }
            }
        }

        public ScoreEvaluator(int passing)
#line 7 "nullable_score_evaluator.spy"
        {
#line (8, 9) - (8, 37) 1 "nullable_score_evaluator.spy"
            this.PassingGrade = passing;
        }
    }

    public static void Main()
    {
#line (29, 5) - (29, 35) 1 "nullable_score_evaluator.spy"
        var evaluator = new ScoreEvaluator(60);
#line (31, 5) - (31, 29) 1 "nullable_score_evaluator.spy"
        Optional<int> testScore1 = 75;
#line (32, 5) - (32, 48) 1 "nullable_score_evaluator.spy"
        var result1 = evaluator.Evaluate(testScore1);
#line (33, 5) - (33, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(result1);
#line (34, 5) - (34, 50) 1 "nullable_score_evaluator.spy"
        var status1 = evaluator.GetStatus(testScore1);
#line (35, 5) - (35, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(status1);
#line (37, 5) - (37, 29) 1 "nullable_score_evaluator.spy"
        Optional<int> testScore2 = 45;
#line (38, 5) - (38, 48) 1 "nullable_score_evaluator.spy"
        var result2 = evaluator.Evaluate(testScore2);
#line (39, 5) - (39, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(result2);
#line (40, 5) - (40, 50) 1 "nullable_score_evaluator.spy"
        var status2 = evaluator.GetStatus(testScore2);
#line (41, 5) - (41, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(status2);
#line (43, 5) - (43, 33) 1 "nullable_score_evaluator.spy"
        Optional<int> testScore3 = Optional<int>.None;
#line (44, 5) - (44, 48) 1 "nullable_score_evaluator.spy"
        var result3 = evaluator.Evaluate(testScore3);
#line (45, 5) - (45, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(result3);
#line (46, 5) - (46, 50) 1 "nullable_score_evaluator.spy"
        var status3 = evaluator.GetStatus(testScore3);
#line (47, 5) - (47, 20) 1 "nullable_score_evaluator.spy"
        global::Sharpy.Builtins.Print(status3);
    }
}
