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
        public Sharpy.Str Name;
        public int BaseScore;
        public double Multiplier;
        public virtual int GetFinalScore()
        {
#line 17 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int(this.BaseScore * this.Multiplier);
        }

        public abstract Sharpy.Str GetRank();
        public Player(Sharpy.Str name, int score)
        {
#line 11 "f_string_expressions_0003.spy"
            this.Name = name;
#line 12 "f_string_expressions_0003.spy"
            this.BaseScore = score;
#line 13 "f_string_expressions_0003.spy"
            this.Multiplier = 1.0d;
        }
    }

    public class CompetitivePlayer : Player
    {
        public int BonusPoints;
        public int Level;
        public override int GetFinalScore()
        {
#line 35 "f_string_expressions_0003.spy"
            return global::Sharpy.Builtins.Int((this.BaseScore + this.BonusPoints) * this.Multiplier);
        }

        public override Sharpy.Str GetRank()
        {
#line 39 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 150)
            {
#line 40 "f_string_expressions_0003.spy"
                return ((Sharpy.Str)"Master");
            }

#line 41 "f_string_expressions_0003.spy"
            if (this.GetFinalScore() >= 100)
            {
#line 42 "f_string_expressions_0003.spy"
                return ((Sharpy.Str)"Expert");
            }

#line 43 "f_string_expressions_0003.spy"
            return ((Sharpy.Str)"Novice");
        }

        public CompetitivePlayer(Sharpy.Str name, int score, int bonus, int level) : base(name, score)
        {
#line 29 "f_string_expressions_0003.spy"
            this.BonusPoints = bonus;
#line 30 "f_string_expressions_0003.spy"
            this.Level = level;
#line 31 "f_string_expressions_0003.spy"
            this.Multiplier = 1.5d;
        }
    }

    public class CasualPlayer : Player
    {
        public int GamesPlayed;
        public override Sharpy.Str GetRank()
        {
#line 55 "f_string_expressions_0003.spy"
            if (this.GamesPlayed > 10)
            {
#line 56 "f_string_expressions_0003.spy"
                return ((Sharpy.Str)"Veteran");
            }

#line 57 "f_string_expressions_0003.spy"
            return ((Sharpy.Str)"Beginner");
        }

        public CasualPlayer(Sharpy.Str name, int score, int games) : base(name, score)
        {
#line 50 "f_string_expressions_0003.spy"
            this.GamesPlayed = games;
#line 51 "f_string_expressions_0003.spy"
            this.Multiplier = 1.2d;
        }
    }

    public static void Main()
    {
#line 60 "f_string_expressions_0003.spy"
        var competitive = new CompetitivePlayer(((Sharpy.Str)"Alice"), 80, 25, 12);
#line 61 "f_string_expressions_0003.spy"
        var casual = new CasualPlayer(((Sharpy.Str)"Bob"), 75, 8);
#line 63 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Player: {(competitive.Name)}, Level: {(competitive.Level)}")));
#line 64 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Base: {(competitive.BaseScore)}, Bonus: {(competitive.BonusPoints)}")));
#line 65 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Score: {(competitive.GetFinalScore())} ({(global::Sharpy.Builtins.FormatFloat(competitive.Multiplier))}x multiplier)")));
#line 66 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Rank: {(competitive.GetRank())}, Status: {(competitive.GetFinalScore() > 100)}")));
#line 68 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Player: {(casual.Name)}, Games: {(casual.GamesPlayed)}")));
#line 69 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Average: {((casual.GamesPlayed == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(casual.BaseScore) / casual.GamesPlayed))))} per game")));
#line 70 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Total Score: {(casual.GetFinalScore())}")));
#line 71 "f_string_expressions_0003.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)FormattableString.Invariant($"Rank: {(casual.GetRank())}, Next level at: {(11 - casual.GamesPlayed)} games")));
    }
}
