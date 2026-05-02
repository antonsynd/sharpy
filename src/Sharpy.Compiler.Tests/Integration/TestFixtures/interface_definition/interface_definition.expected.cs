// Snapshot: Interface definition with method signatures
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public virtual string Draw()
#line 13 "interface_definition.spy"
        {
#line (14, 9) - (14, 33) 1 "interface_definition.spy"
            return "Drawing Circle";
        }

        public virtual int Area()
#line 16 "interface_definition.spy"
        {
#line (17, 9) - (17, 46) 1 "interface_definition.spy"
            return 3 * this.Radius * this.Radius;
        }

        public Circle(int r)
#line 10 "interface_definition.spy"
        {
#line (11, 9) - (11, 24) 1 "interface_definition.spy"
            this.Radius = r;
        }
    }

    public class Rectangle : IDrawable
    {
        public int Width;
        public int Height;
        public virtual string Draw()
#line 27 "interface_definition.spy"
        {
#line (28, 9) - (28, 36) 1 "interface_definition.spy"
            return "Drawing Rectangle";
        }

        public virtual int Area()
#line 30 "interface_definition.spy"
        {
#line (31, 9) - (31, 41) 1 "interface_definition.spy"
            return this.Width * this.Height;
        }

        public Rectangle(int w, int h)
#line 23 "interface_definition.spy"
        {
#line (24, 9) - (24, 23) 1 "interface_definition.spy"
            this.Width = w;
#line (25, 9) - (25, 24) 1 "interface_definition.spy"
            this.Height = h;
        }
    }

    public static void Main()
    {
#line (34, 5) - (34, 35) 1 "interface_definition.spy"
        IDrawable circle = new Circle(5);
#line (35, 5) - (35, 25) 1 "interface_definition.spy"
        global::Sharpy.Builtins.Print(circle.Draw());
#line (36, 5) - (36, 25) 1 "interface_definition.spy"
        global::Sharpy.Builtins.Print(circle.Area());
#line (38, 5) - (38, 39) 1 "interface_definition.spy"
        IDrawable rect = new Rectangle(4, 6);
#line (39, 5) - (39, 23) 1 "interface_definition.spy"
        global::Sharpy.Builtins.Print(rect.Draw());
#line (40, 5) - (40, 23) 1 "interface_definition.spy"
        global::Sharpy.Builtins.Print(rect.Area());
    }
}
