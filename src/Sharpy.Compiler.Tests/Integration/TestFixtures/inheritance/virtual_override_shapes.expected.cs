// Snapshot: Virtual method override polymorphism
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class VirtualOverrideShapes
{
    public abstract class Shape
    {
        public string Name;
        public virtual double Area()
        {
#line 12 "virtual_override_shapes.spy"
            return 0;
        }

        public virtual double Perimeter()
        {
#line 16 "virtual_override_shapes.spy"
            return 0;
        }

        public void Describe()
        {
#line 19 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line 20 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Area());
#line 21 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Perimeter());
        }

        public Shape(string name)
        {
#line 8 "virtual_override_shapes.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 34 "virtual_override_shapes.spy"
            return this.Width * this.Height;
        }

        public override double Perimeter()
        {
#line 38 "virtual_override_shapes.spy"
            return 2 * (this.Width + this.Height);
        }

        public Rectangle(double width, double height) : base("Rectangle")
        {
#line 29 "virtual_override_shapes.spy"
            this.Width = width;
#line 30 "virtual_override_shapes.spy"
            this.Height = height;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
        {
#line 49 "virtual_override_shapes.spy"
            return 3.14 * this.Radius * this.Radius;
        }

        public override double Perimeter()
        {
#line 53 "virtual_override_shapes.spy"
            return 2 * 3.14 * this.Radius;
        }

        public Circle(double radius) : base("Circle")
        {
#line 45 "virtual_override_shapes.spy"
            this.Radius = radius;
        }
    }

    public static Rectangle Rect = new Rectangle(5, 3);
    public static Circle Circ = new Circle(4);
    public static void Main()
    {
#line 59 "virtual_override_shapes.spy"
        Rect.Describe();
#line 61 "virtual_override_shapes.spy"
        Circ.Describe();
    }
}
