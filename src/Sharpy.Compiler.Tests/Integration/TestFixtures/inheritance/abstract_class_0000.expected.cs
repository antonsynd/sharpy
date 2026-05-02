// Snapshot: Abstract class with abstract and virtual methods
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AbstractClass0000
{
    public abstract class Shape
    {
        public string Name;
        public abstract double Area();
        public virtual string Describe()
#line 14 "abstract_class_0000.spy"
        {
#line (15, 9) - (15, 26) 1 "abstract_class_0000.spy"
            return this.Name;
        }

        public Shape(string name)
#line 6 "abstract_class_0000.spy"
        {
#line (7, 9) - (7, 25) 1 "abstract_class_0000.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
#line 27 "abstract_class_0000.spy"
        {
#line (28, 9) - (28, 41) 1 "abstract_class_0000.spy"
            return this.Width * this.Height;
        }

        public Rectangle(double w, double h) : base("Rectangle")
#line 21 "abstract_class_0000.spy"
        {
#line (23, 9) - (23, 23) 1 "abstract_class_0000.spy"
            this.Width = w;
#line (24, 9) - (24, 24) 1 "abstract_class_0000.spy"
            this.Height = h;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
#line 38 "abstract_class_0000.spy"
        {
#line (39, 9) - (39, 52) 1 "abstract_class_0000.spy"
            return 3.14159d * this.Radius * this.Radius;
        }

        public override string Describe()
#line 42 "abstract_class_0000.spy"
        {
#line (43, 9) - (43, 31) 1 "abstract_class_0000.spy"
            return "Round Circle";
        }

        public Circle(double r) : base("Circle")
#line 33 "abstract_class_0000.spy"
        {
#line (35, 9) - (35, 24) 1 "abstract_class_0000.spy"
            this.Radius = r;
        }
    }

    public static void Main()
    {
#line (46, 5) - (46, 31) 1 "abstract_class_0000.spy"
        var rect = new Rectangle(4.0d, 5.0d);
#line (47, 5) - (47, 23) 1 "abstract_class_0000.spy"
        var circ = new Circle(3.0d);
#line (49, 5) - (49, 27) 1 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(rect.Describe());
#line (50, 5) - (50, 23) 1 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(rect.Area());
#line (51, 5) - (51, 27) 1 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(circ.Describe());
#line (52, 5) - (52, 23) 1 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(circ.Area());
    }
}
