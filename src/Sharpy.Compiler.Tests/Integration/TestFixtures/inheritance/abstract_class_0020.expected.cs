#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AbstractClass0020
{
    public static class Program
    {
        public static void Main()
        {
#line 52 "abstract_class_0020.spy"
            var rect = new Rectangle(4, 5);
#line 53 "abstract_class_0020.spy"
            var sq = new Square(3);
#line 55 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(rect.Describe());
#line 56 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 57 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(rect.Perimeter());
#line 58 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(sq.Describe());
#line 59 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(sq.Area());
#line 60 "abstract_class_0020.spy"
            global::Sharpy.Core.Exports.Print(sq.Perimeter());
        }
    }

    public abstract class Shape
    {
        public string Name;
        public abstract double Area();
        public abstract double Perimeter();
        public string Describe()
        {
#line 17 "abstract_class_0020.spy"
            return this.Name;
        }

        public Shape(string name)
        {
#line 9 "abstract_class_0020.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 30 "abstract_class_0020.spy"
            return this.Width * this.Height;
        }

        public override double Perimeter()
        {
#line 34 "abstract_class_0020.spy"
            return 2 * (this.Width + this.Height);
        }

        public Rectangle(double w, double h) : base("Rectangle")
        {
#line 25 "abstract_class_0020.spy"
            this.Width = w;
#line 26 "abstract_class_0020.spy"
            this.Height = h;
        }
    }

    public class Square : Shape
    {
        public double Side;
        public override double Area()
        {
#line 45 "abstract_class_0020.spy"
            return this.Side * this.Side;
        }

        public override double Perimeter()
        {
#line 49 "abstract_class_0020.spy"
            return 4 * this.Side;
        }

        public Square(double s) : base("Square")
        {
#line 41 "abstract_class_0020.spy"
            this.Side = s;
        }
    }
}
