#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassRectangleInit
{
    public static class Program
    {
        public static void Main()
        {
#line 14 "class_rectangle_init.spy"
            var r = new Rectangle(5, 3);
#line 15 "class_rectangle_init.spy"
            global::Sharpy.Core.Exports.Print(r.Width);
#line 16 "class_rectangle_init.spy"
            global::Sharpy.Core.Exports.Print(r.Height);
#line 17 "class_rectangle_init.spy"
            global::Sharpy.Core.Exports.Print(r.Area());
        }
    }

    public class Rectangle
    {
        public int Width;
        public int Height;
        public int Area()
        {
#line 11 "class_rectangle_init.spy"
            return this.Width * this.Height;
        }

        public Rectangle(int w, int h)
        {
#line 7 "class_rectangle_init.spy"
            this.Width = w;
#line 8 "class_rectangle_init.spy"
            this.Height = h;
        }
    }
}
