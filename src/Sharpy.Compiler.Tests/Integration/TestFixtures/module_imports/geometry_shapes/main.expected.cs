// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;
using static Sharpy.Test.Validators.Exports;
using Sharpy.Test.Validators;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            Sharpy.Test.Geometry.Rectangle rect = new Sharpy.Test.Geometry.Rectangle(4, 6);
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            int area = rect.Area();
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            global::Sharpy.Core.Exports.Print(area);
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            bool squareCheck = IsSquare(rect);
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            if (squareCheck)
            {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(1);
            }
            else
            {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(0);
            }

#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            Sharpy.Test.Geometry.Rectangle square = new Sharpy.Test.Geometry.Rectangle(5, 5);
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            if (IsSquare(square))
            {
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(1);
            }
            else
            {
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(0);
            }

#line 27 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            int scaled = CalculateScaledArea(rect, 3);
#line 28 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            global::Sharpy.Core.Exports.Print(scaled);
#line 31 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            bool valid = ValidatePositiveDimensions(square);
#line 32 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
            if (valid)
            {
#line 33 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(1);
            }
            else
            {
#line 35 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/main.spy"
                global::Sharpy.Core.Exports.Print(0);
            }
        }
    }
}

// geometry.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.Geometry
{
    public static class Exports
    {
        public static int CalculateScaledArea(Rectangle rect, int scaleFactor)
        {
#line 30 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            int baseArea = rect.Area();
#line 31 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            return baseArea * scaleFactor;
        }
    }

    public class Point
    {
        public int X;
        public int Y;
        public int DistanceFromOrigin()
        {
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            return this.X * this.X + this.Y * this.Y;
        }

        public Point(int xVal, int yVal)
        {
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            this.X = xVal;
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            this.Y = yVal;
        }
    }

    public class Rectangle
    {
        public int Width;
        public int Height;
        public int Area()
        {
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            return this.Width * this.Height;
        }

        public int Perimeter()
        {
#line 27 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            return 2 * (this.Width + this.Height);
        }

        public Rectangle(int w, int h)
        {
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            this.Width = w;
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/geometry.spy"
            this.Height = h;
        }
    }
}

// validators.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;

namespace Sharpy.Test.Validators
{
    public static class Exports
    {
        public static bool IsSquare(Sharpy.Test.Geometry.Rectangle rect)
        {
#line 6 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
            return rect.Width == rect.Height;
        }

        public static bool IsAtOrigin(Sharpy.Test.Geometry.Point point)
        {
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
            if (point.X == 0)
            {
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
                if (point.Y == 0)
                {
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
                    return true;
                }
            }

#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
            return false;
        }

        public static bool ValidatePositiveDimensions(Sharpy.Test.Geometry.Rectangle rect)
        {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
            if (rect.Width > 0)
            {
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
                if (rect.Height > 0)
                {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
                    return true;
                }
            }

#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/geometry_shapes/validators.spy"
            return false;
        }
    }
}
