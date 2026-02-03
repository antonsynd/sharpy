#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.NestedIfInLoop
{
    public static class Program
    {
        public static void SimulateCombat(Player p, Enemy e, int rounds)
        {
#line 68 "nested_if_in_loop.spy"
            int roundNum = 1;
#line 69 "nested_if_in_loop.spy"
            while (roundNum <= rounds)
            {
#line 70 "nested_if_in_loop.spy"
                global::Sharpy.Core.Exports.Print(roundNum);
#line 72 "nested_if_in_loop.spy"
                if (p.IsAlive())
                {
#line 73 "nested_if_in_loop.spy"
                    if (e.IsAlive())
                    {
#line 74 "nested_if_in_loop.spy"
                        int damageDealt = p.Damage;
#line 75 "nested_if_in_loop.spy"
                        if (p.Health < p.CriticalThreshold)
                        {
#line 76 "nested_if_in_loop.spy"
                            if (p.Shield == 0)
                            {
#line 77 "nested_if_in_loop.spy"
                                damageDealt = p.Damage * 2;
#line 78 "nested_if_in_loop.spy"
                                global::Sharpy.Core.Exports.Print(999);
                            }
                        }

#line 80 "nested_if_in_loop.spy"
                        if (e.TakeDamage(damageDealt))
                        {
#line 81 "nested_if_in_loop.spy"
                            global::Sharpy.Core.Exports.Print(1);
                        }
                        else
                        {
#line 83 "nested_if_in_loop.spy"
                            global::Sharpy.Core.Exports.Print(0);
                        }
                    }
                    else
                    {
#line 85 "nested_if_in_loop.spy"
                        global::Sharpy.Core.Exports.Print(777);
#line 86 "nested_if_in_loop.spy"
                        break;
                    }
                }

#line 88 "nested_if_in_loop.spy"
                if (e.IsAlive())
                {
#line 89 "nested_if_in_loop.spy"
                    if (p.IsAlive())
                    {
#line 90 "nested_if_in_loop.spy"
                        if (p.TakeDamage(e.Damage))
                        {
#line 91 "nested_if_in_loop.spy"
                            global::Sharpy.Core.Exports.Print(2);
                        }
                        else
                        {
#line 93 "nested_if_in_loop.spy"
                            global::Sharpy.Core.Exports.Print(3);
                        }
                    }
                    else
                    {
#line 95 "nested_if_in_loop.spy"
                        global::Sharpy.Core.Exports.Print(888);
#line 96 "nested_if_in_loop.spy"
                        break;
                    }
                }

#line 98 "nested_if_in_loop.spy"
                roundNum = roundNum + 1;
            }
        }

        public static Player P1 = new Player(100, 25, 50);
        public static Enemy E1 = new Enemy(120, 30, 5);
        public static void Main()
        {
#line 104 "nested_if_in_loop.spy"
            SimulateCombat(P1, E1, 5);
        }
    }

    public abstract class GameEntity
    {
        public int Health;
        public int Damage;
        public abstract bool TakeDamage(int amount);
        public abstract bool IsAlive();
        public GameEntity(int hp, int dmg)
        {
#line 9 "nested_if_in_loop.spy"
            this.Health = hp;
#line 10 "nested_if_in_loop.spy"
            this.Damage = dmg;
        }
    }

    public class Player : GameEntity
    {
        public int Shield;
        public int CriticalThreshold;
        public override bool TakeDamage(int amount)
        {
#line 31 "nested_if_in_loop.spy"
            int absorbed = 0;
#line 32 "nested_if_in_loop.spy"
            if (this.Shield > 0)
            {
#line 33 "nested_if_in_loop.spy"
                if (amount > this.Shield)
                {
#line 34 "nested_if_in_loop.spy"
                    absorbed = this.Shield;
#line 35 "nested_if_in_loop.spy"
                    this.Shield = 0;
#line 36 "nested_if_in_loop.spy"
                    this.Health = this.Health - (amount - absorbed);
                }
                else
                {
#line 38 "nested_if_in_loop.spy"
                    this.Shield = this.Shield - amount;
#line 39 "nested_if_in_loop.spy"
                    absorbed = amount;
                }
            }
            else
            {
#line 41 "nested_if_in_loop.spy"
                this.Health = this.Health - amount;
            }

#line 42 "nested_if_in_loop.spy"
            return absorbed > 0;
        }

        public override bool IsAlive()
        {
#line 46 "nested_if_in_loop.spy"
            return this.Health > 0;
        }

        public Player(int hp, int dmg, int shld) : base(hp, dmg)
        {
#line 26 "nested_if_in_loop.spy"
            this.Shield = shld;
#line 27 "nested_if_in_loop.spy"
            this.CriticalThreshold = 30;
        }
    }

    public class Enemy : GameEntity
    {
        public int Armor;
        public override bool TakeDamage(int amount)
        {
#line 57 "nested_if_in_loop.spy"
            int reduced = amount - this.Armor;
#line 58 "nested_if_in_loop.spy"
            if (reduced > 0)
            {
#line 59 "nested_if_in_loop.spy"
                this.Health = this.Health - reduced;
#line 60 "nested_if_in_loop.spy"
                return true;
            }

#line 61 "nested_if_in_loop.spy"
            return false;
        }

        public override bool IsAlive()
        {
#line 65 "nested_if_in_loop.spy"
            return this.Health > 0;
        }

        public Enemy(int hp, int dmg, int arm) : base(hp, dmg)
        {
#line 53 "nested_if_in_loop.spy"
            this.Armor = arm;
        }
    }
}
