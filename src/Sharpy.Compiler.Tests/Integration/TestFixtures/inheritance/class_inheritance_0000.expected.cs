#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassInheritance0000
{
    public static class Program
    {
        public static void Main()
        {
#line 24 "class_inheritance_0000.spy"
            var d = new Dog("Buddy", 3, "Labrador");
#line 25 "class_inheritance_0000.spy"
            global::Sharpy.Core.Exports.Print(d.GetAge());
#line 26 "class_inheritance_0000.spy"
            global::Sharpy.Core.Exports.Print(d.GetYearsInDogYears());
        }
    }

    public class Animal
    {
        public string Name;
        public int Age;
        public int GetAge()
        {
#line 11 "class_inheritance_0000.spy"
            return this.Age;
        }

        public Animal(string name, int age)
        {
#line 7 "class_inheritance_0000.spy"
            this.Name = name;
#line 8 "class_inheritance_0000.spy"
            this.Age = age;
        }
    }

    public class Dog : Animal
    {
        public string Breed;
        public int GetYearsInDogYears()
        {
#line 21 "class_inheritance_0000.spy"
            return this.Age * 7;
        }

        public Dog(string name, int age, string breed) : base(name, age)
        {
#line 18 "class_inheritance_0000.spy"
            this.Breed = breed;
        }
    }
}
