#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.StructDefinition0001
{
    public static class Program
    {
        public static void CompareShapes()
        {
#line 26 "struct_definition_0001.spy"
            Rectangle rect = new Rectangle();
#line 27 "struct_definition_0001.spy"
            rect.Width = 5;
#line 28 "struct_definition_0001.spy"
            rect.Height = 8;
#line 30 "struct_definition_0001.spy"
            Rectangle square = new Rectangle();
#line 31 "struct_definition_0001.spy"
            square.Width = 6;
#line 32 "struct_definition_0001.spy"
            square.Height = 6;
#line 34 "struct_definition_0001.spy"
            Circle circ = new Circle();
#line 35 "struct_definition_0001.spy"
            circ.Radius = 4;
#line 37 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 38 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(rect.Perimeter());
#line 39 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(square.IsSquare());
#line 40 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(rect.IsSquare());
#line 41 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(circ.Diameter());
#line 42 "struct_definition_0001.spy"
            global::Sharpy.Core.Exports.Print(circ.ApproximateArea());
        }

        public static void Main()
        {
#line 45 "struct_definition_0001.spy"
            CompareShapes();
        }
    }

    public struct Rectangle
    {
        public int Width;
        public int Height;
        public int Area()
        {
#line 7 "struct_definition_0001.spy"
            return this.Width * this.Height;
        }

        public int Perimeter()
        {
#line 10 "struct_definition_0001.spy"
            return 2 * (this.Width + this.Height);
        }

        public bool IsSquare()
        {
#line 13 "struct_definition_0001.spy"
            return this.Width == this.Height;
        }
    }

    public struct Circle
    {
        public int Radius;
        public int Diameter()
        {
#line 19 "struct_definition_0001.spy"
            return this.Radius * 2;
        }

        public int ApproximateArea()
        {
#line 23 "struct_definition_0001.spy"
            return 3 * this.Radius * this.Radius;
        }
    }
}
