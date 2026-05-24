// Snapshot: F-string with embedded expressions
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class FStringExpressions0003
{
    public abstract class Player
    {
        public string Name;
        public int BaseScore;
        public double Multiplier;
        public virtual int GetFinalScore()
#line 16 "f_string_expressions_0003.spy"
        {
#line (17, 9) - (17, 55) 1 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int(this.BaseScore * this.Multiplier);
        }

        public abstract string GetRank();
        public Player(string name, int score)
#line 10 "f_string_expressions_0003.spy"
        {
#line (11, 9) - (11, 25) 1 "f_string_expressions_0003.spy"
            this.Name = name;
#line (12, 9) - (12, 32) 1 "f_string_expressions_0003.spy"
            this.BaseScore = score;
#line (13, 9) - (13, 30) 1 "f_string_expressions_0003.spy"
            this.Multiplier = 1.0d;
        }
    }

    public class CompetitivePlayer : Player
    {
        public int BonusPoints;
        public int Level;
        public override int GetFinalScore()
#line 34 "f_string_expressions_0003.spy"
        {
#line (35, 9) - (35, 77) 1 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int((this.BaseScore + this.BonusPoints) * this.Multiplier);
        }

        public override string GetRank()
#line 38 "f_string_expressions_0003.spy"
        {
#line (39, 9) - (41, 1) 1 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 150)
            {
#line (40, 13) - (40, 29) 1 "f_string_expressions_0003.spy"
                return "Master";
            }

#line (41, 9) - (43, 1) 1 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 100)
            {
#line (42, 13) - (42, 29) 1 "f_string_expressions_0003.spy"
                return "Expert";
            }

#line (43, 9) - (43, 25) 1 "f_string_expressions_0003.spy"
            return "Novice";
        }

        public CompetitivePlayer(string name, int score, int bonus, int level) : base(name, score)
#line 27 "f_string_expressions_0003.spy"
        {
#line (29, 9) - (29, 34) 1 "f_string_expressions_0003.spy"
            this.BonusPoints = bonus;
#line (30, 9) - (30, 27) 1 "f_string_expressions_0003.spy"
            this.Level = level;
#line (31, 9) - (31, 30) 1 "f_string_expressions_0003.spy"
            this.Multiplier = 1.5d;
        }
    }

    public class CasualPlayer : Player
    {
        public int GamesPlayed;
        public override string GetRank()
#line 54 "f_string_expressions_0003.spy"
        {
#line (55, 9) - (57, 1) 1 "f_string_expressions_0003.spy"
            if (this.GamesPlayed > 10)
            {
#line (56, 13) - (56, 30) 1 "f_string_expressions_0003.spy"
                return "Veteran";
            }

#line (57, 9) - (57, 27) 1 "f_string_expressions_0003.spy"
            return "Beginner";
        }

        public CasualPlayer(string name, int score, int games) : base(name, score)
#line 48 "f_string_expressions_0003.spy"
        {
#line (50, 9) - (50, 34) 1 "f_string_expressions_0003.spy"
            this.GamesPlayed = games;
#line (51, 9) - (51, 30) 1 "f_string_expressions_0003.spy"
            this.Multiplier = 1.2d;
        }
    }

    public static void Main()
    {
#line (60, 5) - (60, 57) 1 "f_string_expressions_0003.spy"
        var competitive = new CompetitivePlayer("Alice", 80, 25, 12);
#line (61, 5) - (61, 40) 1 "f_string_expressions_0003.spy"
        var casual = new CasualPlayer("Bob", 75, 8);
#line (63, 5) - (63, 69) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Player: {(competitive.Name)}, Level: {(competitive.Level)}"));
#line (64, 5) - (64, 80) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Base: {(competitive.BaseScore)}, Bonus: {(competitive.BonusPoints)}"));
#line (65, 5) - (65, 92) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Score: {(competitive.GetFinalScore())} ({(global::Sharpy.Builtins.FormatFloat(competitive.Multiplier))}x multiplier)"));
#line (66, 5) - (66, 92) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Rank: {(competitive.GetRank())}, Status: {(competitive.GetFinalScore() > 100)}"));
#line (68, 5) - (68, 66) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Player: {(casual.Name)}, Games: {(casual.GamesPlayed)}"));
#line (69, 5) - (69, 75) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Average: {((casual.GamesPlayed == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(casual.BaseScore) / casual.GamesPlayed))))} per game"));
#line (70, 5) - (70, 54) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Total Score: {(casual.GetFinalScore())}"));
#line (71, 5) - (71, 89) 1 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Rank: {(casual.GetRank())}, Next level at: {(11 - casual.GamesPlayed)} games"));
    }
}
