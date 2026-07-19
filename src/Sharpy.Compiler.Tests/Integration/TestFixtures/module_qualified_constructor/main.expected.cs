// Snapshot: module-qualified constructor calls emit `new global::...` object creation.
// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;
using shapes = Sharpy.Test.Shapes;

namespace Sharpy.Test
{
    public static partial class Program
    {
        public static void Main()
        {
#line (10, 5) - (10, 27) 12 "main.spy"
            var p = new global::Sharpy.Test.Shapes.Point(3, 4);
#line (11, 5) - (11, 13) 12 "main.spy"
            global::Sharpy.Builtins.Print(p);
#line (14, 5) - (14, 39) 12 "main.spy"
            var c = new global::Sharpy.Test.Shapes.Color("red", alpha: 128);
#line (15, 5) - (15, 13) 12 "main.spy"
            global::Sharpy.Builtins.Print(c);
#line (18, 5) - (18, 35) 12 "main.spy"
            var c2 = new global::Sharpy.Test.Shapes.Color(name: "blue");
#line (19, 5) - (19, 14) 12 "main.spy"
            global::Sharpy.Builtins.Print(c2);
#line hidden
        }
    }
}
#line default


// shapes.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;

namespace Sharpy.Test
{
    [global::Sharpy.SharpyModule("shapes")]
    public static partial class Shapes
    {
        public class Point
        {
            public int X;
            public int Y;
            public override string ToString()
#line 9 "shapes.spy"
            {
#line (10, 9) - (10, 40) 16 "shapes.spy"
                return FormattableString.Invariant($"({(this.X)}, {(this.Y)})");
#line hidden
            }

            public Point(int x, int y)
#line 5 "shapes.spy"
            {
#line (6, 9) - (6, 19) 16 "shapes.spy"
                this.X = x;
#line (7, 9) - (7, 19) 16 "shapes.spy"
                this.Y = y;
#line hidden
            }
        }

        public class Color
        {
            public string Name;
            public int Alpha;
            public override string ToString()
#line 20 "shapes.spy"
            {
#line (21, 9) - (21, 44) 16 "shapes.spy"
                return FormattableString.Invariant($"{(this.Name)}@{(this.Alpha)}");
#line hidden
            }

            public Color(string name, int alpha = 255)
#line 16 "shapes.spy"
            {
#line (17, 9) - (17, 25) 16 "shapes.spy"
                this.Name = name;
#line (18, 9) - (18, 27) 16 "shapes.spy"
                this.Alpha = alpha;
#line hidden
            }
        }
    }
}
#line default
