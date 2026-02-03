// analyzer.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Shapes.Exports;
using Sharpy.Test.Shapes;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;

namespace Sharpy.Test.Analyzer
{
    public static class Exports
    {
    }

    public class ShapeAnalyzer
    {
        public int TotalShapes;
        public void AnalyzeRectangle(Sharpy.Test.Shapes.Rectangle rect)
        {
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            this.TotalShapes = this.TotalShapes + 1;
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            double area = rect.GetArea();
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            double perimeter = rect.GetPerimeter();
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            global::Sharpy.Core.Exports.Print(area);
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            global::Sharpy.Core.Exports.Print(perimeter);
        }

        public void AnalyzeCircle(Sharpy.Test.Shapes.Circle circ)
        {
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            this.TotalShapes = this.TotalShapes + 1;
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            double area = circ.GetArea();
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            double circumference = circ.GetPerimeter();
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            global::Sharpy.Core.Exports.Print(area);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            global::Sharpy.Core.Exports.Print(circumference);
        }

        public int GetTotal()
        {
#line 26 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            return this.TotalShapes;
        }

        public ShapeAnalyzer()
        {
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/analyzer.spy"
            this.TotalShapes = 0;
        }
    }
}

// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Shapes.Exports;
using Sharpy.Test.Shapes;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;
using static Sharpy.Test.Analyzer.Exports;
using Sharpy.Test.Analyzer;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            global::Sharpy.Core.Exports.Print(100);
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            Sharpy.Test.Shapes.Rectangle rect = new Sharpy.Test.Shapes.Rectangle(5, 3);
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            rect.PrintName();
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            Sharpy.Test.Shapes.Circle circ = new Sharpy.Test.Shapes.Circle(2);
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            circ.PrintName();
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            Sharpy.Test.Analyzer.ShapeAnalyzer analyzer = new Sharpy.Test.Analyzer.ShapeAnalyzer();
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            analyzer.AnalyzeRectangle(rect);
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            analyzer.AnalyzeCircle(circ);
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            int total = analyzer.GetTotal();
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            global::Sharpy.Core.Exports.Print(total);
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            Sharpy.Test.Geometry.Point point = new Sharpy.Test.Geometry.Point(3, 4);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            double distance = point.DistanceFromOrigin();
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/main.spy"
            global::Sharpy.Core.Exports.Print(distance);
        }
    }
}

// shapes.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Geometry.Exports;
using Sharpy.Test.Geometry;

namespace Sharpy.Test.Shapes
{
    public static class Exports
    {
    }

    public class Rectangle : Sharpy.Test.Geometry.Shape, Sharpy.Test.Geometry.IMeasurable
    {
        public double Width;
        public double Height;
        public double GetArea()
        {
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return this.Width * this.Height;
        }

        public double GetPerimeter()
        {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return 2 * (this.Width + this.Height);
        }

        public override string Describe()
        {
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            double area = this.GetArea();
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return this.Name;
        }

        public Rectangle(double w, double h) : base("Rectangle")
        {
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            this.Width = w;
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            this.Height = h;
        }
    }

    public class Circle : Sharpy.Test.Geometry.Shape, Sharpy.Test.Geometry.IMeasurable
    {
        public double Radius;
        public double GetArea()
        {
#line 32 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return 3.14159 * this.Radius * this.Radius;
        }

        public double GetPerimeter()
        {
#line 35 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return 2 * 3.14159 * this.Radius;
        }

        public override string Describe()
        {
#line 39 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            double circumference = this.GetPerimeter();
#line 40 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            return this.Name;
        }

        public Circle(double r) : base("Circle")
        {
#line 29 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/shapes.spy"
            this.Radius = r;
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

    public interface IMeasurable
    {
        double GetArea();
        double GetPerimeter();
    }

    public abstract class Shape
    {
        public string Name;
        public abstract string Describe();
        public void PrintName()
        {
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/geometry.spy"
            global::Sharpy.Core.Exports.Print(this.Name);
        }

        public Shape(string shapeName)
        {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/geometry.spy"
            this.Name = shapeName;
        }
    }

    public class Point
    {
        public double X;
        public double Y;
        public double DistanceFromOrigin()
        {
#line 35 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/geometry.spy"
            return System.Math.Pow((this.X * this.X + this.Y * this.Y), 0.5);
        }

        public Point(double xCoord, double yCoord)
        {
#line 31 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/geometry.spy"
            this.X = xCoord;
#line 32 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/multifile_import_chain/geometry.spy"
            this.Y = yCoord;
        }
    }
}
