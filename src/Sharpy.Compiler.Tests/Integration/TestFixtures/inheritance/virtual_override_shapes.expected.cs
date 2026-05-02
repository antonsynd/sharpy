// Snapshot: Virtual method override polymorphism
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class VirtualOverrideShapes
{
    public abstract class Shape
    {
        public string Name;
        public virtual double Area()
#line 11 "virtual_override_shapes.spy"
        {
#line (12, 9) - (12, 20) 1 "virtual_override_shapes.spy"
            return 0.0d;
        }

        public virtual double Perimeter()
#line 15 "virtual_override_shapes.spy"
        {
#line (16, 9) - (16, 20) 1 "virtual_override_shapes.spy"
            return 0.0d;
        }

        public void Describe()
#line 18 "virtual_override_shapes.spy"
        {
#line (19, 9) - (19, 25) 1 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line (20, 9) - (20, 27) 1 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Area());
#line (21, 9) - (21, 32) 1 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Perimeter());
        }

        public Shape(string name)
#line 7 "virtual_override_shapes.spy"
        {
#line (8, 9) - (8, 25) 1 "virtual_override_shapes.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
#line 33 "virtual_override_shapes.spy"
        {
#line (34, 9) - (34, 41) 1 "virtual_override_shapes.spy"
            return this.Width * this.Height;
        }

        public override double Perimeter()
#line 37 "virtual_override_shapes.spy"
        {
#line (38, 9) - (38, 49) 1 "virtual_override_shapes.spy"
            return 2.0d * (this.Width + this.Height);
        }

        public Rectangle(double width, double height) : base("Rectangle")
#line 27 "virtual_override_shapes.spy"
        {
#line (29, 9) - (29, 27) 1 "virtual_override_shapes.spy"
            this.Width = width;
#line (30, 9) - (30, 29) 1 "virtual_override_shapes.spy"
            this.Height = height;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
#line 48 "virtual_override_shapes.spy"
        {
#line (49, 9) - (49, 49) 1 "virtual_override_shapes.spy"
            return 3.14d * this.Radius * this.Radius;
        }

        public override double Perimeter()
#line 52 "virtual_override_shapes.spy"
        {
#line (53, 9) - (53, 41) 1 "virtual_override_shapes.spy"
            return 2.0d * 3.14d * this.Radius;
        }

        public Circle(double radius) : base("Circle")
#line 43 "virtual_override_shapes.spy"
        {
#line (45, 9) - (45, 29) 1 "virtual_override_shapes.spy"
            this.Radius = radius;
        }
    }

    public static Rectangle Rect = new Rectangle(5.0d, 3.0d);
    public static Circle Circ = new Circle(4.0d);
    public static void Main()
    {
#line (59, 5) - (59, 20) 1 "virtual_override_shapes.spy"
        Rect.Describe();
#line (61, 5) - (61, 20) 1 "virtual_override_shapes.spy"
        Circ.Describe();
    }
}
