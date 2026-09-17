#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NestedUnionInClass1729
{
    public class Outer
    {
        public abstract class Shape
        {
            private Shape()
            {
            }

            public sealed class Circle : Shape
            {
                public int Radius { get; }

                public Circle(int radius)
                {
                    Radius = radius;
                }

                public void Deconstruct(out int radius)
                {
                    radius = Radius;
                }
            }

            public sealed class Square : Shape
            {
                public int Side { get; }

                public Square(int side)
                {
                    Side = side;
                }

                public void Deconstruct(out int side)
                {
                    side = Side;
                }
            }
        }

        public int Area(Outer.Shape s)
#line 9 "nested_union_in_class_1729.spy"
        {
#line (10, 9) - (14, 36) 12 "nested_union_in_class_1729.spy"
            switch (s)
#line hidden
            {
                case Outer.Shape.Circle(var r):
#line (12, 17) - (12, 34) 20 "nested_union_in_class_1729.spy"
                    return r * r * 3;
#line hidden
                case Outer.Shape.Square(var side):
#line (14, 17) - (14, 36) 20 "nested_union_in_class_1729.spy"
                    return side * side;
#line hidden
                default:
                    throw new System.InvalidOperationException("Unreachable: exhaustive match");
            }
        }
    }

    public static void Main()
    {
#line (18, 5) - (18, 24) 8 "nested_union_in_class_1729.spy"
        Outer o = new Outer();
#line (19, 5) - (19, 41) 8 "nested_union_in_class_1729.spy"
        global::Sharpy.Builtins.Print(o.Area(new Outer.Shape.Circle(2)));
#line (20, 5) - (20, 41) 8 "nested_union_in_class_1729.spy"
        global::Sharpy.Builtins.Print(o.Area(new Outer.Shape.Square(4)));
#line hidden
    }
}
#line default
