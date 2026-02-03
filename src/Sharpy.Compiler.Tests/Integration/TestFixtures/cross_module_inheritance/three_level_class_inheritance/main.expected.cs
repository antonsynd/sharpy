// mammal.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Animal.Exports;
using Sharpy.Test.Animal;

namespace Sharpy.Test.Mammal
{
    public static class Exports
    {
    }

    public class Mammal : Sharpy.Test.Animal.Animal
    {
        public bool WarmBlooded;
        public override string Speak()
        {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/mammal.spy"
            return "mammal sound";
        }

        public bool IsWarmBlooded()
        {
#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/mammal.spy"
            return this.WarmBlooded;
        }

        public Mammal(string name) : base(name)
        {
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/mammal.spy"
            this.WarmBlooded = true;
        }
    }
}

// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Dog.Exports;
using Sharpy.Test.Dog;
using static Sharpy.Test.Mammal.Exports;
using Sharpy.Test.Mammal;
using static Sharpy.Test.Animal.Exports;
using Sharpy.Test.Animal;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 9 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            Sharpy.Test.Dog.Dog dog = new Sharpy.Test.Dog.Dog("Rex", "Shepherd");
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(dog.Speak());
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(dog.GetName());
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(dog.IsWarmBlooded());
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(dog.GetBreed());
#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            Sharpy.Test.Animal.Animal animal = dog;
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(animal.Speak());
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/main.spy"
            global::Sharpy.Core.Exports.Print(animal.GetName());
        }
    }
}

// dog.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Mammal.Exports;
using Sharpy.Test.Mammal;

namespace Sharpy.Test.Dog
{
    public static class Exports
    {
    }

    public class Dog : Sharpy.Test.Mammal.Mammal
    {
        public string Breed;
        public override string Speak()
        {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/dog.spy"
            return "woof";
        }

        public string GetBreed()
        {
#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/dog.spy"
            return this.Breed;
        }

        public Dog(string name, string breed) : base(name)
        {
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/dog.spy"
            this.Breed = breed;
        }
    }
}

// animal.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.Animal
{
    public static class Exports
    {
    }

    public class Animal
    {
        public string Name;
        public virtual string Speak()
        {
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/animal.spy"
            return "...";
        }

        public string GetName()
        {
#line 15 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/animal.spy"
            return this.Name;
        }

        public Animal(string name)
        {
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/three_level_class_inheritance/animal.spy"
            this.Name = name;
        }
    }
}
