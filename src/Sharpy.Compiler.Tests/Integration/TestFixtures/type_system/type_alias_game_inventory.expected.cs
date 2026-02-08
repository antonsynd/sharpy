// Snapshot: Type alias definitions
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class TypeAliasGameInventory
{
    public abstract class Item
    {
        public int Id;
        public string Name;
        public string? Description;
        public virtual void DisplayInfo()
        {
#line 23 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Id);
#line 24 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line 25 "type_alias_game_inventory.spy"
            string desc = this.Description ?? "No description";
#line 26 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(desc);
        }

        public Item(int id, string name, string? description)
        {
#line 17 "type_alias_game_inventory.spy"
            this.Id = id;
#line 18 "type_alias_game_inventory.spy"
            this.Name = name;
#line 19 "type_alias_game_inventory.spy"
            this.Description = description;
        }
    }

    public class Weapon : Item
    {
        public int Damage;
        public double Durability;
        public override void DisplayInfo()
        {
#line 39 "type_alias_game_inventory.spy"
            base.DisplayInfo();
#line 40 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Damage);
#line 41 "type_alias_game_inventory.spy"
            global::Sharpy.Builtins.Print(this.Durability);
        }

        public Weapon(int id, string name, int damage, double durability) : base(id, name, null)
        {
#line 34 "type_alias_game_inventory.spy"
            this.Damage = damage;
#line 35 "type_alias_game_inventory.spy"
            this.Durability = durability;
        }
    }

    public class Inventory
    {
        public int WeaponCount;
        public int TotalDamage;
        public void AddWeapon(Weapon weapon)
        {
#line 52 "type_alias_game_inventory.spy"
            this.WeaponCount = this.WeaponCount + 1;
#line 53 "type_alias_game_inventory.spy"
            this.TotalDamage = this.TotalDamage + weapon.Damage;
        }

        public int GetAverageDamage()
        {
#line 56 "type_alias_game_inventory.spy"
            if (this.WeaponCount == 0)
            {
#line 57 "type_alias_game_inventory.spy"
                return 0;
            }

#line 58 "type_alias_game_inventory.spy"
            return (int)System.Math.Floor((double)((double)(this.TotalDamage) / this.WeaponCount));
        }

        public Inventory()
        {
#line 48 "type_alias_game_inventory.spy"
            this.WeaponCount = 0;
#line 49 "type_alias_game_inventory.spy"
            this.TotalDamage = 0;
        }
    }

    public static void Main()
    {
#line 61 "type_alias_game_inventory.spy"
        Inventory inventory = new Inventory();
#line 63 "type_alias_game_inventory.spy"
        Weapon sword = new Weapon(1, "Iron Sword", 50, 100);
#line 64 "type_alias_game_inventory.spy"
        Weapon axe = new Weapon(2, "Battle Axe", 70, 85.5);
#line 65 "type_alias_game_inventory.spy"
        Weapon dagger = new Weapon(3, "Steel Dagger", 30, 95);
#line 67 "type_alias_game_inventory.spy"
        inventory.AddWeapon(sword);
#line 68 "type_alias_game_inventory.spy"
        inventory.AddWeapon(axe);
#line 69 "type_alias_game_inventory.spy"
        inventory.AddWeapon(dagger);
#line 71 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.WeaponCount);
#line 72 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.TotalDamage);
#line 73 "type_alias_game_inventory.spy"
        global::Sharpy.Builtins.Print(inventory.GetAverageDamage());
#line 75 "type_alias_game_inventory.spy"
        sword.DisplayInfo();
    }
}
