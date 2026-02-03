#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassFieldAccessScale
{
    public static class Program
    {
        public static void Main()
        {
#line 36 "class_field_access_scale.spy"
            var rect = new Rectangle(5, 3, "MyRect");
#line 37 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Width);
#line 38 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Height);
#line 39 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 40 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Perimeter());
#line 42 "class_field_access_scale.spy"
            rect.Scale(2);
#line 43 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Width);
#line 44 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Height);
#line 45 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 47 "class_field_access_scale.spy"
            var point = new Point(4, 6);
#line 48 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(point.X);
#line 49 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(point.Y);
#line 50 "class_field_access_scale.spy"
            global::Sharpy.Core.Exports.Print(point.DistanceFromOrigin());
        }
    }

    public class Rectangle
    {
        public int Width;
        public int Height;
        public string Label;
        public int Area()
        {
#line 14 "class_field_access_scale.spy"
            return this.Width * this.Height;
        }

        public int Perimeter()
        {
#line 17 "class_field_access_scale.spy"
            return 2 * this.Width + 2 * this.Height;
        }

        public void Scale(int factor)
        {
#line 20 "class_field_access_scale.spy"
            this.Width = this.Width * factor;
#line 21 "class_field_access_scale.spy"
            this.Height = this.Height * factor;
        }

        public Rectangle(int w, int h, string name)
        {
#line 9 "class_field_access_scale.spy"
            this.Width = w;
#line 10 "class_field_access_scale.spy"
            this.Height = h;
#line 11 "class_field_access_scale.spy"
            this.Label = name;
        }
    }

    public class Point
    {
        public int X;
        public int Y;
        public int DistanceFromOrigin()
        {
#line 33 "class_field_access_scale.spy"
            return this.X >= 0 && this.Y >= 0 ? this.X + this.Y : 0;
        }

        public Point(int px, int py)
        {
#line 28 "class_field_access_scale.spy"
            this.X = px;
#line 29 "class_field_access_scale.spy"
            this.Y = py;
        }
    }
}
