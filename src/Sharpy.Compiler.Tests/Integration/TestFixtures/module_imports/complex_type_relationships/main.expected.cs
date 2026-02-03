// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;
using static Sharpy.Test.Calculator.Exports;
using Sharpy.Test.Calculator;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void TestShapeHierarchy()
        {
#line 6 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            Sharpy.Test.Geometry.Rectangle rect = new Sharpy.Test.Geometry.Rectangle(5, 3);
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            string shapeName = rect.Describe();
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print(shapeName);
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            Sharpy.Test.Calculator.ShapeCalculator calc = new Sharpy.Test.Calculator.ShapeCalculator(2);
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            double area = calc.ComputeArea(rect);
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            double perimeter = calc.ComputePerimeter(rect);
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print(calc.RoundValue(area));
#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print(calc.RoundValue(perimeter));
        }

        public static void TestStruct()
        {
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            Sharpy.Test.Calculator.Dimensions dims = CreateDimensions(10, 8, 5);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print((int)dims.Length);
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print((int)dims.Width);
#line 25 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print((int)dims.Height);
        }

        public static void TestDirectRect()
        {
#line 28 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            Sharpy.Test.Geometry.Rectangle rect = new Sharpy.Test.Geometry.Rectangle(4, 6);
#line 29 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            double areaResult = rect.Area();
#line 30 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print((int)areaResult);
        }

        public static void Main()
        {
#line 33 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            global::Sharpy.Core.Exports.Print(999);
#line 34 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            TestShapeHierarchy();
#line 35 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            TestStruct();
#line 36 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/main.spy"
            TestDirectRect();
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
    }

    public class Rectangle
    {
        public double Width;
        public double Height;
        public string Name;
        public double Area()
        {
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            return this.Width * this.Height;
        }

        public double Perimeter()
        {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            return 2 * (this.Width + this.Height);
        }

        public string Describe()
        {
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            return this.Name;
        }

        public Rectangle(double w, double h)
        {
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            this.Name = "Rectangle";
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            this.Width = w;
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/geometry.spy"
            this.Height = h;
        }
    }
}

// calculator.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;

namespace Sharpy.Test.Calculator
{
    public static class Exports
    {
        public static Dimensions CreateDimensions(double length, double width, double height)
        {
#line 25 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            Dimensions d = new Dimensions();
#line 26 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            d.Length = length;
#line 27 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            d.Width = width;
#line 28 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            d.Height = height;
#line 29 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            return d;
        }
    }

    public struct Dimensions
    {
        public double Length;
        public double Width;
        public double Height;
    }

    public class ShapeCalculator
    {
        public int Precision;
        public double ComputeArea(Sharpy.Test.Geometry.Rectangle rect)
        {
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            return rect.Area();
        }

        public double ComputePerimeter(Sharpy.Test.Geometry.Rectangle rect)
        {
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            return rect.Perimeter();
        }

        public int RoundValue(double value)
        {
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            return (int)value;
        }

        public ShapeCalculator(int prec)
        {
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/complex_type_relationships/calculator.spy"
            this.Precision = prec;
        }
    }
}
