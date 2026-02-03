#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AbstractClass0002
{
    public static class Program
    {
        public static void Main()
        {
#line 20 "abstract_class_0002.spy"
            var sq = new Square(5);
#line 21 "abstract_class_0002.spy"
            global::Sharpy.Core.Exports.Print(sq.Area());
        }
    }

    public abstract class Shape
    {
        public abstract int Area();
    }

    public class Square : Shape
    {
        public int Side;
        public override int Area()
        {
#line 17 "abstract_class_0002.spy"
            return this.Side * this.Side;
        }

        public Square(int s)
        {
#line 13 "abstract_class_0002.spy"
            this.Side = s;
        }
    }
}
