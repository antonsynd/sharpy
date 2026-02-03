#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassInheritance
{
    public static class Program
    {
        public static void TestVehicle(Vehicle v)
        {
#line 65 "class_inheritance.spy"
            v.Describe();
#line 66 "class_inheritance.spy"
            v.Move(10);
#line 67 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print(v.GetPosition());
        }

        public static void Main()
        {
#line 70 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print("Testing inheritance");
#line 72 "class_inheritance.spy"
            Car car = new Car("Sedan", 4);
#line 73 "class_inheritance.spy"
            TestVehicle(car);
#line 75 "class_inheritance.spy"
            Motorcycle bike = new Motorcycle("Speedy", false);
#line 76 "class_inheritance.spy"
            TestVehicle(bike);
#line 78 "class_inheritance.spy"
            Motorcycle bikeWithSidecar = new Motorcycle("Cruiser", true);
#line 79 "class_inheritance.spy"
            TestVehicle(bikeWithSidecar);
#line 81 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print("All tests complete");
        }
    }

    public interface IMovable
    {
        void Move(int distance);
        int GetPosition();
    }

    public abstract class Vehicle : IMovable
    {
        public int Position;
        public string Name;
        public virtual void Move(int distance)
        {
#line 21 "class_inheritance.spy"
            this.Position = this.Position + distance;
        }

        public int GetPosition()
        {
#line 24 "class_inheritance.spy"
            return this.Position;
        }

        public abstract void Describe();
        public Vehicle(string name)
        {
#line 16 "class_inheritance.spy"
            this.Name = name;
#line 17 "class_inheritance.spy"
            this.Position = 0;
        }
    }

    public class Car : Vehicle
    {
        public int Wheels;
        public override void Describe()
        {
#line 39 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print("Car description:");
#line 40 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
#line 41 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print(this.Wheels);
        }

        public Car(string name, int wheels) : base(name)
        {
#line 35 "class_inheritance.spy"
            this.Wheels = wheels;
        }
    }

    public class Motorcycle : Vehicle
    {
        public bool HasSidecar;
        public override void Move(int distance)
        {
#line 52 "class_inheritance.spy"
            int multiplier = 1;
#line 53 "class_inheritance.spy"
            if (this.HasSidecar)
            {
#line 54 "class_inheritance.spy"
                multiplier = 1;
            }
            else
            {
#line 56 "class_inheritance.spy"
                multiplier = 2;
            }

#line 57 "class_inheritance.spy"
            this.Position = this.Position + distance * multiplier;
        }

        public override void Describe()
        {
#line 61 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print("Motorcycle description:");
#line 62 "class_inheritance.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
        }

        public Motorcycle(string name, bool hasSidecar) : base(name)
        {
#line 48 "class_inheritance.spy"
            this.HasSidecar = hasSidecar;
        }
    }
}
