#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ShapeCalculator
{
    public static class Program
    {
        public static string CompareShapes(Shape s1, Shape s2)
        {
#line 64 "shape_calculator.spy"
            double area1 = s1.Area();
#line 65 "shape_calculator.spy"
            double area2 = s2.Area();
#line 67 "shape_calculator.spy"
            if (area1 > area2)
            {
#line 68 "shape_calculator.spy"
                return s1.Label;
            }
            else if (area2 > area1)
            {
#line 70 "shape_calculator.spy"
                return s2.Label;
            }
            else
            {
#line 72 "shape_calculator.spy"
                return "equal";
            }
        }

        public static Point P1 = new Point(3, 4);
        public static Circle C1 = new Circle("MyCircle", P1, 5);
        public static Rectangle R1 = new Rectangle("MyRect", 10, 7);
        public static ShapeCalculator Calc = new ShapeCalculator();
        public static string Winner = CompareShapes(C1, R1);
        public static void Main()
        {
#line 82 "shape_calculator.spy"
            global::Sharpy.Core.Exports.Print(P1.DistanceFromOrigin());
#line 85 "shape_calculator.spy"
            global::Sharpy.Core.Exports.Print(C1.Area());
#line 86 "shape_calculator.spy"
            global::Sharpy.Core.Exports.Print(R1.Area());
#line 89 "shape_calculator.spy"
            Calc.AddShape(C1);
#line 90 "shape_calculator.spy"
            Calc.AddShape(R1);
#line 92 "shape_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc.GetTotal());
#line 95 "shape_calculator.spy"
            global::Sharpy.Core.Exports.Print(Winner);
        }
    }

    public class Point
    {
        public double X;
        public double Y;
        public double DistanceFromOrigin()
        {
#line 12 "shape_calculator.spy"
            return System.Math.Pow((this.X * this.X + this.Y * this.Y), 0.5);
        }

        public Point(double x, double y)
        {
#line 8 "shape_calculator.spy"
            this.X = x;
#line 9 "shape_calculator.spy"
            this.Y = y;
        }
    }

    public class Shape
    {
        public string Label;
        public virtual double Area()
        {
#line 22 "shape_calculator.spy"
            return 0;
        }

        public Shape(string label)
        {
#line 18 "shape_calculator.spy"
            this.Label = label;
        }
    }

    public class Circle : Shape
    {
        public Point Center;
        public double Radius;
        public override double Area()
        {
#line 35 "shape_calculator.spy"
            return 3.14159 * this.Radius * this.Radius;
        }

        public Circle(string label, Point center, double radius) : base(label)
        {
#line 30 "shape_calculator.spy"
            this.Center = center;
#line 31 "shape_calculator.spy"
            this.Radius = radius;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 48 "shape_calculator.spy"
            return this.Width * this.Height;
        }

        public Rectangle(string label, double width, double height) : base(label)
        {
#line 43 "shape_calculator.spy"
            this.Width = width;
#line 44 "shape_calculator.spy"
            this.Height = height;
        }
    }

    public class ShapeCalculator
    {
        public double TotalArea;
        public void AddShape(Shape shape)
        {
#line 57 "shape_calculator.spy"
            double area = shape.Area();
#line 58 "shape_calculator.spy"
            this.TotalArea = this.TotalArea + area;
        }

        public double GetTotal()
        {
#line 61 "shape_calculator.spy"
            return this.TotalArea;
        }

        public ShapeCalculator()
        {
#line 54 "shape_calculator.spy"
            this.TotalArea = 0;
        }
    }
}
