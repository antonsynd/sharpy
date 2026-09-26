#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NestedUnionInClass1729
{
    public static partial class NestedUnionInClass1729Module
    {
        public static void Main()
        {
#line (18, 5) - (18, 24) 12 "nested_union_in_class_1729.spy"
            global::NestedUnionInClass1729.Outer o = new global::NestedUnionInClass1729.Outer();
#line (19, 5) - (19, 41) 12 "nested_union_in_class_1729.spy"
            global::Sharpy.Builtins.Print(o.Area(new global::NestedUnionInClass1729.Outer.Shape.Circle(2)));
#line (20, 5) - (20, 41) 12 "nested_union_in_class_1729.spy"
            global::Sharpy.Builtins.Print(o.Area(new global::NestedUnionInClass1729.Outer.Shape.Square(4)));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Outer")]
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
                case global::NestedUnionInClass1729.Outer.Shape.Circle(var r):
#line (12, 17) - (12, 34) 20 "nested_union_in_class_1729.spy"
                    return r * r * 3;
#line hidden
                case global::NestedUnionInClass1729.Outer.Shape.Square(var side):
#line (14, 17) - (14, 36) 20 "nested_union_in_class_1729.spy"
                    return side * side;
#line hidden
                default:
                    throw new System.InvalidOperationException("Unreachable: exhaustive match");
            }
        }
    }
}
#line default
