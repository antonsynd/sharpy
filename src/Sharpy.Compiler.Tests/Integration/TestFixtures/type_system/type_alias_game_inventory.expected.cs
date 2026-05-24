// Snapshot: Type alias definitions
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class TypeAliasGameInventory
{
    public abstract class Item
    {
        public int Id;
        public string Name;
        public Optional<string> Description;
        public virtual void DisplayInfo()
#line 22 "type_alias_game_inventory.spy"
        {
#line (23, 9) - (23, 23) 1 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Id);
#line (24, 9) - (24, 25) 1 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line (25, 9) - (25, 58) 1 "type_alias_game_inventory.spy"
            string desc = (this.Description).UnwrapOr("No description");
#line (26, 9) - (26, 20) 1 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(desc);
        }

        public Item(int id, string name, Optional<string> description)
#line 16 "type_alias_game_inventory.spy"
        {
#line (17, 9) - (17, 21) 1 "type_alias_game_inventory.spy"
            this.Id = id;
#line (18, 9) - (18, 25) 1 "type_alias_game_inventory.spy"
            this.Name = name;
#line (19, 9) - (19, 39) 1 "type_alias_game_inventory.spy"
            this.Description = description;
        }
    }

    public class Weapon : Item
    {
        public int Damage;
        public double Durability;
        public override void DisplayInfo()
#line 38 "type_alias_game_inventory.spy"
        {
#line (39, 9) - (39, 31) 1 "type_alias_game_inventory.spy"
            base.DisplayInfo();
#line (40, 9) - (40, 27) 1 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Damage);
#line (41, 9) - (41, 31) 1 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Durability);
        }

        public Weapon(int id, string name, int damage, double durability) : base(id, name, Optional<string>.None)
#line 32 "type_alias_game_inventory.spy"
        {
#line (34, 9) - (34, 29) 1 "type_alias_game_inventory.spy"
            this.Damage = damage;
#line (35, 9) - (35, 37) 1 "type_alias_game_inventory.spy"
            this.Durability = durability;
        }
    }

    public class Inventory
    {
        public int WeaponCount;
        public int TotalDamage;
        public void AddWeapon(Weapon weapon)
#line 51 "type_alias_game_inventory.spy"
        {
#line (52, 9) - (52, 31) 1 "type_alias_game_inventory.spy"
            this.WeaponCount = this.WeaponCount + 1;
#line (53, 9) - (53, 43) 1 "type_alias_game_inventory.spy"
            this.TotalDamage = this.TotalDamage + weapon.Damage;
        }

        public int GetAverageDamage()
#line 55 "type_alias_game_inventory.spy"
        {
#line (56, 9) - (58, 1) 1 "type_alias_game_inventory.spy"
            if (this.WeaponCount == 0)
            {
#line (57, 13) - (57, 22) 1 "type_alias_game_inventory.spy"
                return 0;
            }

#line (58, 9) - (58, 55) 1 "type_alias_game_inventory.spy"
            return (this.WeaponCount == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(this.TotalDamage) / this.WeaponCount)));
        }

        public Inventory()
#line 47 "type_alias_game_inventory.spy"
        {
#line (48, 9) - (48, 30) 1 "type_alias_game_inventory.spy"
            this.WeaponCount = 0;
#line (49, 9) - (49, 30) 1 "type_alias_game_inventory.spy"
            this.TotalDamage = 0;
        }
    }

    public static void Main()
    {
#line (61, 5) - (61, 40) 1 "type_alias_game_inventory.spy"
        Inventory inventory = new Inventory();
#line (63, 5) - (63, 56) 1 "type_alias_game_inventory.spy"
        Weapon sword = new Weapon(1, "Iron Sword", 50, 100.0d);
#line (64, 5) - (64, 53) 1 "type_alias_game_inventory.spy"
        Weapon axe = new Weapon(2, "Battle Axe", 70, 85.5d);
#line (65, 5) - (65, 58) 1 "type_alias_game_inventory.spy"
        Weapon dagger = new Weapon(3, "Steel Dagger", 30, 95.0d);
#line (67, 5) - (67, 32) 1 "type_alias_game_inventory.spy"
        inventory.AddWeapon(sword);
#line (68, 5) - (68, 30) 1 "type_alias_game_inventory.spy"
        inventory.AddWeapon(axe);
#line (69, 5) - (69, 33) 1 "type_alias_game_inventory.spy"
        inventory.AddWeapon(dagger);
#line (71, 5) - (71, 34) 1 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.WeaponCount);
#line (72, 5) - (72, 34) 1 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.TotalDamage);
#line (73, 5) - (73, 42) 1 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.GetAverageDamage());
#line (75, 5) - (75, 25) 1 "type_alias_game_inventory.spy"
        sword.DisplayInfo();
    }
}
