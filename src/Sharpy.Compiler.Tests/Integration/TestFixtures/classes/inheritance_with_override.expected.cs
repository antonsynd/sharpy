// Snapshot: Class inheritance with @override methods
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class InheritanceWithOverride
{
    public class Animal
    {
        public string Name;
        public virtual int Speak()
#line 10 "inheritance_with_override.spy"
        {
#line (11, 9) - (11, 17) 1 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(0);
#line (12, 9) - (12, 18) 1 "inheritance_with_override.spy"
            return 0;
        }

        public int GetLegs()
#line 14 "inheritance_with_override.spy"
        {
#line (15, 9) - (15, 18) 1 "inheritance_with_override.spy"
            return 4;
        }

        public Animal(string name)
#line 6 "inheritance_with_override.spy"
        {
#line (7, 9) - (7, 25) 1 "inheritance_with_override.spy"
            this.Name = name;
        }
    }

    public class Dog : Animal
    {
        public string Breed;
        public override int Speak()
#line 25 "inheritance_with_override.spy"
        {
#line (26, 9) - (26, 17) 1 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(1);
#line (27, 9) - (27, 18) 1 "inheritance_with_override.spy"
            return 1;
        }

        public Dog(string name, string breed) : base(name)
#line 20 "inheritance_with_override.spy"
        {
#line (22, 9) - (22, 27) 1 "inheritance_with_override.spy"
            this.Breed = breed;
        }
    }

    public class Cat : Animal
    {
        public bool Indoor;
        public override int Speak()
#line 37 "inheritance_with_override.spy"
        {
#line (38, 9) - (38, 17) 1 "inheritance_with_override.spy"
            global::Sharpy.Builtins.Print(2);
#line (39, 9) - (39, 18) 1 "inheritance_with_override.spy"
            return 2;
        }

        public Cat(string name, bool indoor) : base(name)
#line 32 "inheritance_with_override.spy"
        {
#line (34, 9) - (34, 29) 1 "inheritance_with_override.spy"
            this.Indoor = indoor;
        }
    }

    public static void Main()
    {
#line (44, 5) - (44, 33) 1 "inheritance_with_override.spy"
        var dog = new Dog("Rex", "Shepherd");
#line (45, 5) - (45, 32) 1 "inheritance_with_override.spy"
        var cat = new Cat("Whiskers", true);
#line (46, 5) - (46, 31) 1 "inheritance_with_override.spy"
        var animal = new Animal("Generic");
#line (48, 5) - (48, 19) 1 "inheritance_with_override.spy"
        animal.Speak();
#line (49, 5) - (49, 16) 1 "inheritance_with_override.spy"
        dog.Speak();
#line (50, 5) - (50, 16) 1 "inheritance_with_override.spy"
        cat.Speak();
#line (51, 5) - (51, 26) 1 "inheritance_with_override.spy"
        global::Sharpy.Builtins.Print(dog.GetLegs());
#line (52, 5) - (52, 26) 1 "inheritance_with_override.spy"
        global::Sharpy.Builtins.Print(cat.GetLegs());
    }
}
