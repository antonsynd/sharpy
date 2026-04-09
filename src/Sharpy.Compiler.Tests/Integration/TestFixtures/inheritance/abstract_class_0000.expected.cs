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
        {
#line 15 "abstract_class_0000.spy"
            return this.Name;
        }

        public Shape(string name)
        {
#line 7 "abstract_class_0000.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 28 "abstract_class_0000.spy"
            return this.Width * this.Height;
        }

        public Rectangle(double w, double h) : base("Rectangle")
        {
#line 23 "abstract_class_0000.spy"
            this.Width = w;
#line 24 "abstract_class_0000.spy"
            this.Height = h;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
        {
#line 39 "abstract_class_0000.spy"
            return 3.14159d * this.Radius * this.Radius;
        }

        public override string Describe()
        {
#line 43 "abstract_class_0000.spy"
            return "Round Circle";
        }

        public Circle(double r) : base("Circle")
        {
#line 35 "abstract_class_0000.spy"
            this.Radius = r;
        }
    }

    public static void Main()
    {
#line 46 "abstract_class_0000.spy"
        var rect = new Rectangle(4.0d, 5.0d);
#line 47 "abstract_class_0000.spy"
        var circ = new Circle(3.0d);
#line 49 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(rect.Describe());
#line 50 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(rect.Area());
#line 51 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(circ.Describe());
#line 52 "abstract_class_0000.spy"
        global::Sharpy.Builtins.Print(circ.Area());
    }
}
