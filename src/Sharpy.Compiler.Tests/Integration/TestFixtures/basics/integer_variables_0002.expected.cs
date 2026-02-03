#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.IntegerVariables0002
{
    public static class Program
    {
        public static int CalculateRank(int score)
        {
#line 26 "integer_variables_0002.spy"
            if (score >= 100)
            {
#line 27 "integer_variables_0002.spy"
                return 3;
            }
            else if (score >= 50)
            {
#line 29 "integer_variables_0002.spy"
                return 2;
            }
            else
            {
#line 31 "integer_variables_0002.spy"
                return 1;
            }
        }

        public static void Main()
        {
#line 36 "integer_variables_0002.spy"
            var game = new GameScore(10);
#line 37 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(game.PlayerPoints);
#line 40 "integer_variables_0002.spy"
            game.AddKill(15);
#line 41 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(game.GetFinalScore());
#line 44 "integer_variables_0002.spy"
            game.AddBonusMultiplier();
#line 45 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(game.BonusMultiplier);
#line 48 "integer_variables_0002.spy"
            game.AddKill(20);
#line 49 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(game.GetFinalScore());
#line 52 "integer_variables_0002.spy"
            game.ApplyPenalty(10);
#line 53 "integer_variables_0002.spy"
            var final = game.GetFinalScore();
#line 54 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(final);
#line 57 "integer_variables_0002.spy"
            var rank = CalculateRank(final);
#line 58 "integer_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(rank);
        }
    }

    public class GameScore
    {
        public int PlayerPoints;
        public int BonusMultiplier;
        public int Penalty;
        public void AddKill(int points)
        {
#line 14 "integer_variables_0002.spy"
            this.PlayerPoints = this.PlayerPoints + points * this.BonusMultiplier;
        }

        public void AddBonusMultiplier()
        {
#line 17 "integer_variables_0002.spy"
            this.BonusMultiplier = this.BonusMultiplier + 1;
        }

        public void ApplyPenalty(int amount)
        {
#line 20 "integer_variables_0002.spy"
            this.Penalty = this.Penalty + amount;
        }

        public int GetFinalScore()
        {
#line 23 "integer_variables_0002.spy"
            return this.PlayerPoints - this.Penalty;
        }

        public GameScore(int initialPoints)
        {
#line 9 "integer_variables_0002.spy"
            this.PlayerPoints = initialPoints;
#line 10 "integer_variables_0002.spy"
            this.BonusMultiplier = 1;
#line 11 "integer_variables_0002.spy"
            this.Penalty = 0;
        }
    }
}
