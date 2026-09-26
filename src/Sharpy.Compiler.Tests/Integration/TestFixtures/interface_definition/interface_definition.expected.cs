// Snapshot: Interface definition with method signatures
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace InterfaceDefinition
{
    public static partial class InterfaceDefinitionModule
    {
        public static void Main()
        {
#line (34, 5) - (34, 35) 12 "interface_definition.spy"
            global::InterfaceDefinition.IDrawable circle = new global::InterfaceDefinition.Circle(5);
#line (35, 5) - (35, 25) 12 "interface_definition.spy"
            global::Sharpy.Builtins.Print(circle.Draw());
#line (36, 5) - (36, 25) 12 "interface_definition.spy"
            global::Sharpy.Builtins.Print(circle.Area());
#line (38, 5) - (38, 39) 12 "interface_definition.spy"
            global::InterfaceDefinition.IDrawable rect = new global::InterfaceDefinition.Rectangle(4, 6);
#line (39, 5) - (39, 23) 12 "interface_definition.spy"
            global::Sharpy.Builtins.Print(rect.Draw());
#line (40, 5) - (40, 23) 12 "interface_definition.spy"
            global::Sharpy.Builtins.Print(rect.Area());
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "IDrawable")]
    public interface IDrawable
    {
        string Draw();
        int Area();
    }

    [global::Sharpy.SharpyModuleType("__main__", "Circle")]
    public class Circle : global::InterfaceDefinition.IDrawable
    {
        public int Radius;
        public virtual string Draw()
#line 13 "interface_definition.spy"
        {
#line (14, 9) - (14, 33) 12 "interface_definition.spy"
            return "Drawing Circle";
#line hidden
        }

        public virtual int Area()
#line 16 "interface_definition.spy"
        {
#line (17, 9) - (17, 46) 12 "interface_definition.spy"
            return 3 * this.Radius * this.Radius;
#line hidden
        }

        public Circle(int r)
#line 10 "interface_definition.spy"
        {
#line (11, 9) - (11, 24) 12 "interface_definition.spy"
            this.Radius = r;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Rectangle")]
    public class Rectangle : global::InterfaceDefinition.IDrawable
    {
        public int Width;
        public int Height;
        public virtual string Draw()
#line 27 "interface_definition.spy"
        {
#line (28, 9) - (28, 36) 12 "interface_definition.spy"
            return "Drawing Rectangle";
#line hidden
        }

        public virtual int Area()
#line 30 "interface_definition.spy"
        {
#line (31, 9) - (31, 41) 12 "interface_definition.spy"
            return this.Width * this.Height;
#line hidden
        }

        public Rectangle(int w, int h)
#line 23 "interface_definition.spy"
        {
#line (24, 9) - (24, 23) 12 "interface_definition.spy"
            this.Width = w;
#line (25, 9) - (25, 24) 12 "interface_definition.spy"
            this.Height = h;
#line hidden
        }
    }
}
#line default
