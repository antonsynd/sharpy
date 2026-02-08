// Snapshot: Interface definition with method signatures
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class InterfaceDefinition
{
    public interface IDrawable
    {
        string Draw();
        int Area();
    }

    public class Circle : IDrawable
    {
        public int Radius;
        public string Draw()
        {
#line 14 "interface_definition.spy"
            return "Drawing Circle";
        }

        public int Area()
        {
#line 17 "interface_definition.spy"
            return 3 * this.Radius * this.Radius;
        }

        public Circle(int r)
        {
#line 11 "interface_definition.spy"
            this.Radius = r;
        }
    }

    public class Rectangle : IDrawable
    {
        public int Width;
        public int Height;
        public string Draw()
        {
#line 28 "interface_definition.spy"
            return "Drawing Rectangle";
        }

        public int Area()
        {
#line 31 "interface_definition.spy"
            return this.Width * this.Height;
        }

        public Rectangle(int w, int h)
        {
#line 24 "interface_definition.spy"
            this.Width = w;
#line 25 "interface_definition.spy"
            this.Height = h;
        }
    }

    public static void Main()
    {
#line 34 "interface_definition.spy"
        IDrawable circle = new Circle(5);
#line 35 "interface_definition.spy"
        global::Sharpy.Builtins.Print(circle.Draw());
#line 36 "interface_definition.spy"
        global::Sharpy.Builtins.Print(circle.Area());
#line 38 "interface_definition.spy"
        IDrawable rect = new Rectangle(4, 6);
#line 39 "interface_definition.spy"
        global::Sharpy.Builtins.Print(rect.Draw());
#line 40 "interface_definition.spy"
        global::Sharpy.Builtins.Print(rect.Area());
    }
}
