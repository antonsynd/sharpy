#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.TestAbstractEllipsis
{
    public static class Program
    {
        /// <summary>
        /// Base shape class with abstract methods
        /// </summary>
        public abstract class Shape
        {
            public abstract double Area();
            public abstract double Perimeter();
            public string Describe()
            {
                return "I am a shape";
            }
        }

        public class Circle : Shape
        {
            public double Radius;
            public override double Area()
            {
                return 3.14159 * this.Radius * this.Radius;
            }

            public override double Perimeter()
            {
                return 2 * 3.14159 * this.Radius;
            }

            public Circle(double radius)
            {
                this.Radius = radius;
            }
        }

        /// <summary>
        /// A class with unimplemented methods
        /// </summary>
        public class TodoClass
        {
            public int NotDoneYet()
            {
                _ = throw new System.NotImplementedException();
            }

            public void EmptyMethod()
            {
                ;
            }
        }

        public interface IDrawable
        {
            void Draw();
            System.ValueTuple<double, double, double, double> GetBounds();
        }

        public static void Main()
        {
        }
    }
}