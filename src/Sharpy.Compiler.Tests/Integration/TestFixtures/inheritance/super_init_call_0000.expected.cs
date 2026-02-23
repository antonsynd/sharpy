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
        {
#line 8 "super_init_call_0000.spy"
            this.Brand = brand;
#line 9 "super_init_call_0000.spy"
            this.Year = year;
        }
    }

    public class Car : Vehicle
    {
        public int Doors;
        public int Mileage;
        public void Drive(int distance)
        {
#line 21 "super_init_call_0000.spy"
            this.Mileage = this.Mileage + distance;
        }

        public Car(string brand, int year, int doors) : base(brand, year)
        {
#line 17 "super_init_call_0000.spy"
            this.Doors = doors;
#line 18 "super_init_call_0000.spy"
            this.Mileage = 0;
        }
    }

    public class ElectricCar : Car
    {
        public int BatteryCapacity;
        public int ChargeLevel;
        public void DisplayInfo()
        {
#line 33 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Year);
#line 34 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Doors);
#line 35 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.Mileage);
#line 36 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.BatteryCapacity);
#line 37 "super_init_call_0000.spy"
            global::Sharpy.Builtins.Print(this.ChargeLevel);
        }

        public ElectricCar(string brand, int year, int doors, int battery) : base(brand, year, doors)
        {
#line 29 "super_init_call_0000.spy"
            this.BatteryCapacity = battery;
#line 30 "super_init_call_0000.spy"
            this.ChargeLevel = 100;
        }
    }

    public static void Main()
    {
#line 40 "super_init_call_0000.spy"
        var tesla = new ElectricCar("Tesla", 2024, 4, 85);
#line 41 "super_init_call_0000.spy"
        tesla.Drive(150);
#line 42 "super_init_call_0000.spy"
        tesla.DisplayInfo();
    }
}
