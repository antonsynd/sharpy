#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
    {
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
                this.Position = this.Position + distance;
            }

            public int GetPosition()
            {
                return this.Position;
            }

            public abstract void Describe();
            public Vehicle(string name)
            {
                this.Name = name;
                this.Position = 0;
            }
        }

        public class Car : Vehicle
        {
            public int Wheels;
            public override void Describe()
            {
                global::Sharpy.Core.Exports.Print("Car description:");
                global::Sharpy.Core.Exports.Print(this.Name);
                global::Sharpy.Core.Exports.Print(this.Wheels);
            }

            public Car(string name, int wheels) : base(name)
            {
                this.Wheels = wheels;
            }
        }

        public class Motorcycle : Vehicle
        {
            public bool HasSidecar;
            public override void Move(int distance)
            {
                int multiplier = 1;
                if (this.HasSidecar)
                {
                    multiplier = 1;
                }
                else
                {
                    multiplier = 2;
                }

                this.Position = this.Position + distance * multiplier;
            }

            public override void Describe()
            {
                global::Sharpy.Core.Exports.Print("Motorcycle description:");
                global::Sharpy.Core.Exports.Print(this.Name);
            }

            public Motorcycle(string name, bool hasSidecar) : base(name)
            {
                this.HasSidecar = hasSidecar;
            }
        }

        public static void TestVehicle(Vehicle v)
        {
            v.Describe();
            v.Move(10);
            global::Sharpy.Core.Exports.Print(v.GetPosition());
        }

        public static void Main()
        {
            global::Sharpy.Core.Exports.Print("Testing inheritance");
            Car car = new Car("Sedan", 4);
            TestVehicle(car);
            Motorcycle bike = new Motorcycle("Speedy", false);
            TestVehicle(bike);
            Motorcycle bikeWithSidecar = new Motorcycle("Cruiser", true);
            TestVehicle(bikeWithSidecar);
            global::Sharpy.Core.Exports.Print("All tests complete");
        }
    }
}