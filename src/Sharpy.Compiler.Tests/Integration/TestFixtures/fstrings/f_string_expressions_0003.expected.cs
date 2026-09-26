// Snapshot: F-string with embedded expressions
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace FStringExpressions0003
{
    public static partial class FStringExpressions0003Module
    {
        public static void Main()
        {
#line (60, 5) - (60, 57) 12 "f_string_expressions_0003.spy"
            var competitive = new global::FStringExpressions0003.CompetitivePlayer("Alice", 80, 25, 12);
#line (61, 5) - (61, 40) 12 "f_string_expressions_0003.spy"
            var casual = new global::FStringExpressions0003.CasualPlayer("Bob", 75, 8);
#line (63, 5) - (63, 69) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Player: {(global::Sharpy.Builtins.Str(competitive.Name))}, Level: {(global::Sharpy.Builtins.Str(competitive.Level))}"));
#line (64, 5) - (64, 80) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Base: {(global::Sharpy.Builtins.Str(competitive.BaseScore))}, Bonus: {(global::Sharpy.Builtins.Str(competitive.BonusPoints))}"));
#line (65, 5) - (65, 92) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Score: {(global::Sharpy.Builtins.Str(competitive.GetFinalScore()))} ({(global::Sharpy.Builtins.Str(competitive.Multiplier))}x multiplier)"));
#line (66, 5) - (66, 92) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Rank: {(global::Sharpy.Builtins.Str(competitive.GetRank()))}, Status: {(global::Sharpy.Builtins.Str(competitive.GetFinalScore() > 100))}"));
#line (68, 5) - (68, 66) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Player: {(global::Sharpy.Builtins.Str(casual.Name))}, Games: {(global::Sharpy.Builtins.Str(casual.GamesPlayed))}"));
#line (69, 5) - (69, 75) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Average: {(global::Sharpy.Builtins.Str(global::Sharpy.Builtins.FloorDiv(casual.BaseScore, casual.GamesPlayed)))} per game"));
#line (70, 5) - (70, 54) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Total Score: {(global::Sharpy.Builtins.Str(casual.GetFinalScore()))}"));
#line (71, 5) - (71, 89) 12 "f_string_expressions_0003.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Rank: {(global::Sharpy.Builtins.Str(casual.GetRank()))}, Next level at: {(global::Sharpy.Builtins.Str(11 - casual.GamesPlayed))} games"));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Player")]
    public abstract class Player
    {
        public string Name;
        public int BaseScore;
        public double Multiplier;
        public virtual int GetFinalScore()
#line 16 "f_string_expressions_0003.spy"
        {
#line (17, 9) - (17, 55) 12 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int(this.BaseScore * this.Multiplier);
#line hidden
        }

        public abstract string GetRank();
        public Player(string name, int score)
#line 10 "f_string_expressions_0003.spy"
        {
#line (11, 9) - (11, 25) 12 "f_string_expressions_0003.spy"
            this.Name = name;
#line (12, 9) - (12, 32) 12 "f_string_expressions_0003.spy"
            this.BaseScore = score;
#line (13, 9) - (13, 30) 12 "f_string_expressions_0003.spy"
            this.Multiplier = 1.0d;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "CompetitivePlayer")]
    public class CompetitivePlayer : global::FStringExpressions0003.Player
    {
        public int BonusPoints;
        public int Level;
        public override int GetFinalScore()
#line 34 "f_string_expressions_0003.spy"
        {
#line (35, 9) - (35, 77) 12 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int((this.BaseScore + this.BonusPoints) * this.Multiplier);
#line hidden
        }

        public override string GetRank()
#line 38 "f_string_expressions_0003.spy"
        {
#line (39, 9) - (40, 29) 12 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 150)
#line hidden
            {
#line (40, 13) - (40, 29) 16 "f_string_expressions_0003.spy"
                return "Master";
#line hidden
            }

#line (41, 9) - (42, 29) 12 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 100)
#line hidden
            {
#line (42, 13) - (42, 29) 16 "f_string_expressions_0003.spy"
                return "Expert";
#line hidden
            }

#line (43, 9) - (43, 25) 12 "f_string_expressions_0003.spy"
            return "Novice";
#line hidden
        }

        public CompetitivePlayer(string name, int score, int bonus, int level) : base(name, score)
#line 27 "f_string_expressions_0003.spy"
        {
#line (29, 9) - (29, 34) 12 "f_string_expressions_0003.spy"
            this.BonusPoints = bonus;
#line (30, 9) - (30, 27) 12 "f_string_expressions_0003.spy"
            this.Level = level;
#line (31, 9) - (31, 30) 12 "f_string_expressions_0003.spy"
            this.Multiplier = 1.5d;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "CasualPlayer")]
    public class CasualPlayer : global::FStringExpressions0003.Player
    {
        public int GamesPlayed;
        public override string GetRank()
#line 54 "f_string_expressions_0003.spy"
        {
#line (55, 9) - (56, 30) 12 "f_string_expressions_0003.spy"
            if (this.GamesPlayed > 10)
#line hidden
            {
#line (56, 13) - (56, 30) 16 "f_string_expressions_0003.spy"
                return "Veteran";
#line hidden
            }

#line (57, 9) - (57, 27) 12 "f_string_expressions_0003.spy"
            return "Beginner";
#line hidden
        }

        public CasualPlayer(string name, int score, int games) : base(name, score)
#line 48 "f_string_expressions_0003.spy"
        {
#line (50, 9) - (50, 34) 12 "f_string_expressions_0003.spy"
            this.GamesPlayed = games;
#line (51, 9) - (51, 30) 12 "f_string_expressions_0003.spy"
            this.Multiplier = 1.2d;
#line hidden
        }
    }
}
#line default
