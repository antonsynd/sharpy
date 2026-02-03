#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AbstractClass0010
{
    public static class Program
    {
        public static void Main()
        {
#line 42 "abstract_class_0010.spy"
            var rect = new Rectangle(4, 5);
#line 43 "abstract_class_0010.spy"
            var circ = new Circle(3);
#line 45 "abstract_class_0010.spy"
            rect.Describe();
#line 46 "abstract_class_0010.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 48 "abstract_class_0010.spy"
            circ.Describe();
#line 49 "abstract_class_0010.spy"
            global::Sharpy.Core.Exports.Print(circ.Area());
        }
    }

    public abstract class Shape
    {
        public string Name;
        public abstract double Area();
        public void Describe()
        {
#line 15 "abstract_class_0010.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
        }

        public Shape(string name)
        {
#line 8 "abstract_class_0010.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 28 "abstract_class_0010.spy"
            return this.Width * this.Height;
        }

        public Rectangle(double w, double h) : base("Rectangle")
        {
#line 23 "abstract_class_0010.spy"
            this.Width = w;
#line 24 "abstract_class_0010.spy"
            this.Height = h;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
        {
#line 39 "abstract_class_0010.spy"
            return 3.14 * this.Radius * this.Radius;
        }

        public Circle(double r) : base("Circle")
        {
#line 35 "abstract_class_0010.spy"
            this.Radius = r;
        }
    }
}
