#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.SuperGrandparentMethod
{
    public static class Program
    {
        public static void Main()
        {
#line 42 "super_grandparent_method.spy"
            var rect = new Rectangle("MyRectangle", 5, 3);
#line 43 "super_grandparent_method.spy"
            rect.Describe();
#line 44 "super_grandparent_method.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 46 "super_grandparent_method.spy"
            var square = new Square("MySquare", 4);
#line 47 "super_grandparent_method.spy"
            square.Describe();
#line 48 "super_grandparent_method.spy"
            global::Sharpy.Core.Exports.Print(square.Area());
        }
    }

    public abstract class Shape
    {
        public string Name;
        public abstract double Area();
        public virtual void Describe()
        {
#line 17 "super_grandparent_method.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
        }

        public Shape(string name)
        {
#line 9 "super_grandparent_method.spy"
            this.Name = name;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 30 "super_grandparent_method.spy"
            return this.Width * this.Height;
        }

        public Rectangle(string name, double width, double height) : base(name)
        {
#line 25 "super_grandparent_method.spy"
            this.Width = width;
#line 26 "super_grandparent_method.spy"
            this.Height = height;
        }
    }

    public class Square : Rectangle
    {
        public override void Describe()
        {
#line 38 "super_grandparent_method.spy"
            base.Describe();
#line 39 "super_grandparent_method.spy"
            global::Sharpy.Core.Exports.Print(this.Width);
        }

        public Square(string name, double side) : base(name, side, side)
        {
        }
    }
}
