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
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            Sharpy.Test.Geometry.Rectangle rect = new Sharpy.Test.Geometry.Rectangle(5, 3);
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            Sharpy.Test.Geometry.Circle circ = new Sharpy.Test.Geometry.Circle(2);
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            global::Sharpy.Core.Exports.Print(rect.Describe());
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            global::Sharpy.Core.Exports.Print(circ.Describe());
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            Sharpy.Test.Validators.ValidationResult validation = ValidatePositive(rect.Width);
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            global::Sharpy.Core.Exports.Print(validation.Message);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            bool rectValid = ValidateShape(rect);
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            bool circValid = ValidateShape(circ);
#line 25 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            global::Sharpy.Core.Exports.Print(rectValid);
#line 26 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/main.spy"
            global::Sharpy.Core.Exports.Print(circValid);
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

    public abstract class Shape
    {
        public string Name;
        public abstract double Area();
        public abstract double Perimeter();
        public string Describe()
        {
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            return this.Name;
        }

        public Shape(string shapeName)
        {
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            this.Name = shapeName;
        }
    }

    public class Rectangle : Shape
    {
        public double Width;
        public double Height;
        public override double Area()
        {
#line 33 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            return this.Width * this.Height;
        }

        public override double Perimeter()
        {
#line 37 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            return 2 * (this.Width + this.Height);
        }

        public Rectangle(double w, double h) : base("Rectangle")
        {
#line 28 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            this.Width = w;
#line 29 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            this.Height = h;
        }
    }

    public class Circle : Shape
    {
        public double Radius;
        public override double Area()
        {
#line 48 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            return 3.14159 * this.Radius * this.Radius;
        }

        public override double Perimeter()
        {
#line 52 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            return 2 * 3.14159 * this.Radius;
        }

        public Circle(double r) : base("Circle")
        {
#line 44 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/geometry.spy"
            this.Radius = r;
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
        public static ValidationResult ValidatePositive(double value)
        {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
            if (value > 0)
            {
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
                return new ValidationResult(true, "positive");
            }

#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
            return new ValidationResult(false, "not positive");
        }

        public static bool ValidateShape(Sharpy.Test.Geometry.Shape shape)
        {
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
            return shape.Area() > 0 && shape.Perimeter() > 0;
        }
    }

    public class ValidationResult
    {
        public bool IsValid;
        public string Message;
        public ValidationResult(bool valid, string msg)
        {
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
            this.IsValid = valid;
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/abstract_inheritance_with_validation/validators.spy"
            this.Message = msg;
        }
    }
}
