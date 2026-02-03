#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AbstractClassInterfaceStubs
{
    public static class Program
    {
        public static Rectangle Rect = new Rectangle(5, 3);
        public static Circle Circ = new Circle(4);
        public static void Main()
        {
#line 68 "abstract_class_interface_stubs.spy"
            Rect.Display();
#line 69 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(Rect.Calculate());
#line 70 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(Rect.Describe());
#line 72 "abstract_class_interface_stubs.spy"
            Circ.Display();
#line 73 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(Circ.Describe());
        }
    }

    public interface IDisplayable
    {
        void Display();
    }

    public interface ICalculable
    {
        int Calculate();
    }

    public abstract class Shape : IDisplayable
    {
        public string Name;
        public abstract int Area();
        public virtual string Describe()
        {
#line 24 "abstract_class_interface_stubs.spy"
            return this.Name;
        }

        public Shape(string name)
        {
#line 16 "abstract_class_interface_stubs.spy"
            this.Name = name;
        }

        public abstract void Display();
    }

    public class Rectangle : Shape, ICalculable
    {
        public int Width;
        public int Height;
        public override int Area()
        {
#line 37 "abstract_class_interface_stubs.spy"
            return this.Width * this.Height;
        }

        public override void Display()
        {
#line 42 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
#line 43 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(this.Area());
        }

        public int Calculate()
        {
#line 46 "abstract_class_interface_stubs.spy"
            return this.Width + this.Height;
        }

        public Rectangle(int w, int h) : base("Rectangle")
        {
#line 32 "abstract_class_interface_stubs.spy"
            this.Width = w;
#line 33 "abstract_class_interface_stubs.spy"
            this.Height = h;
        }
    }

    public class Circle : Shape
    {
        public int Radius;
        public override int Area()
        {
#line 57 "abstract_class_interface_stubs.spy"
            return 3 * this.Radius * this.Radius;
        }

        public override void Display()
        {
#line 61 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print("Circle with radius:");
#line 62 "abstract_class_interface_stubs.spy"
            global::Sharpy.Core.Exports.Print(this.Radius);
        }

        public Circle(int r) : base("Circle")
        {
#line 53 "abstract_class_interface_stubs.spy"
            this.Radius = r;
        }
    }
}
