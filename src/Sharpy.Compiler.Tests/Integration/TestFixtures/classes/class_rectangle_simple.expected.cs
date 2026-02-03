#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassRectangleSimple
{
    public static class Program
    {
        public static void Main()
        {
#line 17 "class_rectangle_simple.spy"
            var rect = new Rectangle(5, 3);
#line 18 "class_rectangle_simple.spy"
            global::Sharpy.Core.Exports.Print(rect.Area());
#line 19 "class_rectangle_simple.spy"
            global::Sharpy.Core.Exports.Print(rect.Perimeter());
        }
    }

    public class Rectangle
    {
        public int Width;
        public int Height;
        public int Area()
        {
#line 11 "class_rectangle_simple.spy"
            return this.Width * this.Height;
        }

        public int Perimeter()
        {
#line 14 "class_rectangle_simple.spy"
            return 2 * (this.Width + this.Height);
        }

        public Rectangle(int w, int h)
        {
#line 7 "class_rectangle_simple.spy"
            this.Width = w;
#line 8 "class_rectangle_simple.spy"
            this.Height = h;
        }
    }
}
