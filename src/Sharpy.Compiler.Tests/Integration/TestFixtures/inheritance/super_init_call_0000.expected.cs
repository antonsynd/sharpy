// Snapshot: super().__init__() call in subclass constructor
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class SuperInitCall0000
{
    public class Vehicle
    {
        public string Brand;
        public int Year;
        public Vehicle(string brand, int year)
#line 7 "super_init_call_0000.spy"
        {
#line (8, 9) - (8, 27) 1 "super_init_call_0000.spy"
            this.Brand = brand;
#line (9, 9) - (9, 25) 1 "super_init_call_0000.spy"
            this.Year = year;
        }
    }

    public class Car : Vehicle
    {
        public int Doors;
        public int Mileage;
        public void Drive(int distance)
#line 20 "super_init_call_0000.spy"
        {
#line (21, 9) - (21, 33) 1 "super_init_call_0000.spy"
            this.Mileage = this.Mileage + distance;
        }

        public Car(string brand, int year, int doors) : base(brand, year)
#line 15 "super_init_call_0000.spy"
        {
#line (17, 9) - (17, 27) 1 "super_init_call_0000.spy"
            this.Doors = doors;
#line (18, 9) - (18, 25) 1 "super_init_call_0000.spy"
            this.Mileage = 0;
        }
    }

    public class ElectricCar : Car
    {
        public int BatteryCapacity;
        public int ChargeLevel;
        public void DisplayInfo()
#line 32 "super_init_call_0000.spy"
        {
#line (33, 9) - (33, 25) 1 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Year);
#line (34, 9) - (34, 26) 1 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Doors);
#line (35, 9) - (35, 28) 1 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Mileage);
#line (36, 9) - (36, 37) 1 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.BatteryCapacity);
#line (37, 9) - (37, 33) 1 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.ChargeLevel);
        }

        public ElectricCar(string brand, int year, int doors, int battery) : base(brand, year, doors)
#line 27 "super_init_call_0000.spy"
        {
#line (29, 9) - (29, 40) 1 "super_init_call_0000.spy"
            this.BatteryCapacity = battery;
#line (30, 9) - (30, 32) 1 "super_init_call_0000.spy"
            this.ChargeLevel = 100;
        }
    }

    public static void Main()
    {
#line (40, 5) - (40, 46) 1 "super_init_call_0000.spy"
        var tesla = new ElectricCar("Tesla", 2024, 4, 85);
#line (41, 5) - (41, 21) 1 "super_init_call_0000.spy"
        tesla.Drive(150);
#line (42, 5) - (42, 25) 1 "super_init_call_0000.spy"
        tesla.DisplayInfo();
    }
}
