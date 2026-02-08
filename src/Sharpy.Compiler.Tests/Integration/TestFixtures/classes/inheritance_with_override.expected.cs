// Snapshot: Class inheritance with @override methods
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class InheritanceWithOverride
{
    public class Animal
    {
        public string Name;
        public virtual int Speak()
        {
#line 11 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(0);
#line 12 "inheritance_with_override.spy"
            return 0;
        }

        public int GetLegs()
        {
#line 15 "inheritance_with_override.spy"
            return 4;
        }

        public Animal(string name)
        {
#line 7 "inheritance_with_override.spy"
            this.Name = name;
        }
    }

    public class Dog : Animal
    {
        public string Breed;
        public override int Speak()
        {
#line 26 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(1);
#line 27 "inheritance_with_override.spy"
            return 1;
        }

        public Dog(string name, string breed) : base(name)
        {
#line 22 "inheritance_with_override.spy"
            this.Breed = breed;
        }
    }

    public class Cat : Animal
    {
        public bool Indoor;
        public override int Speak()
        {
#line 38 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(2);
#line 39 "inheritance_with_override.spy"
            return 2;
        }

        public Cat(string name, bool indoor) : base(name)
        {
#line 34 "inheritance_with_override.spy"
            this.Indoor = indoor;
        }
    }

    public static void Main()
    {
#line 44 "inheritance_with_override.spy"
        var dog = new Dog("Rex", "Shepherd");
#line 45 "inheritance_with_override.spy"
        var cat = new Cat("Whiskers", true);
#line 46 "inheritance_with_override.spy"
        var animal = new Animal("Generic");
#line 48 "inheritance_with_override.spy"
        animal.Speak();
#line 49 "inheritance_with_override.spy"
        dog.Speak();
#line 50 "inheritance_with_override.spy"
        cat.Speak();
#line 51 "inheritance_with_override.spy"
        global::Sharpy.Builtins.Print(dog.GetLegs());
#line 52 "inheritance_with_override.spy"
        global::Sharpy.Builtins.Print(cat.GetLegs());
    }
}
